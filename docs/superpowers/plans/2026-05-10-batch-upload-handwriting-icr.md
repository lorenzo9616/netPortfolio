# Batch Upload + Handwriting ICR + Fields Table Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add multi-file batch upload (sequential, SSE per file), Claude Vision handwriting/ICR mode, and a redesigned extracted-fields table to the OpenOCR system.

**Architecture:** Handwriting uses a new Python `/extract-pages` endpoint (PDF→images, no OCR) + a .NET `HandwritingExtractorService` that calls Claude Haiku Vision per page. Batch upload is frontend-only: the existing `/api/documents/analyze/stream` SSE endpoint is called once per file in a loop. Fields table is extracted into `ExtractedFieldsTable.tsx` with click-to-edit overrides.

**Tech Stack:** Next.js 14, TypeScript, Tailwind CSS, .NET 8 / ASP.NET Core, FastAPI, Python 3.11, PostgreSQL.

---

## File Map

### Created
- `ocr-service/models.py` — add `ExtractPagesResponse`
- `backend/Models/DocumentModels.cs` — add `ExtractPagesResponse` C#, `HandwritingExtractionResult`
- `backend/Services/IHandwritingExtractorService.cs` — interface
- `backend/Services/HandwritingExtractorService.cs` — Claude Vision implementation
- `frontend/src/components/results/ExtractedFieldsTable.tsx` — new component

### Modified
- `ocr-service/main.py` — add `/extract-pages` endpoint + import
- `backend/Services/IOcrClient.cs` — add `ExtractPagesAsync`
- `backend/Services/OcrClient.cs` — implement `ExtractPagesAsync`
- `backend/Services/IFieldMatcher.cs` — add `rawText` overload
- `backend/Services/FieldMatcher.cs` — implement `rawText` overload
- `backend/Models/DocumentModels.cs` — add `Mode` to `AnalyzeDocumentRequest`
- `backend/Program.cs` — register `IHandwritingExtractorService`
- `backend/Controllers/DocumentsController.cs` — inject service, fork on `Mode`
- `frontend/src/lib/api.ts` — add `mode` param to `analyzeDocumentStream`
- `frontend/src/components/results/ResultsClient.tsx` — use `ExtractedFieldsTable`
- `frontend/src/components/upload/UploadClient.tsx` — tabs, handwriting toggle, batch queue

---

## Task 1 — Add ExtractPagesResponse to OCR service models

**Files:**
- Modify: `ocr-service/models.py`

- [ ] **Step 1: Add the model**

After the `HealthResponse` class at the bottom of `ocr-service/models.py`, add:

```python
class ExtractPagesResponse(BaseModel):
    page_count: int
    page_images: list[str]  # base64 JPEG strings, one per page
```

- [ ] **Step 2: Commit**

```bash
git add ocr-service/models.py
git commit -m "feat(ocr): add ExtractPagesResponse model"
```

---

## Task 2 — Add /extract-pages endpoint to OCR service

**Files:**
- Modify: `ocr-service/main.py`

- [ ] **Step 1: Update imports at top of main.py**

The existing import line is:
```python
from models import ExtractTextResponse, HealthResponse, TextBlock
```
Change it to:
```python
from models import ExtractPagesResponse, ExtractTextResponse, HealthResponse, TextBlock
```

- [ ] **Step 2: Add the endpoint after the `/health` endpoint (before `/extract-text`)**

```python
@app.post("/extract-pages", response_model=ExtractPagesResponse)
async def extract_pages(
    file: UploadFile = File(...),
) -> ExtractPagesResponse:
    """Convert an uploaded image or PDF to page images without running OCR.

    Returns base64 JPEG images for each page. Used by the .NET backend when
    handwriting mode is active — it calls this endpoint to get page images,
    then sends each to Claude Vision for transcription.
    """
    if file.content_type not in ALLOWED_CONTENT_TYPES:
        raise HTTPException(
            status_code=415,
            detail=(
                f"Unsupported media type '{file.content_type}'. "
                f"Allowed: {', '.join(sorted(ALLOWED_CONTENT_TYPES))}"
            ),
        )

    file_bytes: bytes = await file.read()

    if len(file_bytes) > MAX_FILE_SIZE_BYTES:
        raise HTTPException(
            status_code=413,
            detail=f"File size {len(file_bytes)} bytes exceeds the {MAX_FILE_SIZE_BYTES // (1024 * 1024)} MB limit.",
        )

    if file.content_type == "application/pdf":
        images: list = await asyncio.to_thread(pdf_to_images, file_bytes)
    else:
        pil_image = Image.open(io.BytesIO(file_bytes)).convert("RGB")
        images = [np.array(pil_image)]

    page_images: list[str] = await asyncio.to_thread(
        lambda: [_encode_page_image(img) for img in images]
    )

    return ExtractPagesResponse(page_count=len(images), page_images=page_images)
```

