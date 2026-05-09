using System.Text.Json;
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

    private static readonly JsonSerializerOptions _snakeCaseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IOcrClient _ocrClient;
    private readonly IFieldMatcher _fieldMatcher;
    private readonly IDocumentClassifierService _classifierService;
    private readonly IOcrPropertyRepository _propertyRepository;
    private readonly OcrDbContext _dbContext;
    private readonly ILogger<DocumentsController> _logger;
    private readonly IHandwritingExtractorService _handwritingService;

    public DocumentsController(
        IOcrClient ocrClient,
        IFieldMatcher fieldMatcher,
        IDocumentClassifierService classifierService,
        IOcrPropertyRepository propertyRepository,
        OcrDbContext dbContext,
        ILogger<DocumentsController> logger,
        IHandwritingExtractorService handwritingService)
    {
        _ocrClient          = ocrClient;
        _fieldMatcher       = fieldMatcher;
        _classifierService  = classifierService;
        _propertyRepository = propertyRepository;
        _dbContext          = dbContext;
        _logger             = logger;
        _handwritingService = handwritingService;
    }

    // ── GET /api/documents ───────────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(typeof(List<DocumentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var summaries = await _dbContext.AnalysisResults
            .OrderByDescending(r => r.AnalyzedAt)
            .Select(r => new DocumentSummaryDto
            {
                Id           = r.Id,
                FileName     = r.FileName,
                AnalyzedAt   = r.AnalyzedAt,
                DocumentType = r.DocumentType,
                FieldCount   = r.Fields.Count,
                PageCount    = r.Pages.Count,
                Fields       = r.Fields
                    .Where(f => f.ExtractedValue != null && f.ExtractedValue != "")
                    .OrderBy(f => f.PropertyName)
                    .Select(f => new DocumentFieldSummaryDto
                    {
                        PropertyName   = f.PropertyName,
                        ExtractedValue = f.ExtractedValue,
                        Confidence     = f.Confidence,
                    })
                    .ToList(),
            })
            .ToListAsync(ct);

        return Ok(summaries);
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
            "DocumentsController: Analyze started — file='{FileName}' size={Size} bytes lang='{Lang}'",
            file.FileName, file.Length, request.Lang);

        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        var dbProperties     = await _propertyRepository.GetAllAsync();
        var activeProperties = dbProperties.Where(p => p.IsActive).ToList();

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
                request.Lang,
                ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "DocumentsController: OCR service failure for '{FileName}'", file.FileName);
            return StatusCode(StatusCodes.Status502BadGateway,
                "The OCR service is unavailable or returned an error. Please try again later.");
        }

        var classification  = await _classifierService.ClassifyAsync(ocrResult.RawText, ct);
        var existingNames   = new HashSet<string>(activeProperties.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var tempProperties  = classification.SuggestedProperties
            .Where(sp => !existingNames.Contains(sp.Name))
            .Select(sp => new OcrProperty
            {
                Id              = 0,
                Name            = sp.Name,
                DataType        = sp.DataType,
                SearchHeuristic = sp.SearchHeuristic,
                IsRegex         = sp.IsRegex,
                IsActive        = true,
                CreatedAt       = DateTime.UtcNow,
                UpdatedAt       = DateTime.UtcNow,
            });
        var allProperties   = activeProperties.Concat(tempProperties);
        var extractedFields = _fieldMatcher.MatchFields(allProperties, ocrResult);

        var analysisResult = new AnalysisResult
        {
            FileName     = file.FileName,
            RawText      = ocrResult.RawText,
            AnalyzedAt   = DateTime.UtcNow,
            ImageBytes   = fileBytes,
            DocumentType = classification.DocumentType,
            Fields         = extractedFields.Select(f => new SavedField
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
            }).ToList(),
        };

        if (!string.IsNullOrEmpty(ocrResult.SignatureImage))
        {
            try
            {
                analysisResult.Signatures.Add(new DocumentSignature
                {
                    ImageData  = Convert.FromBase64String(ocrResult.SignatureImage),
                    Label      = "Auto-detected",
                    CapturedAt = DateTime.UtcNow,
                });
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "DocumentsController: auto-detected signature base64 was invalid — skipping");
            }
        }

        for (int i = 0; i < ocrResult.PageImages.Count; i++)
        {
            if (!string.IsNullOrEmpty(ocrResult.PageImages[i]))
            {
                analysisResult.Pages.Add(new DocumentPage
                {
                    PageNumber = i + 1,
                    ImageBytes = Convert.FromBase64String(ocrResult.PageImages[i])
                });
            }
        }

        if (ocrResult.TableBlocks.Count > 0)
            analysisResult.TableBlocksJson = JsonSerializer.Serialize(ocrResult.TableBlocks);

        _dbContext.AnalysisResults.Add(analysisResult);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentsController: Analyze complete — AnalysisResult.Id={Id}, {FieldCount} field(s), {BlockCount} token(s), {PageCount} page image(s) saved",
            analysisResult.Id, analysisResult.Fields.Count, analysisResult.TextBlocks.Count, analysisResult.Pages.Count);

        return Ok(new AnalyzeDocumentResponse
        {
            DocumentId      = analysisResult.Id,
            RawText         = ocrResult.RawText,
            ExtractedFields = extractedFields
        });
    }

    // ── POST /api/documents/analyze/stream ──────────────────────────────────

    [HttpPost("analyze/stream")]
    [EnableRateLimiting("ocr-policy")]
    [Consumes("multipart/form-data")]
    public async Task AnalyzeStream(
        IFormFile file,
        [FromForm] AnalyzeDocumentRequest request,
        CancellationToken ct)
    {
        Response.ContentType             = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"]      = "no-cache";
        Response.Headers["X-Accel-Buffering"]  = "no";

        async Task WriteEventAsync(string eventType, object data)
        {
            var payload = $"event: {eventType}\ndata: {JsonSerializer.Serialize(data)}\n\n";
            var bytes   = System.Text.Encoding.UTF8.GetBytes(payload);
            await Response.Body.WriteAsync(bytes, ct);
            await Response.Body.FlushAsync(ct);
        }

        try
        {
            if (file is null || file.Length == 0)
            {
                await WriteEventAsync("error", new { message = "A non-empty file is required." });
                return;
            }

            var contentType = file.ContentType?.Trim();
            if (string.IsNullOrEmpty(contentType) || !_allowedContentTypes.Contains(contentType))
            {
                await WriteEventAsync("error", new { message = $"Unsupported file type '{contentType}'." });
                return;
            }

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                fileBytes = ms.ToArray();
            }

            var dbProperties     = await _propertyRepository.GetAllAsync();
            var activeProperties = dbProperties.Where(p => p.IsActive).ToList();

            // ── Handwriting / ICR path ───────────────────────────────────────────
            if (string.Equals(request.Mode, "handwriting", StringComparison.OrdinalIgnoreCase))
            {
                await WriteEventAsync("status", new { step = "handwriting", message = "Sending pages to Claude Vision..." });

                var hwResult = await _handwritingService.ExtractAsync(fileBytes, file.FileName, contentType, ct);

                await WriteEventAsync("status", new { step = "handwriting", message = $"Transcription complete — {hwResult.PageCount} page(s)" });
                await WriteEventAsync("status", new { step = "classifying", message = "Classifying document type with AI..." });

                var classification = await _classifierService.ClassifyAsync(hwResult.RawText, ct);

                var existingNames  = new HashSet<string>(activeProperties.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                var tempProperties = classification.SuggestedProperties
                    .Where(sp => !existingNames.Contains(sp.Name))
                    .Select(sp => new OcrProperty
                    {
                        Id              = 0,
                        Name            = sp.Name,
                        DataType        = sp.DataType,
                        SearchHeuristic = sp.SearchHeuristic,
                        IsRegex         = sp.IsRegex,
                        IsActive        = true,
                        CreatedAt       = DateTime.UtcNow,
                        UpdatedAt       = DateTime.UtcNow,
                    })
                    .ToList();

                if (classification.DocumentType != "Unknown" || tempProperties.Count > 0)
                {
                    var typeLabel = classification.DocumentType == "Unknown"
                        ? "Document type detected"
                        : $"{classification.DocumentType} detected";
                    var suffix = tempProperties.Count > 0
                        ? $" — adding {tempProperties.Count} additional {(tempProperties.Count == 1 ? "field" : "fields")}"
                        : string.Empty;
                    await WriteEventAsync("status", new { step = "type_detected", message = $"{typeLabel}{suffix}" });
                }

                await WriteEventAsync("status", new { step = "saving", message = "Saving results to database" });

                var allProperties   = activeProperties.Concat(tempProperties);
                var extractedFields = _fieldMatcher.MatchFields(allProperties, hwResult.RawText);

                var analysisResult = new AnalysisResult
                {
                    FileName     = file.FileName,
                    RawText      = hwResult.RawText,
                    AnalyzedAt   = DateTime.UtcNow,
                    ImageBytes   = fileBytes,
                    DocumentType = classification.DocumentType,
                    Fields       = extractedFields.Select(f => new SavedField
                    {
                        PropertyName   = f.PropertyName,
                        ExtractedValue = f.ExtractedValue,
                        Confidence     = f.Confidence,
                    }).ToList(),
                    TextBlocks = [],
                };

                for (int i = 0; i < hwResult.PageImages.Count; i++)
                {
                    if (!string.IsNullOrEmpty(hwResult.PageImages[i]))
                    {
                        analysisResult.Pages.Add(new DocumentPage
                        {
                            PageNumber = i + 1,
                            ImageBytes = Convert.FromBase64String(hwResult.PageImages[i]),
                        });
                    }
                }

                _dbContext.AnalysisResults.Add(analysisResult);
                await _dbContext.SaveChangesAsync(ct);

                _logger.LogInformation("AnalyzeStream[handwriting]: saved AnalysisResult.Id={Id}", analysisResult.Id);

                await WriteEventAsync("done", new { documentId = analysisResult.Id });
                return;
            }
            // ── End handwriting path — fall through to Tesseract OCR path below ──

            using var ocrResponse = await _ocrClient.ExtractTextStreamAsync(
                new MemoryStream(fileBytes),
                file.FileName,
                contentType,
                request.CropX, request.CropY, request.CropWidth, request.CropHeight,
                request.Lang,
                ct);

            if (!ocrResponse.IsSuccessStatusCode)
            {
                await WriteEventAsync("error", new { message = "The OCR service returned an error." });
                return;
            }

            using var bodyStream = await ocrResponse.Content.ReadAsStreamAsync(ct);
            using var reader     = new System.IO.StreamReader(bodyStream);

            string? currentEvent = null;
            string? currentData  = null;

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line is null) break;

                if (line.StartsWith("event: ", StringComparison.Ordinal))
                    currentEvent = line[7..].Trim();
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                    currentData = line[6..].Trim();
                else if (line.Length == 0 && currentEvent is not null && currentData is not null)
                {
                    if (currentEvent is "status" or "error")
                    {
                        var raw   = $"event: {currentEvent}\ndata: {currentData}\n\n";
                        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
                        await Response.Body.WriteAsync(bytes, ct);
                        await Response.Body.FlushAsync(ct);
                    }
                    else if (currentEvent == "complete")
                    {
                        var ocrResult = JsonSerializer.Deserialize<ExtractTextResponse>(currentData, _snakeCaseOptions);

                        if (ocrResult is not null)
                        {
                            await WriteEventAsync("status", new { step = "classifying", message = "Classifying document type with AI..." });

                            var classification = await _classifierService.ClassifyAsync(ocrResult.RawText, ct);
                            var existingNames  = new HashSet<string>(activeProperties.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                            var tempProperties = classification.SuggestedProperties
                                .Where(sp => !existingNames.Contains(sp.Name))
                                .Select(sp => new OcrProperty
                                {
                                    Id              = 0,
                                    Name            = sp.Name,
                                    DataType        = sp.DataType,
                                    SearchHeuristic = sp.SearchHeuristic,
                                    IsRegex         = sp.IsRegex,
                                    IsActive        = true,
                                    CreatedAt       = DateTime.UtcNow,
                                    UpdatedAt       = DateTime.UtcNow,
                                })
                                .ToList();

                            if (classification.DocumentType != "Unknown" || tempProperties.Count > 0)
                            {
                                var typeLabel = classification.DocumentType == "Unknown"
                                    ? "Document type detected"
                                    : $"{classification.DocumentType} detected";
                                var suffix = tempProperties.Count > 0
                                    ? $" — adding {tempProperties.Count} additional {(tempProperties.Count == 1 ? "field" : "fields")}"
                                    : string.Empty;
                                await WriteEventAsync("status", new { step = "type_detected", message = $"{typeLabel}{suffix}" });
                            }

                            await WriteEventAsync("status", new { step = "saving", message = "Saving results to database" });

                            var allProperties   = activeProperties.Concat(tempProperties);
                            var extractedFields = _fieldMatcher.MatchFields(allProperties, ocrResult);

                            var analysisResult = new AnalysisResult
                            {
                                FileName     = file.FileName,
                                RawText      = ocrResult.RawText,
                                AnalyzedAt   = DateTime.UtcNow,
                                ImageBytes   = fileBytes,
                                DocumentType = classification.DocumentType,
                                Fields         = extractedFields.Select(f => new SavedField
                                {
                                    PropertyName   = f.PropertyName,
                                    ExtractedValue = f.ExtractedValue,
                                    Confidence     = f.Confidence,
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
                                }).ToList(),
                            };

                            if (!string.IsNullOrEmpty(ocrResult.SignatureImage))
                            {
                                try
                                {
                                    analysisResult.Signatures.Add(new DocumentSignature
                                    {
                                        ImageData  = Convert.FromBase64String(ocrResult.SignatureImage),
                                        Label      = "Auto-detected",
                                        CapturedAt = DateTime.UtcNow,
                                    });
                                }
                                catch (FormatException ex)
                                {
                                    _logger.LogWarning(ex, "AnalyzeStream: invalid signature base64 — skipping");
                                }
                            }

                            for (int i = 0; i < ocrResult.PageImages.Count; i++)
                            {
                                if (!string.IsNullOrEmpty(ocrResult.PageImages[i]))
                                {
                                    analysisResult.Pages.Add(new DocumentPage
                                    {
                                        PageNumber = i + 1,
                                        ImageBytes = Convert.FromBase64String(ocrResult.PageImages[i]),
                                    });
                                }
                            }

                            if (ocrResult.TableBlocks.Count > 0)
                                analysisResult.TableBlocksJson = JsonSerializer.Serialize(ocrResult.TableBlocks);

                            _dbContext.AnalysisResults.Add(analysisResult);
                            await _dbContext.SaveChangesAsync(ct);

                            _logger.LogInformation(
                                "AnalyzeStream: saved AnalysisResult.Id={Id}", analysisResult.Id);

                            await WriteEventAsync("done", new { documentId = analysisResult.Id });
                        }
                    }

                    currentEvent = null;
                    currentData  = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalyzeStream: unexpected error");
            try { await WriteEventAsync("error", new { message = "An unexpected error occurred." }); }
            catch { /* response may already be closed */ }
        }
    }

    // ── GET /api/documents/{id} ──────────────────────────────────────────────

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AnalysisResultDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var raw = await _dbContext.AnalysisResults
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.FileName,
                r.RawText,
                r.AnalyzedAt,
                r.TableBlocksJson,
                r.DocumentType,
                PageCount = r.Pages.Count,
                Signatures = r.Signatures.Select(s => new SignatureDto
                {
                    Id         = s.Id,
                    ImageData  = Convert.ToBase64String(s.ImageData),
                    Label      = s.Label,
                    CapturedAt = s.CapturedAt,
                }).ToList(),
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

        var tableBlocks = string.IsNullOrEmpty(raw.TableBlocksJson)
            ? new List<TableBlockDto>()
            : (JsonSerializer.Deserialize<List<OcrTableBlock>>(raw.TableBlocksJson, _snakeCaseOptions)
                   ?.Select(tb => new TableBlockDto
                   {
                       Page  = tb.Page,
                       Rows  = tb.Rows,
                       Cols  = tb.Cols,
                       Cells = tb.Cells.Select(c => new TableCellDto
                       {
                           Row  = c.Row,
                           Col  = c.Col,
                           Text = c.Text,
                       }).ToList(),
                   }).ToList() ?? new List<TableBlockDto>());

        var dto = new AnalysisResultDetailDto
        {
            DocumentId      = raw.Id,
            FileName        = raw.FileName,
            RawText         = raw.RawText,
            AnalyzedAt      = raw.AnalyzedAt,
            DocumentType    = raw.DocumentType,
            PageCount       = raw.PageCount,
            Signatures      = raw.Signatures,
            ExtractedFields = raw.Fields,
            TextBlocks      = raw.TextBlocks,
            TableBlocks     = tableBlocks,
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

    // ── GET /api/documents/{id}/image/{page} ─────────────────────────────────

    [HttpGet("{id:int}/image/{page:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPageImage(int id, int page, CancellationToken ct)
    {
        var docPage = await _dbContext.DocumentPages
            .Where(p => p.AnalysisResultId == id && p.PageNumber == page)
            .Select(p => new { p.ImageBytes })
            .FirstOrDefaultAsync(ct);

        if (docPage is null)
            return NotFound($"Page {page} not found for document {id}.");

        return File(docPage.ImageBytes, "image/jpeg");
    }

    // ── POST /api/documents/{id}/signatures ──────────────────────────────────

    [HttpPost("{id:int}/signatures")]
    [ProducesResponseType(typeof(SignatureDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddSignature(
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

        var exists = await _dbContext.AnalysisResults
            .AnyAsync(r => r.Id == id, ct);

        if (!exists)
            return NotFound($"No analysis result found with id {id}.");

        var sig = new DocumentSignature
        {
            AnalysisResultId = id,
            ImageData        = imageBytes,
            Label            = request.Label,
            CapturedAt       = DateTime.UtcNow,
        };

        _dbContext.DocumentSignatures.Add(sig);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentsController: signature added Id={SigId} for AnalysisResult.Id={DocId} ({Bytes} bytes)",
            sig.Id, id, imageBytes.Length);

        return CreatedAtAction(nameof(GetById), new { id }, new SignatureDto
        {
            Id         = sig.Id,
            ImageData  = request.ImageData,
            Label      = sig.Label,
            CapturedAt = sig.CapturedAt,
        });
    }

    // ── DELETE /api/documents/{id}/signatures/{sigId} ────────────────────────

    [HttpDelete("{id:int}/signatures/{sigId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSignature(int id, int sigId, CancellationToken ct)
    {
        var sig = await _dbContext.DocumentSignatures
            .FirstOrDefaultAsync(s => s.Id == sigId && s.AnalysisResultId == id, ct);

        if (sig is null)
            return NotFound($"Signature {sigId} not found for document {id}.");

        _dbContext.DocumentSignatures.Remove(sig);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DocumentsController: signature Id={SigId} deleted from AnalysisResult.Id={DocId}", sigId, id);

        return NoContent();
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
