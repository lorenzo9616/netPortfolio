# Signature Capture, OCR Text Table & Save Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Store uploaded document images server-side, let users crop a signature region on the results page, display a structured OCR token table, and fix the circular-reference crash in Save Changes.

**Architecture:** Image bytes are stored as `bytea` in PostgreSQL alongside each `AnalysisResult`. A new `saved_text_blocks` table persists every Tesseract word token. The results page fetches the image, overlays an interactive canvas for signature selection, and shows a token table — all within the existing Next.js + .NET 8 + PostgreSQL stack.

**Tech Stack:** .NET 8 / EF Core / Npgsql, Next.js 14 (React 18, TypeScript), Tailwind CSS, PostgreSQL 16, `dotnet ef` CLI

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `backend/Models/SavedTextBlock.cs` | DB entity for Tesseract word tokens |
| Modify | `backend/Models/AnalysisResult.cs` | Add ImageBytes, SignatureImage, TextBlocks nav |
| Modify | `backend/Models/DocumentModels.cs` | Add TextBlockDto, SignatureCaptureRequest/Response; extend AnalysisResultDetailDto |
| Modify | `backend/Data/OcrDbContext.cs` | Map SavedTextBlock table; add new AnalysisResult columns |
| Generate | `backend/Migrations/<ts>_AddImageStorageAndTextBlocks.cs` | EF migration |
| Modify | `backend/Controllers/DocumentsController.cs` | Fix UpdateFields; extend Analyze + GetById; add GetImage + PatchSignature |
| Modify | `frontend/src/types/ocr.ts` | Add OcrTextBlock interface; extend AnalysisResultDetail |
| Modify | `frontend/src/lib/api.ts` | Add captureSignature() |
| Create | `frontend/src/components/results/SignatureCanvas.tsx` | Canvas overlay + signature crop component |
| Create | `frontend/src/components/results/OcrTokensTable.tsx` | Token table component |
| Modify | `frontend/src/components/results/ResultsClient.tsx` | Wire in new components; Signature row shows image |

---

## Task 1: Create SavedTextBlock entity

**Files:**
- Create: `backend/Models/SavedTextBlock.cs`

- [ ] **Step 1: Create the entity**

```csharp
// backend/Models/SavedTextBlock.cs
namespace OcrApi.Models;

public class SavedTextBlock
{
    public int Id { get; set; }
    public int AnalysisResultId { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int Page { get; set; }
    public int BboxX { get; set; }
    public int BboxY { get; set; }
    public int BboxWidth { get; set; }
    public int BboxHeight { get; set; }

    public AnalysisResult AnalysisResult { get; set; } = null!;
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Models/SavedTextBlock.cs
git commit -m "feat: add SavedTextBlock entity for OCR token storage"
```

---

## Task 2: Extend AnalysisResult model

**Files:**
- Modify: `backend/Models/AnalysisResult.cs`

- [ ] **Step 1: Add new properties to AnalysisResult**

Replace the entire file content:

```csharp
// backend/Models/AnalysisResult.cs
namespace OcrApi.Models;

public class AnalysisResult
{
    public int Id { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string FileName { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public byte[]? ImageBytes { get; set; }
    public byte[]? SignatureImage { get; set; }
    public List<SavedField> Fields { get; set; } = new();
    public List<SavedTextBlock> TextBlocks { get; set; } = new();
}

public class SavedField
{
    public int Id { get; set; }
    public int AnalysisResultId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public string? ManualOverride { get; set; }
    public double Confidence { get; set; }

    public AnalysisResult AnalysisResult { get; set; } = null!;
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Models/AnalysisResult.cs
git commit -m "feat: add ImageBytes, SignatureImage and TextBlocks to AnalysisResult"
```

---

## Task 3: Add new DTOs to DocumentModels.cs

**Files:**
- Modify: `backend/Models/DocumentModels.cs`

- [ ] **Step 1: Add TextBlockDto, SignatureCaptureRequest/Response; extend AnalysisResultDetailDto**

Replace the entire file content:

```csharp
// backend/Models/DocumentModels.cs
namespace OcrApi.Models;

// ── Analyze request (form fields from Next.js) ───────────────────────────────

public class AnalyzeDocumentRequest
{
    public int? CropX { get; set; }
    public int? CropY { get; set; }
    public int? CropWidth { get; set; }
    public int? CropHeight { get; set; }
}

// ── OCR engine response types (deserialized from Python service) ─────────────

public class OcrTextBlock
{
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int Page { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
}

public class BoundingBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class ExtractTextResponse
{
    public bool Success { get; set; }
    public int PageCount { get; set; }
    public List<OcrTextBlock> TextBlocks { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
    public float ProcessingTimeMs { get; set; }
}

// ── Field extraction types ────────────────────────────────────────────────────

public class ExtractedField
{
    public string PropertyName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public double Confidence { get; set; }
}

public class AnalyzeDocumentResponse
{
    public int DocumentId { get; set; }
    public List<ExtractedField> ExtractedFields { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
}

// ── PUT /api/documents/{id}/fields body ──────────────────────────────────────

public class SavedFieldUpdateDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string? ManualOverride { get; set; }
}

// ── Text block DTO (returned to frontend) ────────────────────────────────────

public class TextBlockDto
{
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int Page { get; set; }
    public int BboxX { get; set; }
    public int BboxY { get; set; }
    public int BboxWidth { get; set; }
    public int BboxHeight { get; set; }
}

// ── GET /api/documents/{id} response ─────────────────────────────────────────

public class AnalysisResultDetailDto
{
    public int DocumentId { get; set; }
    public List<ExtractedFieldDto> ExtractedFields { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public List<TextBlockDto> TextBlocks { get; set; } = new();
    public string? SignatureImage { get; set; }
}

public class ExtractedFieldDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public string? ManualOverride { get; set; }
    public double Confidence { get; set; }
}

// ── PATCH /api/documents/{id}/signature ──────────────────────────────────────

public class SignatureCaptureRequest
{
    public string ImageData { get; set; } = string.Empty;
}

public class SignatureCaptureResponse
{
    public string ImageData { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Models/DocumentModels.cs
git commit -m "feat: add TextBlockDto, SignatureCaptureRequest/Response; extend AnalysisResultDetailDto"
```

---

## Task 4: Update OcrDbContext

**Files:**
- Modify: `backend/Data/OcrDbContext.cs`

- [ ] **Step 1: Add SavedTextBlock DbSet and table mapping; add new AnalysisResult columns**

Replace the entire file content:

```csharp
// backend/Data/OcrDbContext.cs
using Microsoft.EntityFrameworkCore;
using OcrApi.Models;

namespace OcrApi.Data;

public class OcrDbContext : DbContext
{
    public OcrDbContext(DbContextOptions<OcrDbContext> options) : base(options) { }

    public DbSet<OcrProperty>    OcrProperties   => Set<OcrProperty>();
    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();
    public DbSet<SavedField>     SavedFields      => Set<SavedField>();
    public DbSet<SavedTextBlock> SavedTextBlocks  => Set<SavedTextBlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OcrProperty>(entity =>
        {
            entity.ToTable("ocr_properties");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DataType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SearchHeuristic).IsRequired(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.ToTable("analysis_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.RawText).IsRequired(false);
            entity.Property(e => e.AnalyzedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.ImageBytes).IsRequired(false);
            entity.Property(e => e.SignatureImage).IsRequired(false);

            entity.HasMany(e => e.Fields)
                  .WithOne(f => f.AnalysisResult)
                  .HasForeignKey(f => f.AnalysisResultId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TextBlocks)
                  .WithOne(b => b.AnalysisResult)
                  .HasForeignKey(b => b.AnalysisResultId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SavedField>(entity =>
        {
            entity.ToTable("saved_fields");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PropertyName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ExtractedValue).IsRequired(false);
            entity.Property(e => e.ManualOverride).IsRequired(false);
        });

        modelBuilder.Entity<SavedTextBlock>(entity =>
        {
            entity.ToTable("saved_text_blocks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<OcrProperty>().HasData(
            new OcrProperty
            {
                Id = 1, Name = "Signature", DataType = "string",
                SearchHeuristic = null, IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id = 2, Name = "FullName", DataType = "string",
                SearchHeuristic = null, IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id = 3, Name = "DateOfBirth", DataType = "date",
                SearchHeuristic = @"\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b",
                IsRegex = true, IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id = 4, Name = "BillingTotal", DataType = "decimal",
                SearchHeuristic = @"\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?",
                IsRegex = true, IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new OcrProperty
            {
                Id = 5, Name = "ProcessingFee", DataType = "decimal",
                SearchHeuristic = @"\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?",
                IsRegex = true, IsActive = true,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Data/OcrDbContext.cs
git commit -m "feat: add saved_text_blocks table and image columns to OcrDbContext"
```