- [ ] **Step 3: Verify manually**

Start the OCR service (`uvicorn main:app --reload`) and test with curl:
```bash
curl -X POST http://localhost:8000/extract-pages \
  -F "file=@samples/restaurant_legal_sales.pdf" | python -c "import sys,json; d=json.load(sys.stdin); print(d['page_count'], len(d['page_images']))"
```
Expected output: `1 1` (or the actual page count of the PDF).

- [ ] **Step 4: Commit**

```bash
git add ocr-service/main.py
git commit -m "feat(ocr): add /extract-pages endpoint — PDF/image to base64 page images, no OCR"
```

---

## Task 3 — Add C# models for pages extraction and handwriting result

**Files:**
- Modify: `backend/Models/DocumentModels.cs`

- [ ] **Step 1: Add Mode to AnalyzeDocumentRequest**

In `DocumentModels.cs`, the `AnalyzeDocumentRequest` class currently ends at:
```csharp
    public string Lang { get; set; } = "eng";
}
```

Add the `Mode` property:
```csharp
    public string Lang { get; set; } = "eng";
    public string Mode { get; set; } = "ocr";
}
```

- [ ] **Step 2: Add ExtractPagesResponse after the ExtractTextResponse class**

After the `ExtractTextResponse` class (which ends at `public List<OcrTableBlock> TableBlocks { get; set; } = new();`), add:

```csharp
public class ExtractPagesResponse
{
    public int PageCount { get; set; }
    public List<string> PageImages { get; set; } = new();
}
```

- [ ] **Step 3: Add HandwritingExtractionResult in the Services namespace section**

After `ExtractPagesResponse`, add:

```csharp
public class HandwritingExtractionResult
{
    public string RawText { get; set; } = string.Empty;
    public List<string> PageImages { get; set; } = new();
    public int PageCount { get; set; }
}
```

- [ ] **Step 4: Commit**

```bash
git add backend/Models/DocumentModels.cs
git commit -m "feat(backend): add Mode to AnalyzeDocumentRequest; add ExtractPagesResponse and HandwritingExtractionResult models"
```

---

## Task 4 — Add ExtractPagesAsync to IOcrClient and OcrClient

**Files:**
- Modify: `backend/Services/IOcrClient.cs`
- Modify: `backend/Services/OcrClient.cs`

- [ ] **Step 1: Add method to IOcrClient.cs**

The full file should now be:
```csharp
using OcrApi.Models;

namespace OcrApi.Services;

public interface IOcrClient
{
    Task<ExtractTextResponse> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int? cropX,
        int? cropY,
        int? cropWidth,
        int? cropHeight,
        string lang = "eng",
        CancellationToken ct = default);

    Task<HttpResponseMessage> ExtractTextStreamAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        int? cropX,
        int? cropY,
        int? cropWidth,
        int? cropHeight,
        string lang = "eng",
        CancellationToken ct = default);

    Task<ExtractPagesResponse> ExtractPagesAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default);
}
```

- [ ] **Step 2: Implement ExtractPagesAsync in OcrClient.cs**

Add this method after `ExtractTextStreamAsync`:

```csharp
    public async Task<ExtractPagesResponse> ExtractPagesAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "OcrClient: starting ExtractPagesAsync for file '{FileName}' ({ContentType})",
            fileName, contentType);

        using var multipart  = new MultipartFormDataContent();
        var fileContent      = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("/extract-pages", multipart, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OcrClient: network error reaching OCR service (extract-pages)");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OcrClient: OCR service returned {StatusCode} for extract-pages '{FileName}'",
                (int)response.StatusCode, fileName);
            throw new HttpRequestException(
                $"OCR service responded with {(int)response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<ExtractPagesResponse>(_jsonOptions, ct)
            ?? throw new InvalidOperationException("OCR service returned an empty response for extract-pages.");

        _logger.LogInformation(
            "OcrClient: ExtractPagesAsync complete — {PageCount} page(s)", result.PageCount);

        return result;
    }
```

- [ ] **Step 3: Commit**

```bash
git add backend/Services/IOcrClient.cs backend/Services/OcrClient.cs
git commit -m "feat(backend): add ExtractPagesAsync to IOcrClient and OcrClient"
```

---

## Task 5 — Add rawText overload to IFieldMatcher and FieldMatcher

**Files:**
- Modify: `backend/Services/IFieldMatcher.cs`
- Modify: `backend/Services/FieldMatcher.cs`

- [ ] **Step 1: Add overload to IFieldMatcher.cs**

Replace the full file content:

