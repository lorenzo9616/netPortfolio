# Empty Fields Collapse + AI Auto-Properties + Analyzed Documents Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add empty-fields disclosure section in results view, auto-classify documents with Claude AI to inject temporary extraction properties, and upgrade the Analyzed Documents page with per-document field accordions.

**Architecture:** Feature 1 is a pure frontend rendering change. Feature 2 adds a new `DocumentClassifierService` in the .NET backend that calls the Anthropic REST API after OCR completes, merges AI-suggested properties in-memory with DB properties before field matching, and saves the detected document type to a new DB column. Feature 3 expands the existing `GET /api/documents` list endpoint to include non-empty fields per document, and rewrites `DocumentsClient.tsx` with an expandable accordion layout.

**Tech Stack:** Next.js 14 (App Router), .NET 8, Entity Framework Core, PostgreSQL, Anthropic REST API (claude-haiku-4-5-20251001), Tailwind CSS.

---

## File Map

| File | Change |
|------|--------|
| `frontend/src/components/results/ResultsClient.tsx` | Split filled/empty fields; show documentType |
| `frontend/src/components/documents/DocumentsClient.tsx` | Full rewrite: accordion with fields |
| `frontend/src/components/Sidebar.tsx` | Rename "History" → "Analyzed Documents" |
| `frontend/src/app/documents/page.tsx` | Update h1 heading |
| `frontend/src/types/ocr.ts` | Add DocumentFieldSummary, update DocumentSummary + AnalysisResultDetail |
| `backend/Models/AnalysisResult.cs` | Add DocumentType column |
| `backend/Models/DocumentModels.cs` | Add DocumentFieldSummaryDto; update DocumentSummaryDto + AnalysisResultDetailDto |
| `backend/Data/OcrDbContext.cs` | Column config for DocumentType |
| `backend/Services/IDocumentClassifierService.cs` | New interface + records |
| `backend/Services/DocumentClassifierService.cs` | New service calling Anthropic REST API |
| `backend/Controllers/DocumentsController.cs` | Inject classifier; update Analyze, AnalyzeStream, GetAll, GetById |
| `backend/Program.cs` | Register named HttpClient + DocumentClassifierService |
| `backend/appsettings.json` | Add Anthropic:ApiKey placeholder |
| `backend/Migrations/` | New migration for DocumentType column |

---

## Task 1: Empty Fields Collapse in ResultsClient

**Files:**
- Modify: `frontend/src/components/results/ResultsClient.tsx`

- [ ] **Step 1: Split fields and add empty disclosure section**

Replace the entire fields table block (lines 162–219 in `ResultsClient.tsx`) with this version that separates filled from empty fields:

```tsx
          {result.extractedFields.length === 0 ? (
            <div className="rounded-xl border border-gray-200 bg-gray-50 px-5 py-6 text-center">
              <p className="text-sm text-gray-600">
                No fields were extracted. Try adjusting your OCR properties.
              </p>
            </div>
          ) : (
            <>
              {(() => {
                const filledFields = result.extractedFields.filter(
                  (f) => f.extractedValue !== null && f.extractedValue !== ''
                );
                const emptyFields = result.extractedFields.filter(
                  (f) => f.extractedValue === null || f.extractedValue === ''
                );

                const renderRows = (fields: typeof result.extractedFields) =>
                  fields.map((field) => {
                    const isSignatureField = field.propertyName === 'Signature';
                    return (
                      <tr key={field.propertyName} className="align-middle">
                        <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-800">
                          {field.propertyName}
                        </td>
                        <td className="px-4 py-3 text-gray-600">
                          {isSignatureField && signatureImage
                            ? <span className="inline-flex items-center rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-700 ring-1 ring-inset ring-emerald-200">Auto-detected</span>
                            : (field.extractedValue ?? <span className="italic text-gray-400">—</span>)
                          }
                        </td>
                        <td className="px-4 py-3">
                          {isSignatureField
                            ? <span className="text-xs italic text-gray-400">See preview above</span>
                            : (
                              <input
                                type="text"
                                aria-label={`Manual override for ${field.propertyName}`}
                                value={overrides[field.propertyName] ?? ''}
                                onChange={(e) => handleOverrideChange(field.propertyName, e.target.value)}
                                disabled={isSaving}
                                className="w-full rounded-md border border-gray-300 bg-white px-2 py-1 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:cursor-not-allowed disabled:bg-gray-50"
                                placeholder="Enter override…"
                              />
                            )
                          }
                        </td>
                        <td className="px-4 py-3 text-right">
                          <ConfidenceBadge value={field.confidence} />
                        </td>
                      </tr>
                    );
                  });

                return (
                  <div className="flex flex-col gap-3">
                    {filledFields.length > 0 && (
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
                            {renderRows(filledFields)}
                          </tbody>
                        </table>
                      </div>
                    )}

                    {emptyFields.length > 0 && (
                      <details className="group rounded-xl border border-gray-200 bg-white">
                        <summary className="cursor-pointer select-none px-4 py-3 text-xs font-semibold uppercase tracking-wide text-gray-400 hover:text-gray-600 focus:outline-none list-none flex items-center gap-2">
                          <svg className="h-3.5 w-3.5 transition-transform group-open:rotate-90" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                            <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
                          </svg>
                          {emptyFields.length} empty {emptyFields.length === 1 ? 'field' : 'fields'}
                        </summary>
                        <div className="border-t border-gray-100">
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
                              {renderRows(emptyFields)}
                            </tbody>
                          </table>
                        </div>
                      </details>
                    )}
                  </div>
                );
              })()}
            </>
          )}
```

- [ ] **Step 2: Verify TypeScript compiles**

Run from `frontend/`:
```bash
npx tsc --noEmit
```
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/components/results/ResultsClient.tsx
git commit -m "feat: collapse empty extracted fields into disclosure section"
```

---

## Task 2: Add DocumentType Column to AnalysisResult

**Files:**
- Modify: `backend/Models/AnalysisResult.cs`
- Modify: `backend/Data/OcrDbContext.cs`
- Create: new EF migration

- [ ] **Step 1: Add DocumentType property to AnalysisResult**

In `backend/Models/AnalysisResult.cs`, add the new property after `TableBlocksJson`:

```csharp
namespace OcrApi.Models;