---

## Task 5: Generate EF migration

**Files:**
- Generate: `backend/Migrations/` (new migration files)

- [ ] **Step 1: Run migration from the backend directory**

```bash
cd backend
dotnet ef migrations add AddImageStorageAndTextBlocks
```

Expected output (last line):
```
Done. To undo this action, use 'ef migrations remove'
```

Two new files appear: `Migrations/<timestamp>_AddImageStorageAndTextBlocks.cs` and an updated `OcrDbContextModelSnapshot.cs`.

- [ ] **Step 2: Open the generated migration file and verify it contains**

Look for these three operations in the `Up()` method:
1. `AddColumn` for `image_bytes` (bytea/nullable)
2. `AddColumn` for `signature_image` (bytea/nullable)
3. `CreateTable` for `saved_text_blocks` with all expected columns

- [ ] **Step 3: Commit**

```bash
cd ..
git add backend/Migrations/
git commit -m "feat: migration AddImageStorageAndTextBlocks"
```

---

## Task 6: Fix UpdateFields circular reference

**Files:**
- Modify: `backend/Controllers/DocumentsController.cs` — `UpdateFields` method only

- [ ] **Step 1: Replace the return statement in UpdateFields**

Find the `UpdateFields` method (currently ends with `return Ok(analysisResult.Fields);`) and replace it. The full updated method body:

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add backend/Controllers/DocumentsController.cs
git commit -m "fix: map SavedField to DTO in UpdateFields to resolve circular reference"
```

---

## Task 7: Extend Analyze endpoint (buffer bytes + store image and text blocks)

**Files:**
- Modify: `backend/Controllers/DocumentsController.cs` — `Analyze` method only

- [ ] **Step 1: Replace the Analyze method**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add backend/Controllers/DocumentsController.cs
git commit -m "feat: buffer file bytes; store ImageBytes and SavedTextBlocks on analyze"
```

---

## Task 8: Extend GetById to return TextBlocks and SignatureImage

**Files:**
- Modify: `backend/Controllers/DocumentsController.cs` — `GetById` method only

- [ ] **Step 1: Replace GetById**

`Convert.ToBase64String` cannot run inside an EF LINQ expression (EF cannot translate it to SQL). Use a two-step approach: project everything *except* the large `ImageBytes` column into an anonymous type, then convert the signature bytes to base64 in C#.

```csharp
[HttpGet("{id:int}")]
[ProducesResponseType(typeof(AnalysisResultDetailDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetById(int id, CancellationToken ct)
{
    // Step 1: project to an anonymous type (EF translates this to SQL).
    // ImageBytes is intentionally excluded — it is large and not needed here.
    var raw = await _dbContext.AnalysisResults
        .Where(r => r.Id == id)
        .Select(r => new
        {
            r.Id,
            r.FileName,
            r.RawText,
            r.AnalyzedAt,
            r.SignatureImage,                 // byte[]? — small, OK to load
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

    // Step 2: base64 conversion happens in C#, not in the SQL query.
    var dto = new AnalysisResultDetailDto
    {
        DocumentId      = raw.Id,
        FileName        = raw.FileName,
        RawText         = raw.RawText,
        AnalyzedAt      = raw.AnalyzedAt,
        SignatureImage  = raw.SignatureImage is not null
                            ? Convert.ToBase64String(raw.SignatureImage)
                            : null,
        ExtractedFields = raw.Fields,
        TextBlocks      = raw.TextBlocks,
    };

    return Ok(dto);
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Controllers/DocumentsController.cs
git commit -m "feat: include textBlocks and signatureImage in GetById response"
```

---

## Task 9: Add GetImage and CaptureSignature endpoints + content-type helper

**Files:**
- Modify: `backend/Controllers/DocumentsController.cs` — add two new methods and a private helper

- [ ] **Step 1: Add the private helper and two new endpoints**

Add these three methods to the `DocumentsController` class (after `UpdateFields`):

```csharp
// ── GET /api/documents/{id}/image ────────────────────────────────────────────

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

// ── PATCH /api/documents/{id}/signature ──────────────────────────────────────

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

// ── Helpers ───────────────────────────────────────────────────────────────────

private static string GetContentType(string fileName)
{
    var ext = Path.GetExtension(fileName).ToLowerInvariant();
    return ext switch
    {
        ".png"           => "image/png",
        ".jpg" or ".jpeg"=> "image/jpeg",
        ".pdf"           => "application/pdf",
        _                => "application/octet-stream"
    };
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Controllers/DocumentsController.cs
git commit -m "feat: add GetImage and CaptureSignature endpoints"
```