```csharp
using OcrApi.Models;

namespace OcrApi.Services;

public interface IFieldMatcher
{
    /// <summary>
    /// Matches OCR text blocks against the configured property heuristics and
    /// returns one <see cref="ExtractedField"/> per property.
    /// </summary>
    List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        ExtractTextResponse ocrResult);

    /// <summary>
    /// Matches plain text (e.g. from Claude Vision transcription) against the
    /// configured property heuristics. Used by the handwriting pipeline.
    /// </summary>
    List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        string rawText);
}
```

- [ ] **Step 2: Implement the overload in FieldMatcher.cs**

Replace the existing `MatchFields` implementation block (lines 18–29) with:

```csharp
    public List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        ExtractTextResponse ocrResult)
        => MatchFieldsCore(properties, ReconstructText(ocrResult));

    public List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        string rawText)
        => MatchFieldsCore(properties, rawText);

    private static List<ExtractedField> MatchFieldsCore(
        IEnumerable<OcrProperty> properties, string rawText)
    {
        var results = new List<ExtractedField>();
        foreach (var property in properties)
            results.Add(MatchSingleField(property, rawText));
        return results;
    }
```

The old body was:
```csharp
    public List<ExtractedField> MatchFields(
        IEnumerable<OcrProperty> properties,
        ExtractTextResponse ocrResult)
    {
        var rawText = ReconstructText(ocrResult);
        var results = new List<ExtractedField>();

        foreach (var property in properties)
            results.Add(MatchSingleField(property, rawText));

        return results;
    }
```

- [ ] **Step 3: Commit**

```bash
git add backend/Services/IFieldMatcher.cs backend/Services/FieldMatcher.cs
git commit -m "feat(backend): add rawText overload to IFieldMatcher for handwriting pipeline"
```

---

## Task 6 — Create IHandwritingExtractorService interface

**Files:**
- Create: `backend/Services/IHandwritingExtractorService.cs`

- [ ] **Step 1: Write the interface**

```csharp
using OcrApi.Models;

namespace OcrApi.Services;

public interface IHandwritingExtractorService
{
    Task<HandwritingExtractionResult> ExtractAsync(
        byte[] fileBytes,
        string fileName,
        string contentType,
        CancellationToken ct = default);
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Services/IHandwritingExtractorService.cs
git commit -m "feat(backend): add IHandwritingExtractorService interface"
```

---

## Task 7 — Implement HandwritingExtractorService

**Files:**
- Create: `backend/Services/HandwritingExtractorService.cs`

- [ ] **Step 1: Write the service**

```csharp
using System.Text;
using System.Text.Json;
using OcrApi.Models;

namespace OcrApi.Services;

public class HandwritingExtractorService : IHandwritingExtractorService
{
    private readonly IOcrClient _ocrClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HandwritingExtractorService> _logger;

    public HandwritingExtractorService(
        IOcrClient ocrClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HandwritingExtractorService> logger)
    {
        _ocrClient          = ocrClient;
        _httpClientFactory  = httpClientFactory;
        _configuration      = configuration;
        _logger             = logger;
    }

    public async Task<HandwritingExtractionResult> ExtractAsync(
        byte[] fileBytes,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        // ── 1. Get page images from OCR service (PDF→images, no Tesseract) ───────
        ExtractPagesResponse pages;
        try
        {
            pages = await _ocrClient.ExtractPagesAsync(
                new MemoryStream(fileBytes), fileName, contentType, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HandwritingExtractorService: extract-pages failed — returning empty result");
            return new HandwritingExtractionResult { RawText = string.Empty, PageCount = 0 };
        }

        if (pages.PageImages.Count == 0)
            return new HandwritingExtractionResult { RawText = string.Empty, PageCount = 0 };

        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("HandwritingExtractorService: Anthropic:ApiKey not configured — returning empty transcription");
            return new HandwritingExtractionResult
            {
                RawText    = string.Empty,
                PageImages = pages.PageImages,
                PageCount  = pages.PageCount,
            };
        }

        // ── 2. Transcribe each page with Claude Haiku Vision ─────────────────────
        var pageTexts = new List<string>();
        var client    = _httpClientFactory.CreateClient("anthropic");

        for (int i = 0; i < pages.PageImages.Count; i++)
        {
            var pageText = await TranscribePageAsync(client, apiKey, pages.PageImages[i], i + 1, ct);
            pageTexts.Add(pageText);
        }

        var rawText = pageTexts.Count == 1
            ? pageTexts[0]
            : string.Join("\n\n", pageTexts.Select((t, i) => $"--- Page {i + 1} ---\n{t}"));

        return new HandwritingExtractionResult
        {
            RawText    = rawText,
            PageImages = pages.PageImages,
            PageCount  = pages.PageCount,
        };
    }

    private async Task<string> TranscribePageAsync(
        HttpClient client, string apiKey, string base64Jpeg, int pageNum, CancellationToken ct)
    {
        var requestBody = new
        {
            model      = "claude-haiku-4-5-20251001",
            max_tokens = 4096,
            messages   = new[]
            {
                new
                {
                    role    = "user",
                    content = new object[]
                    {
                        new
                        {
                            type   = "image",
                            source = new
                            {
                                type       = "base64",
                                media_type = "image/jpeg",
                                data       = base64Jpeg,
                            },
                        },
                        new
                        {
                            type = "text",
                            text = "Transcribe all handwritten and printed text exactly as written. Preserve line breaks. Return plain text only.",
                        },
                    },
                },
            },
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("x-api-key", apiKey);

            var response     = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "HandwritingExtractorService: Anthropic returned {Status} for page {Page} — {Body}",
                    (int)response.StatusCode, pageNum,
                    responseBody.Length > 200 ? responseBody[..200] : responseBody);
                return string.Empty;
            }

            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HandwritingExtractorService: transcription failed for page {Page}", pageNum);
            return string.Empty;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add backend/Services/HandwritingExtractorService.cs
git commit -m "feat(backend): add HandwritingExtractorService — calls Claude Haiku Vision per page"
```

