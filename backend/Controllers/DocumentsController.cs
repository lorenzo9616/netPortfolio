using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OcrApi.Data;
using OcrApi.Models;
using OcrApi.Repositories;
using OcrApi.Services;

namespace OcrApi.Controllers;

[Authorize]
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

    /// <summary>
    /// Accepts a multipart upload, runs it through the OCR engine, matches
    /// fields against active OcrProperties, persists the result and returns
    /// the extracted data.
    /// </summary>
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
        // ── Validate file ─────────────────────────────────────────────────────
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        var contentType = file.ContentType?.Trim();
        if (string.IsNullOrEmpty(contentType) || !_allowedContentTypes.Contains(contentType))
        {
            _logger.LogWarning(
                "DocumentsController: unsupported content-type '{ContentType}' rejected",
                contentType);

            return StatusCode(
                StatusCodes.Status415UnsupportedMediaType,
                $"Unsupported file type '{contentType}'. Allowed: image/png, image/jpeg, application/pdf.");
        }

        _logger.LogInformation(
            "DocumentsController: Analyze started — file='{FileName}' size={Size} bytes",
            file.FileName, file.Length);

        // ── Fetch active properties ───────────────────────────────────────────
        var allProperties = await _propertyRepository.GetAllAsync();
        var activeProperties = allProperties.Where(p => p.IsActive).ToList();

        // ── Call OCR engine ───────────────────────────────────────────────────
        ExtractTextResponse ocrResult;
        try
        {
            // Open the stream inside a using block so it is closed immediately
            // after OcrClient finishes reading it.
            await using var fileStream = file.OpenReadStream();

            ocrResult = await _ocrClient.ExtractTextAsync(
                fileStream,
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

        // ── Match fields ──────────────────────────────────────────────────────
        var extractedFields = _fieldMatcher.MatchFields(activeProperties, ocrResult);

        // ── Persist result ────────────────────────────────────────────────────
        var analysisResult = new AnalysisResult
        {
            FileName   = file.FileName,
            RawText    = ocrResult.RawText,
            AnalyzedAt = DateTime.UtcNow,
            PageCount  = ocrResult.PageCount,
            Fields     = extractedFields.Select(f => new SavedField
            {
                PropertyName   = f.PropertyName,
                ExtractedValue = f.ExtractedValue,
                Confidence     = f.Confidence
            }).ToList()
        };

        _dbContext.AnalysisResults.Add(analysisResult);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentsController: Analyze complete — AnalysisResult.Id={Id}, {FieldCount} field(s) saved",
            analysisResult.Id, analysisResult.Fields.Count);

        // ── Return response ───────────────────────────────────────────────────
        var response = new AnalyzeDocumentResponse
        {
            DocumentId      = analysisResult.Id,
            RawText         = ocrResult.RawText,
            ExtractedFields = extractedFields
        };

        return Ok(response);
    }

    // ── GET /api/documents/{id} ──────────────────────────────────────────────

    /// <summary>
    /// Returns the full analysis result (fields + raw text + metadata) for a
    /// previously processed document.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AnalysisResultDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var analysisResult = await _dbContext.AnalysisResults
            .Include(r => r.Fields)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (analysisResult is null)
        {
            return NotFound($"No analysis result found with id {id}.");
        }

        var dto = new AnalysisResultDetailDto
        {
            DocumentId = analysisResult.Id,
            FileName   = analysisResult.FileName,
            RawText    = analysisResult.RawText ?? string.Empty,
            AnalyzedAt = analysisResult.AnalyzedAt,
            ExtractedFields = analysisResult.Fields.Select(f => new ExtractedFieldDto
            {
                PropertyName   = f.PropertyName,
                ExtractedValue = f.ExtractedValue,
                ManualOverride = f.ManualOverride,
                Confidence     = f.Confidence,
            }).ToList(),
        };

        return Ok(dto);
    }

    // ── GET /api/documents ───────────────────────────────────────────────────────

    /// <summary>Returns a paginated list of past analysis results, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DocumentHistoryPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDocuments(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await _dbContext.AnalysisResults.CountAsync(ct);

        var results = await _dbContext.AnalysisResults
            .Include(r => r.Fields)
            .OrderByDescending(r => r.AnalyzedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = results.Select(r => new DocumentHistoryItemDto
        {
            DocumentId = r.Id,
            CreatedAt  = r.AnalyzedAt,
            PageCount  = r.PageCount,
            FieldCount = r.Fields.Count,
            Preview    = BuildPreview(r.RawText),
        }).ToList();

        return Ok(new DocumentHistoryPageDto
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        });
    }

    private static string BuildPreview(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        if (rawText.Length <= 120) return rawText;

        var truncated = rawText[..120];
        var lastSpace = truncated.LastIndexOf(' ');
        return lastSpace > 0
            ? truncated[..lastSpace] + "..."
            : truncated + "...";
    }

    // ── DELETE /api/documents/{id} ───────────────────────────────────────────────

    /// <summary>Deletes an analysis result and all its associated saved fields (cascade).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var analysisResult = await _dbContext.AnalysisResults
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (analysisResult is null)
            return NotFound($"No analysis result found with id {id}.");

        _dbContext.AnalysisResults.Remove(analysisResult);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted AnalysisResult.Id={Id}", id);
        return NoContent();
    }

    // ── PUT /api/documents/{id}/fields ───────────────────────────────────────

    /// <summary>
    /// Applies user-supplied manual overrides to the saved fields of an
    /// existing <see cref="AnalysisResult"/>.
    /// </summary>
    [HttpPut("{id:int}/fields")]
    [ProducesResponseType(typeof(List<SavedField>), StatusCodes.Status200OK)]
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
        {
            return NotFound($"No analysis result found with id {id}.");
        }

        // Apply overrides to matching fields.
        foreach (var update in updates)
        {
            var field = analysisResult.Fields
                .FirstOrDefault(f => string.Equals(
                    f.PropertyName, update.PropertyName,
                    StringComparison.OrdinalIgnoreCase));

            if (field is not null)
            {
                field.ManualOverride = update.ManualOverride;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentsController: UpdateFields complete — AnalysisResult.Id={Id}", id);

        return Ok(analysisResult.Fields);
    }
}