public class AnalysisResult
{
    public int Id { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string FileName { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public byte[]? ImageBytes { get; set; }
    public byte[]? SignatureImage { get; set; }
    public string? TableBlocksJson { get; set; }
    public string? DocumentType { get; set; }
    public List<SavedField> Fields { get; set; } = new();
    public List<SavedTextBlock> TextBlocks { get; set; } = new();
    public List<DocumentPage> Pages { get; set; } = new();
}
```

- [ ] **Step 2: Add column configuration in OcrDbContext**

In `backend/Data/OcrDbContext.cs`, inside `modelBuilder.Entity<AnalysisResult>(entity => { ... })`, add after the `TableBlocksJson` property config:

```csharp
            entity.Property(e => e.DocumentType)
                  .IsRequired(false)
                  .HasMaxLength(100);
```

The full AnalysisResult block becomes:
```csharp
        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.ToTable("analysis_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.RawText).IsRequired(false);
            entity.Property(e => e.AnalyzedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.ImageBytes).IsRequired(false);
            entity.Property(e => e.SignatureImage).IsRequired(false);
            entity.Property(e => e.TableBlocksJson).IsRequired(false);
            entity.Property(e => e.DocumentType).IsRequired(false).HasMaxLength(100);

            entity.HasMany(e => e.Fields)
                  .WithOne(f => f.AnalysisResult)
                  .HasForeignKey(f => f.AnalysisResultId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TextBlocks)
                  .WithOne(b => b.AnalysisResult)
                  .HasForeignKey(b => b.AnalysisResultId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Pages)
                  .WithOne(p => p.AnalysisResult)
                  .HasForeignKey(p => p.AnalysisResultId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
```

- [ ] **Step 3: Generate the EF migration**

Run from the repo root:
```bash
cd backend
dotnet ef migrations add AddDocumentType
cd ..
```
Expected output includes: `Done. To undo this action, use 'ef migrations remove'`

- [ ] **Step 4: Verify migration content**

Open the newly created migration file in `backend/Migrations/` (name will contain `AddDocumentType`). Confirm it contains:
```csharp
migrationBuilder.AddColumn<string>(
    name: "DocumentType",
    table: "analysis_results",
    type: "character varying(100)",
    maxLength: 100,
    nullable: true);
```

- [ ] **Step 5: Commit**

```bash
git add backend/Models/AnalysisResult.cs backend/Data/OcrDbContext.cs backend/Migrations/
git commit -m "feat: add DocumentType column to analysis_results table"
```

---

## Task 3: DocumentClassifierService

**Files:**
- Modify: `backend/appsettings.json`
- Create: `backend/Services/IDocumentClassifierService.cs`
- Create: `backend/Services/DocumentClassifierService.cs`
- Modify: `backend/Program.cs`

- [ ] **Step 1: Add Anthropic API key placeholder to appsettings.json**

Replace `backend/appsettings.json` content with:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Auth": {
    "ApiKey": ""
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000" ]
  },
  "Services": {
    "OcrServiceUrl": "http://ocr-service:8000"
  },
  "Anthropic": {
    "ApiKey": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Set the real key via the `Anthropic__ApiKey` environment variable at runtime (same pattern as `Auth__ApiKey`).

- [ ] **Step 2: Create IDocumentClassifierService.cs**

Create `backend/Services/IDocumentClassifierService.cs`:
```csharp
namespace OcrApi.Services;

public record SuggestedProperty(
    string Name,
    string DataType,
    string SearchHeuristic,
    bool IsRegex
);

public record ClassificationResult(
    string DocumentType,
    List<SuggestedProperty> SuggestedProperties
);

public interface IDocumentClassifierService
{
    Task<ClassificationResult> ClassifyAsync(string rawText, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create DocumentClassifierService.cs**

Create `backend/Services/DocumentClassifierService.cs`:
```csharp
using System.Text.Json;
using OcrApi.Models;

namespace OcrApi.Services;

public class DocumentClassifierService : IDocumentClassifierService
{
    private static readonly ClassificationResult _emptyResult =
        new("Unknown", new List<SuggestedProperty>());

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentClassifierService> _logger;

    public DocumentClassifierService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DocumentClassifierService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(string rawText, CancellationToken ct = default)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("DocumentClassifierService: Anthropic:ApiKey not configured — skipping AI classification");
            return _emptyResult;
        }

        var truncated = rawText.Length > 2000 ? rawText[..2000] : rawText;

        const string systemPrompt = """
            You are a document classifier. Analyze OCR text and respond with ONLY valid JSON, no other text.
            Identify the document type and suggest up to 8 field extraction properties specific to this document.
            
            Required JSON format:
            {
              "documentType": "Invoice",
              "suggestedProperties": [
                { "name": "VendorName", "dataType": "string", "searchHeuristic": "vendor|from|seller", "isRegex": false },
                { "name": "TotalDue", "dataType": "decimal", "searchHeuristic": "total\\s*due[:\\s]*\\$?([\\d,\\.]+)", "isRegex": true }
              ]
            }
            
            Rules:
            - documentType: short name (Invoice, Receipt, Medical Record, Legal Contract, ID Document, Meeting Minutes, Other)
            - name: PascalCase, no spaces
            - dataType: string | date | decimal | number
            - isRegex true → regex with one capture group for the value; false → keyword/phrase to search for
            - Avoid duplicating these already-standard properties: FullName, DateOfBirth, Signature, InvoiceNumber, InvoiceDate, DueDate, AccountNumber, TaxAmount
            - Return at most 8 suggestedProperties
            """;

        var requestBody = new
        {
            model      = "claude-haiku-4-5-20251001",
            max_tokens = 1024,
            system     = systemPrompt,
            messages   = new[] { new { role = "user", content = $"Classify this document:\n\n{truncated}" } }
        };

        try
        {
            var client = _httpClientFactory.CreateClient("anthropic");
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);

            var json    = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response     = await client.PostAsync("/v1/messages", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "DocumentClassifierService: Anthropic API returned {Status} — {Body}",
                    (int)response.StatusCode, responseBody.Length > 200 ? responseBody[..200] : responseBody);
                return _emptyResult;
            }

            using var doc  = JsonDocument.Parse(responseBody);
            var textContent = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            var opts   = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ClassificationResult>(textContent, opts);

            return result ?? _emptyResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DocumentClassifierService: classification failed — continuing without AI properties");
            return _emptyResult;
        }
    }
}
```

- [ ] **Step 4: Register named HttpClient and service in Program.cs**

In `backend/Program.cs`, after the `AddSingleton<IFieldMatcher>` line, add:
```csharp
builder.Services.AddHttpClient("anthropic", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});
builder.Services.AddScoped<IDocumentClassifierService, DocumentClassifierService>();
```

- [ ] **Step 5: Build to verify**

```bash
cd backend && dotnet build && cd ..
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add backend/appsettings.json backend/Services/IDocumentClassifierService.cs backend/Services/DocumentClassifierService.cs backend/Program.cs
git commit -m "feat: add DocumentClassifierService for AI-based document type detection"
```

---

## Task 4: Wire Classifier into DocumentsController

**Files:**
- Modify: `backend/Controllers/DocumentsController.cs`

- [ ] **Step 1: Add classifier field and inject via constructor**

In `DocumentsController.cs`, add the field and constructor parameter:

```csharp
    private readonly IOcrClient _ocrClient;
    private readonly IFieldMatcher _fieldMatcher;
    private readonly IDocumentClassifierService _classifierService;
    private readonly IOcrPropertyRepository _propertyRepository;
    private readonly OcrDbContext _dbContext;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IOcrClient ocrClient,
        IFieldMatcher fieldMatcher,
        IDocumentClassifierService classifierService,
        IOcrPropertyRepository propertyRepository,
        OcrDbContext dbContext,
        ILogger<DocumentsController> logger)
    {
        _ocrClient          = ocrClient;
        _fieldMatcher       = fieldMatcher;
        _classifierService  = classifierService;
        _propertyRepository = propertyRepository;
        _dbContext          = dbContext;
        _logger             = logger;
    }
```

- [ ] **Step 2: Update the Analyze endpoint**

Replace the single line `var extractedFields = _fieldMatcher.MatchFields(activeProperties, ocrResult);` in the `Analyze` method with:

```csharp
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
```

Also set `DocumentType` when building `analysisResult`. Add `DocumentType = classification.DocumentType,` to the object initializer:

```csharp
        var analysisResult = new AnalysisResult
        {
            FileName       = file.FileName,
            RawText        = ocrResult.RawText,
            AnalyzedAt     = DateTime.UtcNow,
            ImageBytes     = fileBytes,
            SignatureImage = autoSignatureBytes,
            DocumentType   = classification.DocumentType,
            Fields         = extractedFields.Select(f => new SavedField
            // ... rest unchanged
```

- [ ] **Step 3: Update the AnalyzeStream endpoint**

Inside the `else if (currentEvent == "complete")` block, replace the single `MatchFields` call with the classifier + merge + SSE events. Find this block:

```csharp
                    else if (currentEvent == "complete")
                    {
                        await WriteEventAsync("status", new { step = "saving", message = "Saving results to database" });

                        var ocrResult = JsonSerializer.Deserialize<ExtractTextResponse>(currentData, _snakeCaseOptions);

                        if (ocrResult is not null)
                        {
                            var extractedFields = _fieldMatcher.MatchFields(activeProperties, ocrResult);
```

Replace with:

```csharp
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
```

Also add `DocumentType = classification.DocumentType,` to the `analysisResult` object initializer inside `AnalyzeStream`. The full initializer becomes:

```csharp
                            var analysisResult = new AnalysisResult
                            {
                                FileName       = file.FileName,
                                RawText        = ocrResult.RawText,
                                AnalyzedAt     = DateTime.UtcNow,
                                ImageBytes     = fileBytes,
                                SignatureImage = autoSignatureBytes,
                                DocumentType   = classification.DocumentType,
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
```

- [ ] **Step 4: Build to verify**

```bash
cd backend && dotnet build && cd ..
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add backend/Controllers/DocumentsController.cs
git commit -m "feat: wire AI document classifier into analyze pipeline; emit SSE classification events"
```

---

## Task 5: Expand DTOs — DocumentType + Fields in List/Detail

**Files:**
- Modify: `backend/Models/DocumentModels.cs`
- Modify: `backend/Controllers/DocumentsController.cs`

- [ ] **Step 1: Add DocumentFieldSummaryDto and update DocumentSummaryDto + AnalysisResultDetailDto**

In `backend/Models/DocumentModels.cs`, replace the `DocumentSummaryDto` class and add the new `DocumentFieldSummaryDto` class. Find the comment `// ── GET /api/documents response` and replace everything from there through the end of `DocumentSummaryDto` with:

```csharp
// ── GET /api/documents response ───────────────────────────────────────────────

public class DocumentFieldSummaryDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public double Confidence { get; set; }
}

public class DocumentSummaryDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public string? DocumentType { get; set; }
    public int FieldCount { get; set; }
    public int PageCount { get; set; }
    public List<DocumentFieldSummaryDto> Fields { get; set; } = new();
}
```

Then in the same file, update `AnalysisResultDetailDto` to add `DocumentType`:

```csharp
public class AnalysisResultDetailDto
{
    public int DocumentId { get; set; }
    public List<ExtractedFieldDto> ExtractedFields { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public List<TextBlockDto> TextBlocks { get; set; } = new();
    public string? SignatureImage { get; set; }
    public int PageCount { get; set; }
    public List<TableBlockDto> TableBlocks { get; set; } = new();
}
```

- [ ] **Step 2: Update GetAll to include fields and documentType**

In `DocumentsController.cs`, replace the `GetAll` method body with:

```csharp
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
```

- [ ] **Step 3: Update GetById to include documentType in the DTO**

In `DocumentsController.cs`, in the `GetById` method, update the `dto` construction to include `DocumentType`:

```csharp
        var dto = new AnalysisResultDetailDto
        {
            DocumentId     = raw.Id,
            FileName       = raw.FileName,
            RawText        = raw.RawText,
            AnalyzedAt     = raw.AnalyzedAt,
            DocumentType   = raw.DocumentType,
            PageCount      = raw.PageCount,
            SignatureImage = raw.SignatureImage is not null
                               ? Convert.ToBase64String(raw.SignatureImage)
                               : null,
            ExtractedFields = raw.Fields,
            TextBlocks      = raw.TextBlocks,
            TableBlocks     = tableBlocks,
        };
```

Also update the `Select` projection in `GetById` to include `DocumentType`. Replace the entire `var raw = await _dbContext.AnalysisResults...` projection with:

```csharp
        var raw = await _dbContext.AnalysisResults
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.FileName,
                r.RawText,
                r.AnalyzedAt,
                r.SignatureImage,
                r.TableBlocksJson,
                r.DocumentType,
                PageCount = r.Pages.Count,
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
```

- [ ] **Step 4: Build to verify**

```bash
cd backend && dotnet build && cd ..
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add backend/Models/DocumentModels.cs backend/Controllers/DocumentsController.cs
git commit -m "feat: expand GET /api/documents to include document type and extracted fields; add documentType to detail endpoint"
```

---

## Task 6: Frontend Types and API Client

**Files:**
- Modify: `frontend/src/types/ocr.ts`

- [ ] **Step 1: Update ocr.ts with new types**

Replace the entire contents of `frontend/src/types/ocr.ts` with:

```typescript
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

export interface TableCell {
  row: number;
  col: number;
  text: string;
}

export interface TableBlock {
  page: number;
  rows: number;
  cols: number;
  cells: TableCell[];
}

export interface AnalysisResultDetail {
  documentId: number;
  extractedFields: ExtractedField[];
  rawText: string;
  analyzedAt: string;
  fileName: string;
  documentType: string | null;
  textBlocks: OcrTextBlock[];
  signatureImage: string | null;
  pageCount: number;
  tableBlocks: TableBlock[];
}

export interface DocumentFieldSummary {
  propertyName: string;
  extractedValue: string | null;
  confidence: number;
}

export interface DocumentSummary {
  id: number;
  fileName: string;
  analyzedAt: string;
  documentType: string | null;
  fieldCount: number;
  pageCount: number;
  fields: DocumentFieldSummary[];
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

- [ ] **Step 2: Verify TypeScript compiles**

```bash
cd frontend && npx tsc --noEmit && cd ..
```
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/types/ocr.ts
git commit -m "feat: add DocumentFieldSummary type; add documentType to DocumentSummary and AnalysisResultDetail"
```

---

## Task 7: Show Document Type in ResultsClient

**Files:**
- Modify: `frontend/src/components/results/ResultsClient.tsx`

- [ ] **Step 1: Add documentType row to metadata section**

In `ResultsClient.tsx`, find the `<dl>` metadata block (around line 287) and add a documentType row. The full `<dl>` block becomes:

```tsx
              <dl className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-gray-500">
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Analyzed:</dt>
                  <dd>{analyzedDate}</dd>
                </div>
                {result.documentType && result.documentType !== 'Unknown' && (
                  <div className="flex gap-1">
                    <dt className="font-medium text-gray-600">Type:</dt>
                    <dd>
                      <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-0.5 text-xs font-semibold text-blue-700 ring-1 ring-inset ring-blue-200">
                        {result.documentType}
                      </span>
                    </dd>
                  </div>
                )}
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Fields:</dt>
                  <dd>{result.extractedFields.length} {result.extractedFields.length === 1 ? 'field' : 'fields'} extracted</dd>
                </div>
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Document ID:</dt>
                  <dd>{result.documentId}</dd>
                </div>
              </dl>
```

- [ ] **Step 2: Verify TypeScript compiles**

```bash
cd frontend && npx tsc --noEmit && cd ..
```
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/components/results/ResultsClient.tsx
git commit -m "feat: show AI-detected document type badge in results metadata"
```

---

## Task 8: Analyzed Documents Page — Accordion with Fields

**Files:**
- Modify: `frontend/src/components/documents/DocumentsClient.tsx`
- Modify: `frontend/src/app/documents/page.tsx`
- Modify: `frontend/src/components/Sidebar.tsx`

- [ ] **Step 1: Rewrite DocumentsClient.tsx with accordion**

Replace the entire contents of `frontend/src/components/documents/DocumentsClient.tsx` with:

```tsx
'use client';

import { useState } from 'react';
import Link from 'next/link';
import type { DocumentSummary } from '@/types/ocr';

interface Props {
  documents: DocumentSummary[];
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

export default function DocumentsClient({ documents }: Props) {
  const [expandedId, setExpandedId] = useState<number | null>(null);

  if (documents.length === 0) {
    return (
      <div className="rounded-xl border border-gray-200 bg-gray-50 px-5 py-12 text-center">
        <p className="text-sm font-semibold text-gray-700">No documents analyzed yet.</p>
        <p className="mt-1 text-xs text-gray-500">Upload a document to get started.</p>
        <Link
          href="/upload"
          className="mt-4 inline-block rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
        >
          Upload Document
        </Link>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
      <table className="min-w-full divide-y divide-gray-100 text-sm">
        <thead>
          <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
            <th className="w-6 px-4 py-3" />
            <th className="px-4 py-3">Document Name</th>
            <th className="px-4 py-3">Type</th>
            <th className="px-4 py-3">Analyzed Date</th>
            <th className="px-4 py-3">Fields Extracted</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {documents.map((doc) => {
            const isExpanded = expandedId === doc.id;
            const analyzedDate = new Date(doc.analyzedAt).toLocaleString(undefined, {
              dateStyle: 'medium',
              timeStyle: 'short',
            });

            return (
              <>
                <tr
                  key={doc.id}
                  className="cursor-pointer transition-colors hover:bg-gray-50"
                  onClick={() => setExpandedId(isExpanded ? null : doc.id)}
                  aria-expanded={isExpanded}
                >
                  <td className="px-4 py-3 text-gray-400">
                    <svg
                      className={`h-3.5 w-3.5 transition-transform ${isExpanded ? 'rotate-90' : ''}`}
                      viewBox="0 0 20 20"
                      fill="currentColor"
                      aria-hidden="true"
                    >
                      <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
                    </svg>
                  </td>
                  <td className="max-w-xs truncate px-4 py-3 font-medium text-gray-800" title={doc.fileName}>
                    {doc.fileName}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    {doc.documentType && doc.documentType !== 'Unknown' ? (
                      <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-0.5 text-xs font-semibold text-blue-700 ring-1 ring-inset ring-blue-200">
                        {doc.documentType}
                      </span>
                    ) : (
                      <span className="text-xs text-gray-400">—</span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-600">{analyzedDate}</td>
                  <td className="px-4 py-3 text-gray-600">{doc.fieldCount}</td>
                </tr>

                {isExpanded && (
                  <tr key={`${doc.id}-fields`}>
                    <td colSpan={5} className="bg-gray-50 px-8 py-4">
                      {doc.fields.length === 0 ? (
                        <p className="text-xs italic text-gray-400">No extracted values for this document.</p>
                      ) : (
                        <>
                          <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
                            <table className="min-w-full divide-y divide-gray-100 text-sm">
                              <thead>
                                <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
                                  <th className="px-4 py-2">Field Name</th>
                                  <th className="px-4 py-2">Extracted Value</th>
                                  <th className="px-4 py-2 text-right">Confidence</th>
                                </tr>
                              </thead>
                              <tbody className="divide-y divide-gray-100">
                                {doc.fields.map((field) => (
                                  <tr key={field.propertyName}>
                                    <td className="whitespace-nowrap px-4 py-2 font-medium text-gray-700">
                                      {field.propertyName}
                                    </td>
                                    <td className="px-4 py-2 text-gray-600">
                                      {field.extractedValue ?? <span className="italic text-gray-400">—</span>}
                                    </td>
                                    <td className="px-4 py-2 text-right">
                                      <ConfidenceBadge value={field.confidence} />
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                          <div className="mt-3 flex justify-end">
                            <Link
                              href={`/results/${doc.id}`}
                              onClick={(e) => e.stopPropagation()}
                              className="rounded-md bg-blue-50 px-3 py-1.5 text-xs font-semibold text-blue-700 transition-colors hover:bg-blue-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
                            >
                              View Full Results →
                            </Link>
                          </div>
                        </>
                      )}
                    </td>
                  </tr>
                )}
              </>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
```

- [ ] **Step 2: Update page heading in documents/page.tsx**

In `frontend/src/app/documents/page.tsx`, change the h1 text from `"Document History"` to `"Analyzed Documents"` (appears in two places — the success render and the error render):

```tsx
          <h1 className="text-2xl font-bold text-gray-900">Analyzed Documents</h1>
```

Both occurrences should read "Analyzed Documents".

- [ ] **Step 3: Rename sidebar label**

In `frontend/src/components/Sidebar.tsx`, update the NAV_ITEMS array:

```tsx
const NAV_ITEMS: NavItem[] = [
  { label: 'Upload', href: '/upload' },
  { label: 'Analyzed Documents', href: '/documents' },
  { label: 'Properties', href: '/properties' },
];
```

- [ ] **Step 4: Verify TypeScript compiles**

```bash
cd frontend && npx tsc --noEmit && cd ..
```
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/components/documents/DocumentsClient.tsx frontend/src/app/documents/page.tsx frontend/src/components/Sidebar.tsx
git commit -m "feat: upgrade Analyzed Documents page with per-document field accordion and document type column"
```

---

## Post-Implementation Checklist

- [ ] Set `Anthropic__ApiKey` env var in your `.env` or docker-compose environment
- [ ] Restart the backend so the new migration runs and `DocumentType` column is created
- [ ] Upload a document and confirm streaming shows "Classifying document type with AI..." step
- [ ] Confirm results page shows the blue document type badge in metadata
- [ ] Confirm results page collapses empty fields into disclosure at the bottom
- [ ] Open /documents and confirm rows expand showing field sub-table and confidence badges