---

## Task 8 — Register service and fork AnalyzeStream on Mode

**Files:**
- Modify: `backend/Program.cs`
- Modify: `backend/Controllers/DocumentsController.cs`

- [ ] **Step 1: Register service in Program.cs**

After the line:
```csharp
builder.Services.AddSingleton<IFieldMatcher, FieldMatcher>();
```
Add:
```csharp
builder.Services.AddScoped<IHandwritingExtractorService, HandwritingExtractorService>();
```

- [ ] **Step 2: Inject IHandwritingExtractorService into DocumentsController**

Add the field after `_logger`:
```csharp
    private readonly IHandwritingExtractorService _handwritingService;
```

Update the constructor to accept and assign it — add after the `ILogger<DocumentsController> logger` parameter:
```csharp
        IHandwritingExtractorService handwritingService,
```
And in the body:
```csharp
        _handwritingService = handwritingService;
```

- [ ] **Step 3: Add the handwriting fork to AnalyzeStream**

In `AnalyzeStream`, after the file validation block (after the `fileBytes` array is populated and `activeProperties` are loaded), add the handwriting fork before the existing OCR SSE proxy block. Insert this entire block before `using var ocrResponse = await _ocrClient.ExtractTextStreamAsync(...)`:

```csharp
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
```

- [ ] **Step 4: Verify it builds**

```bash
cd backend && dotnet build
```
Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add backend/Program.cs backend/Controllers/DocumentsController.cs
git commit -m "feat(backend): inject HandwritingExtractorService; fork AnalyzeStream on mode=handwriting"
```

---

## Task 9 — Add mode param to analyzeDocumentStream in api.ts

**Files:**
- Modify: `frontend/src/lib/api.ts`

- [ ] **Step 1: Update the function signature and FormData**

Find the `analyzeDocumentStream` function (starts at line 161). Change its signature from:
```typescript
export async function analyzeDocumentStream(
  file: File,
  crop: CropRegion | undefined,
  lang: string,
  onProgress: (message: string) => void,
): Promise<{ documentId: number }>
```
to:
```typescript
export async function analyzeDocumentStream(
  file: File,
  crop: CropRegion | undefined,
  lang: string,
  mode: 'ocr' | 'handwriting',
  onProgress: (message: string) => void,
): Promise<{ documentId: number }>
```

In the function body, after `formData.append('lang', lang);`, add:
```typescript
  formData.append('mode', mode);
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/lib/api.ts
git commit -m "feat(frontend): add mode param to analyzeDocumentStream"
```

---

## Task 10 — Create ExtractedFieldsTable component

**Files:**
- Create: `frontend/src/components/results/ExtractedFieldsTable.tsx`

- [ ] **Step 1: Write the component**

```tsx
import { useState } from 'react';
import type { ExtractedFieldDto, SignatureDto } from '@/types/ocr';