---

## Task 10: Build and verify the backend compiles

- [ ] **Step 1: Build from the backend directory**

```bash
cd backend
dotnet build
```

Expected output (last lines):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If errors appear, fix them before continuing. Common issues:
- Missing `using System.Linq;` → already covered by global usings in .NET 8

- [ ] **Step 2: Commit if there are any fixes**

```bash
cd ..
git add backend/Controllers/DocumentsController.cs
git commit -m "fix: backend compile error"
```

---

## Task 11: Update frontend types

**Files:**
- Modify: `frontend/src/types/ocr.ts`

- [ ] **Step 1: Add OcrTextBlock interface; extend AnalysisResultDetail**

Replace the entire file:

```ts
// frontend/src/types/ocr.ts
export interface CropRegion {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface UploadState {
  file: File | null;
  previewUrl: string | null;
  imageDimensions: { width: number; height: number } | null;
  cropRegion: CropRegion | null;
  isCapturing: boolean;
}

export interface ExtractedField {
  propertyName: string;
  extractedValue: string | null;
  confidence: number;
}

export interface AnalyzeResponse {
  documentId: number;
  extractedFields: ExtractedField[];
  rawText: string;
}

export interface SavedFieldUpdate {
  propertyName: string;
  manualOverride: string | null;
}

export interface OcrTextBlock {
  text: string;
  confidence: number;
  page: number;
  bboxX: number;
  bboxY: number;
  bboxWidth: number;
  bboxHeight: number;
}

export interface AnalysisResultDetail {
  documentId: number;
  extractedFields: ExtractedField[];
  rawText: string;
  analyzedAt: string;
  fileName: string;
  textBlocks: OcrTextBlock[];
  signatureImage: string | null;
}

export interface OcrProperty {
  id: number;
  name: string;
  dataType: string;
  searchHeuristic: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateOcrPropertyDto {
  name: string;
  dataType: string;
  searchHeuristic?: string | null;
  isActive: boolean;
}

export interface UpdateOcrPropertyDto {
  name?: string;
  dataType?: string;
  searchHeuristic?: string | null;
  isActive?: boolean;
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/types/ocr.ts
git commit -m "feat: add OcrTextBlock type; extend AnalysisResultDetail with textBlocks and signatureImage"
```

---

## Task 12: Update api.ts — add captureSignature

**Files:**
- Modify: `frontend/src/lib/api.ts`

- [ ] **Step 1: Add the captureSignature function**

Add after the existing `saveFieldOverrides` function (before `analyzeDocument`):

```ts
export async function captureSignature(
  documentId: number,
  imageData: string,
): Promise<{ imageData: string }> {
  const res = await fetch(`${API_BASE}/api/documents/${documentId}/signature`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ imageData }),
  });
  return handleResponse<{ imageData: string }>(res);
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/lib/api.ts
git commit -m "feat: add captureSignature API function"
```

---

## Task 13: Create SignatureCanvas component

**Files:**
- Create: `frontend/src/components/results/SignatureCanvas.tsx`

- [ ] **Step 1: Create the component**

