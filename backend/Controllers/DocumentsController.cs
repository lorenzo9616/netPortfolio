using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OcrApi.Data;
using OcrApi.Models;
using OcrApi.Repositories;
using OcrApi.Services;

namespace OcrApi.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private static readonly HashSet<string> _allowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "application/pdf"
    };

    private readonly IOcrClient _ocrClient;
    private readonly IFieldMatcher _fieldMatcher;
    private readonly IOcrPropertyRepository _propertyRepository;
    private readonly OcrDbContext _dbContext;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IOcrClient ocrClient,
        IFieldMatcher fieldMatcher,
        IOcrPropertyRepository propertyRepository,
        OcrDbContext dbContext,
        ILogger<DocumentsController> logger)
    {
        _ocrClient          = ocrClient;
        _fieldMatcher       = fieldMatcher;
        _propertyRepository = propertyRepository;
        _dbContext          = dbContext;
        _logger             = logger;
    }

    // ── POST /api/documents/analyze ──────────────────────────────────────────

    [HttpPost("analyze")]
    [EnableRateLimiting("ocr-policy")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AnalyzeDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Analyze(
        IFormFile file,
        [FromForm] AnalyzeDocumentRequest request,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty file is required.");

        var contentType = file.ContentType?.Trim();
        if (string.IsNullOrEmpty(contentType) || !_allowedContentTypes.Contains(contentType))
        {
            _logger.LogWarning(
                "DocumentsController: unsupported content-type '{ContentType}' rejected", contentType);
            return StatusCode(
                StatusCodes.Status415UnsupportedMediaType,
                $"Unsupported file type '{contentType}'. Allowed: image/png, image/jpeg, application/pdf.");
        }

        _logger.LogInformation(
            "DocumentsController: Analyze started — file='{FileName}' size={Size} bytes",
            file.FileName, file.Length);

        // Buffer file bytes so we can store them AND pass to OCR without reading the stream twice
        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        var allProperties = await _propertyRepository.GetAllAsync();
        var activeProperties = allProperties.Where(p => p.IsActive).ToList();

        ExtractTextResponse ocrResult;
        try
        {
            await using var ocrStream = new MemoryStream(fileBytes);
            ocrResult = await _ocrClient.ExtractTextAsync(
                ocrStream,
                file.FileName,
                contentType,
                request.CropX,
                request.CropY,
                request.CropWidth,
                request.CropHeight,
                ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "DocumentsController: OCR service failure for '{FileName}'", file.FileName);
            return StatusCode(StatusCodes.Status502BadGateway,
                "The OCR service is unavailable or returned an error. Please try again later.");
        }

        var extractedFields = _fieldMatcher.MatchFields(activeProperties, ocrResult);

        var analysisResult = new AnalysisResult
        {
            FileName   = file.FileName,
            RawText    = ocrResult.RawText,
            AnalyzedAt = DateTime.UtcNow,
            ImageBytes = fileBytes,
            Fields     = extractedFields.Select(f => new SavedField
            {
                PropertyName   = f.PropertyName,
                ExtractedValue = f.ExtractedValue,
                Confidence     = f.Confidence
            }).ToList(),
            TextBlocks = ocrResult.TextBlocks.Select(b => new SavedTextBlock
            {
                Text       = b.Text,
                Confidence = b.Confidence,
                Page       = b.Page,
                BboxX      = b.BoundingBox.X,
                BboxY      = b.BoundingBox.Y,
                BboxWidth  = b.BoundingBox.Width,
                BboxHeight = b.BoundingBox.Height,
            }).ToList()
        };

        _dbContext.AnalysisResults.Add(analysisResult);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentsController: Analyze complete — AnalysisResult.Id={Id}, {FieldCount} field(s), {BlockCount} token(s) saved",
            analysisResult.Id, analysisResult.Fields.Count, analysisResult.TextBlocks.Count);

        return Ok(new AnalyzeDocumentResponse
        {
            DocumentId      = analysisResult.Id,
            RawText         = ocrResult.RawText,
            ExtractedFields = extractedFields
        });
    }

    // ── GET /api/documents/{id} ──────────────────────────────────────────────

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AnalysisResultDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        // Step 1: project to an anonymous type — ImageBytes is intentionally excluded (large, not needed here)
        var raw = await _dbContext.AnalysisResults
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.FileName,
                r.RawText,
                r.AnalyzedAt,
                r.SignatureImage,
                Fields = r.Fields.Select(f => new ExtractedFieldDto
                {
                    PropertyName   = f.PropertyName,
                    ExtractedValue = f.ExtractedValue,
                    ManualOverride = f.ManualOverride,
                    Confidence     = f.Confidence,
                }).ToList(),
                TextBlocks = r.TextBlocks.Select(b => new TextBlockDto
                {
                    Text       = b.Text,
                    Confidence = b.Confidence,
                    Page       = b.Page,
                    BboxX      = b.BboxX,
                    BboxY      = b.BboxY,
                    BboxWidth  = b.BboxWidth,
                    BboxHeight = b.BboxHeight,
                }).ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (raw is null)
            return NotFound($"No analysis result found with id {id}.");

        // Step 2: base64 conversion happens in C#, not in the SQL query
        var dto = new AnalysisResultDetailDto
        {
            DocumentId     = raw.Id,
            FileName       = raw.FileName,
            RawText        = raw.RawText,
            AnalyzedAt     = raw.AnalyzedAt,
            SignatureImage = raw.SignatureImage is not null
                               ? Convert.ToBase64String(raw.SignatureImage)
                               : null,
            ExtractedFields = raw.Fields,
            TextBlocks      = raw.TextBlocks,
        };

        return Ok(dto);
    }

    // ── PUT /api/documents/{id}/fields ───────────────────────────────────────

    [HttpPut("{id:int}/fields")]
    [ProducesResponseType(typeof(List<ExtractedFieldDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFields(
        int id,
        [FromBody] List<SavedFieldUpdateDto> updates,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "DocumentsController: UpdateFields started — AnalysisResult.Id={Id}", id);

        var analysisResult = await _dbContext.AnalysisResults
            .Include(r => r.Fields)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (analysisResult is null)
            return NotFound($"No analysis result found with id {id}.");

        foreach (var update in updates)
        {
            var field = analysisResult.Fields
                .FirstOrDefault(f => string.Equals(
                    f.PropertyName, update.PropertyName,
                    StringComparison.OrdinalIgnoreCase));

            if (field is not null)
                field.ManualOverride = update.ManualOverride;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentsController: UpdateFields complete — AnalysisResult.Id={Id}", id);

        // Map to DTO — avoids circular reference from the AnalysisResult navigation property
        var dtos = analysisResult.Fields.Select(f => new ExtractedFieldDto
        {
            PropertyName   = f.PropertyName,
            ExtractedValue = f.ExtractedValue,
            ManualOverride = f.ManualOverride,
            Confidence     = f.Confidence,
        }).ToList();

        return Ok(dtos);
    }

    // ── GET /api/documents/{id}/image ────────────────────────────────────────

    [HttpGet("{id:int}/image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(int id, CancellationToken ct)
    {
        var row = await _dbContext.AnalysisResults
            .Where(r => r.Id == id)
            .Select(r => new { r.FileName, r.ImageBytes })
            .FirstOrDefaultAsync(ct);

        if (row is null || row.ImageBytes is null)
            return NotFound("Image not available for this document.");

        var contentType = GetContentType(row.FileName);
        return File(row.ImageBytes, contentType);
    }

    // ── PATCH /api/documents/{id}/signature ──────────────────────────────────

    [HttpPatch("{id:int}/signature")]
    [ProducesResponseType(typeof(SignatureCaptureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CaptureSignature(
        int id,
        [FromBody] SignatureCaptureRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ImageData))
            return BadRequest("imageData is required.");

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(request.ImageData);
        }
        catch (FormatException)
        {
            return BadRequest("imageData must be a valid base64 string.");
        }

        var analysisResult = await _dbContext.AnalysisResults
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (analysisResult is null)
            return NotFound($"No analysis result found with id {id}.");

        analysisResult.SignatureImage = imageBytes;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentsController: signature captured for AnalysisResult.Id={Id} ({Bytes} bytes)",
            id, imageBytes.Length);

        return Ok(new SignatureCaptureResponse { ImageData = request.ImageData });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png"            => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf"            => "application/pdf",
            _                 => "application/octet-stream"
        };
    }
}