interface Props {
  fields: ExtractedFieldDto[];
  overrides: Record<string, string>;
  isSaving: boolean;
  onOverrideChange: (propertyName: string, value: string) => void;
  signatures: SignatureDto[];
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

export default function ExtractedFieldsTable({
  fields,
  overrides,
  isSaving,
  onOverrideChange,
  signatures,
}: Props) {
  const [editingField, setEditingField] = useState<string | null>(null);

  const filledFields = fields.filter(
    (f) => f.extractedValue !== null && f.extractedValue !== '',
  );
  const emptyFields = fields.filter(
    (f) => f.extractedValue === null || f.extractedValue === '',
  );

  function renderRow(field: ExtractedFieldDto) {
    const isSignature  = field.propertyName === 'Signature';
    const displayValue = overrides[field.propertyName] || field.extractedValue;
    const isEditing    = editingField === field.propertyName;

    return (
      <tr
        key={field.propertyName}
        className="group align-middle hover:bg-gray-50"
      >
        {/* Field name */}
        <td className="whitespace-nowrap px-4 py-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
          {field.propertyName}
        </td>

        {/* Value — click to edit (except Signature) */}
        <td
          className={`px-4 py-3 font-medium text-gray-800 ${!isSignature ? 'cursor-text' : ''}`}
          onClick={() => {
            if (!isSignature && !isSaving) setEditingField(field.propertyName);
          }}
        >
          {isSignature ? (
            signatures.length > 0 ? (
              <span className="inline-flex items-center rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-700 ring-1 ring-inset ring-emerald-200">
                {signatures.length} captured
              </span>
            ) : (
              <span className="italic text-gray-400">—</span>
            )
          ) : isEditing ? (
            <input
              autoFocus
              type="text"
              value={overrides[field.propertyName] ?? ''}
              onChange={(e) => onOverrideChange(field.propertyName, e.target.value)}
              onBlur={() => setEditingField(null)}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === 'Escape') setEditingField(null); }}
              disabled={isSaving}
              aria-label={`Edit ${field.propertyName}`}
              className="w-full rounded-md border border-blue-300 bg-white px-2 py-0.5 text-sm text-gray-800 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          ) : (
            <span className={displayValue ? '' : 'italic text-gray-400'}>
              {displayValue || '—'}
            </span>
          )}
        </td>