```tsx
// frontend/src/components/results/SignatureCanvas.tsx
'use client';

import { useEffect, useRef, useState } from 'react';

interface Rect { x: number; y: number; w: number; h: number }

interface Props {
  imageUrl: string;
  onCapture: (base64Png: string) => void;
  isSaving: boolean;
  captureError: string | null;
}

export default function SignatureCanvas({ imageUrl, onCapture, isSaving, captureError }: Props) {
  const imgRef = useRef<HTMLImageElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [dragStart, setDragStart] = useState<{ x: number; y: number } | null>(null);
  const [selection, setSelection] = useState<Rect | null>(null);
  const [imageLoaded, setImageLoaded] = useState(false);
  const [imageError, setImageError] = useState(false);

  // Sync canvas buffer size to displayed image size whenever the image loads
  useEffect(() => {
    const canvas = canvasRef.current;
    const img = imgRef.current;
    if (!canvas || !img || !imageLoaded) return;
    canvas.width = img.clientWidth;
    canvas.height = img.clientHeight;
  }, [imageLoaded]);

  // Redraw selection rectangle whenever selection changes
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (!selection) return;
    ctx.strokeStyle = '#ef4444';
    ctx.lineWidth = 2;
    ctx.setLineDash([6, 3]);
    ctx.strokeRect(selection.x, selection.y, selection.w, selection.h);
  }, [selection]);

  function getPos(e: React.MouseEvent<HTMLCanvasElement>): { x: number; y: number } {
    const rect = canvasRef.current!.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }

  function toRect(a: { x: number; y: number }, b: { x: number; y: number }): Rect {
    return { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), w: Math.abs(b.x - a.x), h: Math.abs(b.y - a.y) };
  }

  function handleMouseDown(e: React.MouseEvent<HTMLCanvasElement>) {
    setDragStart(getPos(e));
    setSelection(null);
  }

  function handleMouseMove(e: React.MouseEvent<HTMLCanvasElement>) {
    if (!dragStart) return;
    setSelection(toRect(dragStart, getPos(e)));
  }

  function handleMouseUp(e: React.MouseEvent<HTMLCanvasElement>) {
    if (!dragStart) return;
    setSelection(toRect(dragStart, getPos(e)));
    setDragStart(null);
  }

  function handleCapture() {
    const img = imgRef.current;
    if (!img || !selection || selection.w <= 5 || selection.h <= 5) return;

    // Scale from display pixels → natural image pixels
    const scaleX = img.naturalWidth / img.clientWidth;
    const scaleY = img.naturalHeight / img.clientHeight;
    const nx = Math.round(selection.x * scaleX);
    const ny = Math.round(selection.y * scaleY);
    const nw = Math.round(selection.w * scaleX);
    const nh = Math.round(selection.h * scaleY);
    if (nw <= 0 || nh <= 0) return;

    const offscreen = document.createElement('canvas');
    offscreen.width = nw;
    offscreen.height = nh;
    const ctx = offscreen.getContext('2d');
    if (!ctx) return;
    // Draw the full image offset so that the selected region sits at (0,0)
    ctx.drawImage(img, -nx, -ny, img.naturalWidth, img.naturalHeight);
    const base64 = offscreen.toDataURL('image/png').split(',')[1];
    onCapture(base64);
  }

  const hasValidSelection = selection !== null && selection.w > 5 && selection.h > 5;

  if (imageError) {
    return (
      <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-5 text-center">
        <p className="text-xs text-gray-500">Image preview not available.</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-xs text-gray-500">
        Draw a box over the signature area, then click <strong>Capture Signature</strong>.
      </p>

      <div className="relative overflow-hidden rounded-lg border border-gray-200 bg-gray-50">
        {/* crossOrigin="anonymous" required so canvas.drawImage() can read cross-origin pixels */}
        <img
          ref={imgRef}
          src={imageUrl}
          alt="Document preview"
          crossOrigin="anonymous"
          onLoad={() => setImageLoaded(true)}
          onError={() => setImageError(true)}
          className="w-full select-none"
          draggable={false}
        />
        {imageLoaded && (
          <canvas
            ref={canvasRef}
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            className="absolute inset-0 cursor-crosshair"
            style={{ width: '100%', height: '100%' }}
          />
        )}
      </div>

      {hasValidSelection && (
        <button
          type="button"
          onClick={handleCapture}
          disabled={isSaving}
          className="w-full rounded-xl bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2"
        >
          {isSaving ? 'Capturing…' : 'Capture Signature'}
        </button>
      )}

      {captureError && (
        <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-4 py-3">
          <p className="text-sm font-semibold text-red-800">Capture failed</p>
          <p className="mt-0.5 text-xs text-red-700">{captureError}</p>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/components/results/SignatureCanvas.tsx
git commit -m "feat: add SignatureCanvas component for interactive signature region capture"
```

---

## Task 14: Create OcrTokensTable component

**Files:**
- Create: `frontend/src/components/results/OcrTokensTable.tsx`

- [ ] **Step 1: Create the component**

```tsx
// frontend/src/components/results/OcrTokensTable.tsx
import type { OcrTextBlock } from '@/types/ocr';

function ConfidenceBadge({ value }: { value: number }) {
  const pct = Math.round(value);
  const colorClass =
    pct > 80
      ? 'bg-green-100 text-green-800 ring-green-200'
      : pct > 50
      ? 'bg-yellow-100 text-yellow-800 ring-yellow-200'
      : 'bg-red-100 text-red-800 ring-red-200';
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${colorClass}`}>
      {pct}%
    </span>
  );
}

interface Props {
  blocks: OcrTextBlock[];
}

export default function OcrTokensTable({ blocks }: Props) {
  return (
    <section aria-label="Raw OCR tokens">
      <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-gray-500">
        Raw OCR Tokens
      </h2>

      {blocks.length === 0 ? (
        <div className="rounded-xl border border-gray-200 bg-gray-50 px-5 py-6 text-center">
          <p className="text-sm text-gray-600">No OCR tokens found.</p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
          <div className="max-h-96 overflow-y-auto">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="sticky top-0 bg-gray-50">
                <tr className="text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
                  <th className="px-4 py-3">#</th>
                  <th className="px-4 py-3">Text</th>
                  <th className="px-4 py-3">Confidence</th>
                  <th className="px-4 py-3">Page</th>
                  <th className="px-4 py-3">Position</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {blocks.map((b, i) => (
                  <tr key={i} className="align-middle">
                    <td className="px-4 py-2 text-gray-400">{i + 1}</td>
                    <td className="px-4 py-2 font-medium text-gray-800">{b.text}</td>
                    <td className="px-4 py-2">
                      <ConfidenceBadge value={b.confidence} />
                    </td>
                    <td className="px-4 py-2 text-gray-600">{b.page}</td>
                    <td className="px-4 py-2 text-gray-500">
                      ({b.bboxX}, {b.bboxY})
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/components/results/OcrTokensTable.tsx
git commit -m "feat: add OcrTokensTable component"
```

---

## Task 15: Update ResultsClient

**Files:**
- Modify: `frontend/src/components/results/ResultsClient.tsx`

- [ ] **Step 1: Replace the entire file**

```tsx
// frontend/src/components/results/ResultsClient.tsx
'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import type { AnalysisResultDetail, SavedFieldUpdate } from '@/types/ocr';
import { saveFieldOverrides, captureSignature, ApiError } from '@/lib/api';
import SignatureCanvas from './SignatureCanvas';
import OcrTokensTable from './OcrTokensTable';

interface Props {
  result: AnalysisResultDetail;
}

function ConfidenceBadge({ value }: { value: number }) {
  const pct = Math.round(value * 100);
  const colorClass =
    pct > 80
      ? 'bg-green-100 text-green-800 ring-green-200'
      : pct > 50
      ? 'bg-yellow-100 text-yellow-800 ring-yellow-200'
      : 'bg-red-100 text-red-800 ring-red-200';
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${colorClass}`}>
      {pct}%
    </span>
  );
}

export default function ResultsClient({ result }: Props) {
  const [overrides, setOverrides] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {};
    for (const field of result.extractedFields) {
      initial[field.propertyName] = field.extractedValue ?? '';
    }
    return initial;
  });

  const [isSaving, setIsSaving] = useState(false);
  const [saveStatus, setSaveStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [saveError, setSaveError] = useState('');
  const dismissTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const [signatureImage, setSignatureImage] = useState<string | null>(
    result.signatureImage ?? null,
  );
  const [isCapturing, setIsCapturing] = useState(false);
  const [captureError, setCaptureError] = useState<string | null>(null);

  // Compute imageUrl client-side only to avoid SSR/hydration mismatch.
  // API_BASE evaluates differently on server (API_URL = internal Docker hostname)
  // vs browser (NEXT_PUBLIC_API_URL). Using state + useEffect pins this to the
  // browser value after hydration.
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  useEffect(() => {
    const base = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000';
    setImageUrl(`${base}/api/documents/${result.documentId}/image`);
  }, [result.documentId]);

  useEffect(() => {
    if (saveStatus !== 'success') return;
    dismissTimerRef.current = setTimeout(() => setSaveStatus('idle'), 3000);
    return () => {
      if (dismissTimerRef.current !== null) clearTimeout(dismissTimerRef.current);
    };
  }, [saveStatus]);

  function handleOverrideChange(propertyName: string, value: string) {
    setOverrides((prev) => ({ ...prev, [propertyName]: value }));
  }

  async function handleSave() {
    setIsSaving(true);
    setSaveStatus('idle');
    setSaveError('');
    const payload: SavedFieldUpdate[] = result.extractedFields.map((f) => ({
      propertyName: f.propertyName,
      manualOverride: overrides[f.propertyName] ?? null,
    }));
    try {
      await saveFieldOverrides(result.documentId, payload);
      setSaveStatus('success');
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message
        : err instanceof Error ? err.message
        : 'An unexpected error occurred while saving.';
      setSaveError(message);
      setSaveStatus('error');
    } finally {
      setIsSaving(false);
    }
  }

  async function handleCapture(base64Png: string) {
    setIsCapturing(true);
    setCaptureError(null);
    try {
      const resp = await captureSignature(result.documentId, base64Png);
      setSignatureImage(resp.imageData);
    } catch (err) {
      setCaptureError(
        err instanceof ApiError ? err.message
        : err instanceof Error ? err.message
        : 'Capture failed.',
      );
    } finally {
      setIsCapturing(false);
    }
  }

  const analyzedDate = new Date(result.analyzedAt).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });


  return (
    <div className="flex flex-col gap-8">
      {/* ── Top row: extracted fields (left) + document image (right) ─────── */}
      <div className="grid grid-cols-1 gap-8 lg:grid-cols-2">

        {/* ── Extracted fields ──────────────────────────────────────────────── */}
        <section aria-label="Extracted fields">
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-gray-500">
            Extracted Fields
          </h2>

          {result.extractedFields.length === 0 ? (
            <div className="rounded-xl border border-gray-200 bg-gray-50 px-5 py-6 text-center">
              <p className="text-sm text-gray-600">
                No fields were extracted. Try adjusting your OCR properties.
              </p>
            </div>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
              <table className="min-w-full divide-y divide-gray-100 text-sm">
                <thead>
                  <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
                    <th className="px-4 py-3">Field Name</th>
                    <th className="px-4 py-3">Extracted Value</th>
                    <th className="px-4 py-3">Manual Override</th>
                    <th className="px-4 py-3 text-right">Confidence</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {result.extractedFields.map((field) => (
                    <tr key={field.propertyName} className="align-middle">
                      <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-800">
                        {field.propertyName}
                      </td>
                      <td className="px-4 py-3 text-gray-600">
                        {field.extractedValue ?? <span className="italic text-gray-400">—</span>}
                      </td>
                      <td className="px-4 py-3">
                        {field.propertyName === 'Signature' && signatureImage ? (
                          <img
                            src={`data:image/png;base64,${signatureImage}`}
                            alt="Captured signature"
                            className="max-h-16 rounded border border-gray-200"
                          />
                        ) : (
                          <input
                            type="text"
                            aria-label={`Manual override for ${field.propertyName}`}
                            value={overrides[field.propertyName] ?? ''}
                            onChange={(e) => handleOverrideChange(field.propertyName, e.target.value)}
                            disabled={isSaving}
                            className="w-full rounded-md border border-gray-300 bg-white px-2 py-1 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:cursor-not-allowed disabled:bg-gray-50"
                            placeholder="Enter override…"
                          />
                        )}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <ConfidenceBadge value={field.confidence} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Save button & banners */}
          <div className="mt-4 flex flex-col gap-3">
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving || result.extractedFields.length === 0}
              className="w-full rounded-xl bg-blue-600 px-6 py-3 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 sm:w-auto"
            >
              {isSaving ? (
                <span className="flex items-center justify-center gap-2">
                  <svg aria-hidden="true" className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                  </svg>
                  Saving…
                </span>
              ) : 'Save Changes'}
            </button>

            {saveStatus === 'success' && (
              <div role="status" aria-live="polite" className="rounded-xl border border-green-200 bg-green-50 px-5 py-3">
                <p className="text-sm font-semibold text-green-800">Changes saved successfully</p>
              </div>
            )}
            {saveStatus === 'error' && (
              <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-5 py-3">
                <p className="text-sm font-semibold text-red-800">Save failed</p>
                <p className="mt-0.5 text-xs text-red-700">{saveError}</p>
              </div>
            )}
          </div>
        </section>

        {/* ── Document image + signature canvas ─────────────────────────────── */}
        <section aria-label="Document preview and signature capture">
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-gray-500">
            Document Preview
          </h2>

          <div className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white px-5 py-5">
            {/* Metadata */}
            <div className="flex flex-col gap-1">
              <p className="truncate text-sm font-semibold text-gray-800" title={result.fileName}>
                {result.fileName}
              </p>
              <dl className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-gray-500">
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Analyzed:</dt>
                  <dd>{analyzedDate}</dd>
                </div>
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Fields:</dt>
                  <dd>{result.extractedFields.length} {result.extractedFields.length === 1 ? 'field' : 'fields'} extracted</dd>
                </div>
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Document ID:</dt>
                  <dd>{result.documentId}</dd>
                </div>
              </dl>
            </div>

            {/* Interactive signature canvas — imageUrl is null until client hydrates */}
            {imageUrl && (
              <SignatureCanvas
                imageUrl={imageUrl}
                onCapture={handleCapture}
                isSaving={isCapturing}
                captureError={captureError}
              />
            )}

            {/* Raw OCR text — collapsed by default */}
            <details className="group">
              <summary className="cursor-pointer select-none text-xs font-medium text-gray-600 hover:text-gray-900 focus:outline-none">
                <span className="group-open:hidden">Show Raw OCR Text</span>
                <span className="hidden group-open:inline">Hide Raw OCR Text</span>
              </summary>
              <pre className="mt-3 max-h-64 overflow-auto whitespace-pre-wrap rounded-lg bg-gray-50 p-3 text-xs text-gray-700 ring-1 ring-gray-200">
                {result.rawText || '(no text extracted)'}
              </pre>
            </details>
          </div>

          <div className="mt-4">
            <Link href="/upload" className="text-sm font-medium text-blue-600 hover:text-blue-800 focus:outline-none focus:underline">
              ← Analyze another document
            </Link>
          </div>
        </section>
      </div>

      {/* ── OCR tokens table (full width below) ───────────────────────────── */}
      <OcrTokensTable blocks={result.textBlocks} />
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/components/results/ResultsClient.tsx
git commit -m "feat: wire SignatureCanvas and OcrTokensTable into ResultsClient; Signature row shows captured image"
```

---

## Task 16: Verify frontend builds

- [ ] **Step 1: Type-check the frontend**

```bash
cd frontend
npx tsc --noEmit
```

Expected: no errors. If TypeScript reports errors, fix them before continuing. Common issues:
- `API_BASE` not exported from `api.ts` — it is exported (`export const API_BASE = ...`), so this should resolve fine.
- Property name casing mismatches between backend JSON (camelCase) and TypeScript interfaces — the interfaces use camelCase which matches ASP.NET Core's default serialization.

- [ ] **Step 2: Commit any fixes**

```bash
cd ..
git add frontend/src/
git commit -m "fix: resolve frontend TypeScript errors"
```

---

## Task 17: Final commit and smoke test checklist

- [ ] **Step 1: Verify all commits are in order**

```bash
git log --oneline -12
```

Expected (most recent first):
```
fix: resolve frontend TypeScript errors        (if needed)
feat: wire SignatureCanvas and OcrTokensTable into ResultsClient
feat: add OcrTokensTable component
feat: add SignatureCanvas component for interactive signature region capture
feat: add captureSignature API function
feat: add OcrTextBlock type; extend AnalysisResultDetail
fix: move Convert.ToBase64String out of EF projection in GetById (if needed)
feat: add GetImage and CaptureSignature endpoints
feat: include textBlocks and signatureImage in GetById response
feat: buffer file bytes; store ImageBytes and SavedTextBlocks on analyze
fix: map SavedField to DTO in UpdateFields to resolve circular reference
feat: migration AddImageStorageAndTextBlocks
feat: add saved_text_blocks table and image columns to OcrDbContext
feat: add TextBlockDto, SignatureCaptureRequest/Response; extend AnalysisResultDetailDto
feat: add ImageBytes, SignatureImage and TextBlocks to AnalysisResult
feat: add SavedTextBlock entity for OCR token storage
```

- [ ] **Step 2: Bring up the full stack**

```bash
docker-compose up --build
```

Wait for all health checks to pass (backend, ocr-service, frontend each log healthy).

- [ ] **Step 3: Manual smoke test**

1. Open `http://localhost:3000/upload`
2. Upload a PNG/JPEG document containing visible text
3. Click "Analyze Document" — should redirect to `/results/{id}`
4. **Document preview**: the actual document image should appear on the right panel (not the old placeholder)
5. **OCR tokens table**: below the two columns, a "Raw OCR Tokens" section shows a table of every word Tesseract found with confidence badges and positions
6. **Signature capture**: click-drag a rectangle over the signature area on the document image → a green "Capture Signature" button appears → click it → the "Signature" row in the Extracted Fields table switches from a text input to an image of the cropped region
7. **Save Changes**: edit any non-Signature field override, click "Save Changes" → green "Changes saved successfully" banner appears (no more 500 error)
8. **Reload** the results page (`/results/{id}`) — the captured signature should still show (it was persisted to the DB)