        {/* Confidence */}
        <td className="px-4 py-3 text-right">
          {isSignature ? (
            <span className="text-xs italic text-gray-400">See preview</span>
          ) : (
            <ConfidenceBadge value={field.confidence} />
          )}
        </td>
      </tr>
    );
  }

  const tableShell = (rows: ExtractedFieldDto[]) => (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
      <table className="min-w-full divide-y divide-gray-100 text-sm">
        <thead>
          <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
            <th className="px-4 py-3">Field</th>
            <th className="px-4 py-3">Value <span className="normal-case font-normal text-gray-400">(click to edit)</span></th>
            <th className="px-4 py-3 text-right">Confidence</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {rows.map(renderRow)}
        </tbody>
      </table>
    </div>
  );

  return (
    <div className="flex flex-col gap-3">
      {filledFields.length > 0 && tableShell(filledFields)}

      {emptyFields.length > 0 && (
        <details className="group">
          <summary className="cursor-pointer select-none list-none text-xs font-medium text-gray-500 hover:text-gray-800 focus:outline-none">
            <span className="group-open:hidden">▶ {emptyFields.length} empty {emptyFields.length === 1 ? 'field' : 'fields'}</span>
            <span className="hidden group-open:inline">▼ {emptyFields.length} empty {emptyFields.length === 1 ? 'field' : 'fields'}</span>
          </summary>
          <div className="mt-2">{tableShell(emptyFields)}</div>
        </details>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/components/results/ExtractedFieldsTable.tsx
git commit -m "feat(frontend): add ExtractedFieldsTable component — 3-col layout, click-to-edit overrides"
```

---

## Task 11 — Wire ExtractedFieldsTable into ResultsClient

**Files:**
- Modify: `frontend/src/components/results/ResultsClient.tsx`

- [ ] **Step 1: Add import**

At the top of `ResultsClient.tsx`, after the existing imports, add:
```tsx
import ExtractedFieldsTable from './ExtractedFieldsTable';
```

Also remove the local `ConfidenceBadge` function (lines 16–29) since `ExtractedFieldsTable` defines its own.

- [ ] **Step 2: Replace the fields rendering block**

Find the section inside `ResultsClient` that starts with:
```tsx
          {result.extractedFields.length === 0 ? (
```
and ends with the closing `</>` and `)` of the IIFE that contains `filledFields`, `emptyFields`, and `renderRows`. Replace that entire block with:

```tsx
          {result.extractedFields.length === 0 ? (
            <div className="rounded-xl border border-gray-200 bg-gray-50 px-5 py-6 text-center">
              <p className="text-sm text-gray-600">
                No fields were extracted. Try adjusting your OCR properties.
              </p>
            </div>
          ) : (
            <ExtractedFieldsTable
              fields={result.extractedFields}
              overrides={overrides}
              isSaving={isSaving}
              onOverrideChange={handleOverrideChange}
              signatures={signatures}
            />
          )}
```

- [ ] **Step 3: Verify build**

```bash
cd frontend && npx tsc --noEmit
```
Expected: No errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/components/results/ResultsClient.tsx
git commit -m "feat(frontend): replace inline fields table with ExtractedFieldsTable component"
```

---

## Task 12 — UploadClient: tabs, handwriting toggle, batch queue

**Files:**
- Modify: `frontend/src/components/upload/UploadClient.tsx`

- [ ] **Step 1: Add QueuedFile type and update imports**

At the top of `UploadClient.tsx`, add `useRef` to the React import and add the `nanoid`-free ID helper. The imports section should start:

```tsx
'use client';

import { useReducer, useState, useCallback, useRef } from 'react';
import { useRouter } from 'next/navigation';
import type { UploadState, CropRegion } from '@/types/ocr';
import { analyzeDocumentStream, ApiError } from '@/lib/api';
import FileDropzone from './FileDropzone';
import CameraCapture from './CameraCapture';
import CanvasPreview from './CanvasPreview';
```

After the `LANGUAGES` array and before `export default function UploadClient()`, add:

```tsx
type UploadTab = 'single' | 'batch';

interface QueuedFile {
  id: string;
  file: File;
  status: 'idle' | 'streaming' | 'done' | 'error';
  steps: string[];
  documentId?: number;
  errorMessage?: string;
}
```

- [ ] **Step 2: Add tab, handwriting, and batch state inside the component**

Inside `UploadClient`, after the existing `const [lang, setLang] = useState('eng');`, add:

```tsx
  const [tab, setTab]             = useState<UploadTab>('single');
  const [handwriting, setHandwriting] = useState(false);
  const [queuedFiles, setQueuedFiles] = useState<QueuedFile[]>([]);
  const [batchRunning, setBatchRunning] = useState(false);
  const batchFileInputRef = useRef<HTMLInputElement>(null);
```

- [ ] **Step 3: Update handleAnalyze to pass mode**

Replace the `analyzeDocumentStream` call inside `handleAnalyze`:
```tsx
      const result = await analyzeDocumentStream(
        state.file,
        state.cropRegion ?? undefined,
        lang,
        (message) => {
```
with:
```tsx
      const result = await analyzeDocumentStream(
        state.file,
        state.cropRegion ?? undefined,
        lang,
        handwriting ? 'handwriting' : 'ocr',
        (message) => {
```

- [ ] **Step 4: Add batch file handlers**

After `handleReset`, add:

```tsx
  function handleBatchFilesSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []);
    if (files.length === 0) return;
    const newEntries: QueuedFile[] = files.map((file) => ({
      id: `${file.name}-${file.size}-${Date.now()}-${Math.random()}`,
      file,
      status: 'idle',
      steps: [],
    }));
    setQueuedFiles((prev) => [...prev, ...newEntries]);
    // reset input so the same file can be re-added after removal
    if (batchFileInputRef.current) batchFileInputRef.current.value = '';
  }

  function handleRemoveQueuedFile(id: string) {
    setQueuedFiles((prev) => prev.filter((f) => f.id !== id));
  }

  async function handleBatchAnalyze() {
    const pending = queuedFiles.filter((f) => f.status === 'idle');
    if (pending.length === 0 || batchRunning) return;
    setBatchRunning(true);
    for (const qf of pending) {
      setQueuedFiles((prev) =>
        prev.map((f) => (f.id === qf.id ? { ...f, status: 'streaming', steps: [] } : f)),
      );
      try {
        const result = await analyzeDocumentStream(
          qf.file,
          undefined,
          lang,
          handwriting ? 'handwriting' : 'ocr',
          (message) => {
            setQueuedFiles((prev) =>
              prev.map((f) =>
                f.id === qf.id ? { ...f, steps: [...f.steps, message] } : f,
              ),
            );
          },
        );
        setQueuedFiles((prev) =>
          prev.map((f) =>
            f.id === qf.id ? { ...f, status: 'done', documentId: result.documentId } : f,
          ),
        );
      } catch (err) {
        const message =
          err instanceof ApiError
            ? err.message
            : err instanceof Error
            ? err.message
            : 'An unexpected error occurred.';
        setQueuedFiles((prev) =>
          prev.map((f) =>
            f.id === qf.id ? { ...f, status: 'error', errorMessage: message } : f,
          ),
        );
      }
    }
    setBatchRunning(false);
  }
```

- [ ] **Step 5: Replace the return JSX**

Replace the entire `return (...)` block with the following. The structure is: tab bar → handwriting toggle → tab content.

```tsx
  const idleCount     = queuedFiles.filter((f) => f.status === 'idle').length;
  const canBatchStart = idleCount > 0 && !batchRunning;

  return (
    <div className="flex flex-col gap-5">
      {/* ── Tab bar ────────────────────────────────────────────────────────── */}
      <div className="flex gap-0.5 self-start rounded-xl border border-gray-200 bg-gray-50 p-0.5">
        {(['single', 'batch'] as UploadTab[]).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            className={`rounded-lg px-4 py-1.5 text-sm font-semibold transition-colors ${
              tab === t
                ? 'bg-white text-gray-800 shadow-sm'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {t === 'single' ? 'Single Document' : 'Batch Upload'}
          </button>
        ))}
      </div>

      {/* ── Handwriting toggle ─────────────────────────────────────────────── */}
      <label className="flex cursor-pointer items-center gap-3 self-start rounded-xl border border-gray-200 bg-white px-4 py-2.5">
        <input
          type="checkbox"
          checked={handwriting}
          onChange={(e) => setHandwriting(e.target.checked)}
          className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <span className="text-sm font-medium text-gray-700">
          Handwriting / ICR mode
        </span>
        <span className="text-xs text-gray-400">uses Claude Vision</span>
      </label>

      {/* ── Single tab ─────────────────────────────────────────────────────── */}
      {tab === 'single' && (
        <>
          <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
            <section aria-label="File input">
              <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
                Select Document
              </h2>
              {state.isCapturing ? (
                <CameraCapture onCapture={handleCameraCapture} onCancel={handleCameraCancel} />
              ) : (
                <FileDropzone onFileSelected={handleFileSelected} onCameraToggle={handleCameraToggle} />
              )}
              {state.file && !state.isCapturing && (
                <div className="mt-3 flex items-center gap-3 rounded-lg border border-gray-200 bg-white px-3 py-2.5">
                  <svg aria-hidden="true" className="h-4 w-4 shrink-0 text-gray-400" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
                  </svg>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-gray-800">{state.file.name}</p>
                    <p className="text-xs text-gray-400">{(state.file.size / 1024 / 1024).toFixed(2)} MB</p>
                  </div>
                  <button type="button" aria-label="Remove file" onClick={handleReset}
                    className="shrink-0 rounded-md px-2.5 py-1 text-xs font-medium text-gray-500 hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-400">
                    Remove
                  </button>
                </div>
              )}
            </section>
            {state.file && !state.isCapturing && (
              <section aria-label="Document preview and crop selection">
                <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
                  Preview & Crop
                </h2>
                <CanvasPreview file={state.file} onCropChange={handleCropChange} />
              </section>
            )}
          </div>

          {!state.isCapturing && (
            <div className="flex flex-wrap items-end gap-3">
              {!handwriting && (
                <div className="flex flex-col gap-1">
                  <label htmlFor="ocr-lang" className="text-xs font-medium text-gray-500">Language</label>
                  <select id="ocr-lang" value={lang} onChange={(e) => setLang(e.target.value)}
                    className="rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500">
                    {LANGUAGES.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}
                  </select>
                </div>
              )}
              <button type="button"
                aria-label={state.file ? 'Analyze document' : 'Select a file before analyzing'}
                disabled={!state.file || analyzeStatus.status === 'streaming'}
                onClick={handleAnalyze}
                className="rounded-xl bg-blue-600 px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2">
                {analyzeStatus.status === 'streaming' ? (
                  <span className="flex items-center gap-2">
                    <svg aria-hidden="true" className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                    </svg>
                    Analyzing…
                  </span>
                ) : (
                  `Analyze${state.cropRegion ? ' (cropped)' : ''}`
                )}
              </button>
            </div>
          )}

          {!state.isCapturing && analyzeStatus.status === 'streaming' && analyzeStatus.steps.length > 0 && (
            <div className="rounded-xl border border-blue-100 bg-blue-50 px-4 py-3">
              <ul className="flex flex-col gap-1.5">
                {analyzeStatus.steps.map((step, i) => (
                  <li key={i} className="flex items-center gap-2 text-sm text-blue-800">
                    <svg className="h-4 w-4 shrink-0 text-blue-500" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                      <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                    </svg>
                    {step}
                  </li>
                ))}
                <li className="flex items-center gap-2 text-sm text-blue-500">
                  <svg aria-hidden="true" className="h-4 w-4 shrink-0 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                  </svg>
                  Processing…
                </li>
              </ul>
            </div>
          )}

          {!state.isCapturing && analyzeStatus.status === 'error' && (
            <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-4 py-3">
              <p className="text-sm font-semibold text-red-800">Analysis failed</p>
              <p className="mt-0.5 text-xs text-red-700">{analyzeStatus.message}</p>
            </div>
          )}
        </>
      )}

      {/* ── Batch tab ──────────────────────────────────────────────────────── */}
      {tab === 'batch' && (
        <>
          {/* File picker */}
          <div>
            <input
              ref={batchFileInputRef}
              type="file"
              id="batch-file-input"
              multiple
              accept=".pdf,.png,.jpg,.jpeg,image/png,image/jpeg,application/pdf"
              className="hidden"
              onChange={handleBatchFilesSelected}
            />
            <label
              htmlFor="batch-file-input"
              className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-gray-300 bg-gray-50 px-6 py-8 text-center transition-colors hover:border-blue-400 hover:bg-blue-50"
            >
              <svg className="h-8 w-8 text-gray-400" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24" aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 16.5V9.75m0 0l-3 3m3-3l3 3M6.75 19.5a4.5 4.5 0 01-1.41-8.775 5.25 5.25 0 0110.233-2.33 3 3 0 013.758 3.848A3.752 3.752 0 0118 19.5H6.75z" />
              </svg>
              <span className="text-sm font-semibold text-gray-700">Click to browse files</span>
              <span className="text-xs text-gray-400">PDF, PNG, JPEG — multiple files allowed</span>
            </label>
          </div>

          {/* Queue list */}
          {queuedFiles.length > 0 && (
            <div className="flex flex-col gap-2">
              {queuedFiles.map((qf) => (
                <div key={qf.id} className="rounded-xl border border-gray-200 bg-white px-4 py-3">
                  <div className="flex items-center gap-3">
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium text-gray-800">{qf.file.name}</p>
                      <p className="text-xs text-gray-400">{(qf.file.size / 1024 / 1024).toFixed(2)} MB</p>
                    </div>
                    {qf.status === 'idle' && (
                      <span className="text-xs font-medium text-gray-400">Waiting</span>
                    )}
                    {qf.status === 'streaming' && (
                      <svg aria-hidden="true" className="h-4 w-4 shrink-0 animate-spin text-blue-500" fill="none" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                      </svg>
                    )}
                    {qf.status === 'done' && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2 py-0.5 text-xs font-semibold text-green-800 ring-1 ring-inset ring-green-200">
                        <svg className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                          <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                        </svg>
                        Done
                      </span>
                    )}
                    {qf.status === 'error' && (
                      <span className="rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-800 ring-1 ring-inset ring-red-200">
                        Error
                      </span>
                    )}
                    {qf.status === 'idle' && !batchRunning && (
                      <button
                        type="button"
                        aria-label={`Remove ${qf.file.name} from queue`}
                        onClick={() => handleRemoveQueuedFile(qf.id)}
                        className="shrink-0 rounded px-2 py-0.5 text-xs text-gray-400 hover:text-red-500"
                      >
                        ✕
                      </button>
                    )}
                  </div>

                  {/* SSE steps while streaming */}
                  {qf.status === 'streaming' && qf.steps.length > 0 && (
                    <ul className="mt-2 flex flex-col gap-1 pl-1">
                      {qf.steps.map((step, i) => (
                        <li key={i} className="flex items-center gap-1.5 text-xs text-blue-700">
                          <svg className="h-3 w-3 shrink-0 text-blue-400" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                            <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                          </svg>
                          {step}
                        </li>
                      ))}
                    </ul>
                  )}

                  {/* Done: link to results */}
                  {qf.status === 'done' && qf.documentId !== undefined && (
                    <a
                      href={`/results/${qf.documentId}`}
                      className="mt-1 block text-xs font-medium text-blue-600 hover:text-blue-800"
                    >
                      View Results →
                    </a>
                  )}

                  {/* Error message */}
                  {qf.status === 'error' && qf.errorMessage && (
                    <p className="mt-1 text-xs text-red-600">{qf.errorMessage}</p>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* Language + Analyze row */}
          <div className="flex flex-wrap items-end gap-3">
            {!handwriting && (
              <div className="flex flex-col gap-1">
                <label htmlFor="batch-ocr-lang" className="text-xs font-medium text-gray-500">Language</label>
                <select id="batch-ocr-lang" value={lang} onChange={(e) => setLang(e.target.value)}
                  className="rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500">
                  {LANGUAGES.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}
                </select>
              </div>
            )}
            <button
              type="button"
              disabled={!canBatchStart}
              onClick={handleBatchAnalyze}
              className="rounded-xl bg-blue-600 px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
            >
              {batchRunning ? (
                <span className="flex items-center gap-2">
                  <svg aria-hidden="true" className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                  </svg>
                  Analyzing…
                </span>
              ) : (
                `Analyze ${idleCount > 0 ? idleCount : ''} Document${idleCount !== 1 ? 's' : ''}`
              )}
            </button>
          </div>
        </>
      )}
    </div>
  );
```

- [ ] **Step 6: Verify TypeScript**

```bash
cd frontend && npx tsc --noEmit
```
Expected: No errors.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/components/upload/UploadClient.tsx
git commit -m "feat(frontend): add Single/Batch tabs, Handwriting toggle, and batch queue to UploadClient"
```
