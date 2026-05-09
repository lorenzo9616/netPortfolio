# Design: Batch Upload + Handwriting / ICR

**Date:** 2026-05-10
**Status:** Approved

---

## Overview

Two features added to the OpenOCR portfolio system:

1. **Batch Upload** — analyze multiple documents in one session from the existing `/upload` page. Files are processed sequentially using the existing SSE streaming endpoint. Results stay on the upload page as per-file summary cards.
2. **Handwriting / ICR** — a mode toggle that routes documents through Claude Haiku Vision instead of Tesseract. The transcription feeds into the existing classifier + field matcher pipeline unchanged.
3. **Extracted Fields Table Redesign** — visual-only improvement to the fields display in `ResultsClient.tsx`.

---

## Architecture

### Batch Upload (frontend-driven)

The upload page gains a tab bar: **Single Document** | **Batch Upload**. The batch tab loops over queued files and calls the existing `POST /api/documents/analyze/stream` sequentially — one file at a time. The server is unaware it is processing a batch; no new backend endpoint is required.

### Handwriting / ICR

A **Handwriting mode** toggle below the tab bar appends `mode=handwriting` to the form data on submit. The .NET `AnalyzeStream` action reads this param and forks:

```
mode=ocr  (default) → _ocrClient.ExtractTextStreamAsync → existing path
mode=handwriting    → HandwritingExtractorService
                         → ExtractPagesAsync (/extract-pages)
                         → Claude Haiku Vision per page
                         → rawText → classifier → field matcher → save
```

Text blocks, bounding boxes, and table blocks are empty for handwriting results.

---

## Feature 1 — Batch Upload

### Frontend State Machine

`UploadClient` gains a new `mode` tab state (`'single' | 'batch'`) and a `files: QueuedFile[]` array for batch mode.

```typescript
interface QueuedFile {
  id: string;            // uuid, key for React list
  file: File;
  status: 'idle' | 'streaming' | 'done' | 'error';
  steps: string[];       // SSE progress messages
  documentId?: number;
  errorMessage?: string;
}
```

Single tab retains the existing reducer state unchanged.

### UI Flow

1. Batch tab dropzone accepts multiple files (same allowed types as single upload).
2. Selected files appear in a queue list: filename + size + `Waiting` badge.
3. **"Analyze N Documents"** button starts sequential processing.
4. The actively-processing file shows its live SSE step list expanded; completed files collapse to a single status line.
5. When all files finish, each row shows:
   - Green **Done** badge + detected document type + field count + **"View Results →"** link
   - Or red **Error** badge + error message
6. No redirect. User can open individual results in new tabs or re-upload failed files.

### Sequential Loop

```typescript
for (const queuedFile of files) {
  setFileStatus(queuedFile.id, 'streaming');
  try {
    const { documentId } = await analyzeDocumentStream(
      queuedFile.file, undefined, lang, handwritingMode,
      (message) => appendStep(queuedFile.id, message),
    );
    setFileDone(queuedFile.id, documentId);
  } catch (err) {
    setFileError(queuedFile.id, err.message);
    // continue to next file
  }
}
```

### `analyzeDocumentStream` signature update

Add `mode: 'ocr' | 'handwriting'` parameter. Appends `mode` to `FormData`.

---

## Feature 2 — Handwriting / ICR

### UI

A toggle row beneath the tab bar:

```
[ ] Handwriting / ICR mode  —  uses Claude Vision for handwritten documents
```

Active in both Single and Batch tabs. When enabled, the language selector is hidden (Claude handles language detection automatically).

### New Python Endpoint `/extract-pages`

**File:** `ocr-service/main.py`

Accepts a file upload. Converts PDF→images (reusing existing `pdf_to_images`) or wraps a single image. Returns:

```python
class ExtractPagesResponse(BaseModel):
    page_count: int
    page_images: list[str]   # base64-encoded PNG per page
```

No OCR, no signature detection, no table detection.

New model added to `ocr-service/models.py`.

### New `IOcrClient` method `ExtractPagesAsync`

**File:** `backend/Services/IOcrClient.cs` + `OcrClient.cs`

```csharp
Task<ExtractPagesResponse> ExtractPagesAsync(
    Stream fileStream,
    string fileName,
    string contentType,
    CancellationToken ct = default);
```

Posts to `/extract-pages`, deserializes `ExtractPagesResponse`.

New `ExtractPagesResponse` C# model added to `backend/Models/DocumentModels.cs`.

### New `IHandwritingExtractorService` + `HandwritingExtractorService`

**Files:** `backend/Services/IHandwritingExtractorService.cs` + `backend/Services/HandwritingExtractorService.cs`

Follows the `IOcrClient` / `OcrClient` pattern. Registered as scoped. Reads `ANTHROPIC_API_KEY` via `IConfiguration` (same as `DocumentClassifierService`).

```
1. Call ExtractPagesAsync → list<string> pageImages
2. For each page image (base64 PNG):
   POST to Anthropic claude-haiku-4-5-20251001 with vision:
   System: "You are a transcription assistant."
   User:   [image] + "Transcribe all text exactly as written.
            Preserve line breaks. Return plain text only."
3. Concatenate page transcriptions with "\n\n--- Page N ---\n\n" separators
4. Return HandwritingExtractionResult { RawText, PageImages, PageCount }
```

On any Anthropic API failure (exception or non-200): log warning, return `HandwritingExtractionResult { RawText = "", PageImages = pageImages, PageCount }` so the file is still saved with empty text. Analysis continues — field extraction returns empty, classifier returns "Unknown".

### `AnalyzeDocumentRequest` update

Add `string Mode { get; set; } = "ocr";` to the existing request model.

### `AnalyzeStream` fork

After file validation, read `request.Mode`.

**`"ocr"` path:** existing SSE proxy path unchanged.

**`"handwriting"` path:** bypasses the OCR service SSE proxy entirely. Has its own inline save block:

```csharp
await WriteEventAsync("status", new { step = "handwriting", message = "Sending pages to Claude Vision..." });
var hwResult = await _handwritingService.ExtractAsync(fileStream, fileName, contentType, ct);
await WriteEventAsync("status", new { step = "handwriting", message = $"Transcription complete — {hwResult.PageCount} page(s)" });
await WriteEventAsync("status", new { step = "classifying", message = "Classifying document type..." });
var classification = await _classifierService.ClassifyAsync(hwResult.RawText);
await WriteEventAsync("status", new { step = "saving", message = "Saving results to database" });
var extractedFields = _fieldMatcher.MatchFields(activeProperties.Concat(tempProps), hwResult.RawText, []);
// build AnalysisResult: TextBlocks = [], TableBlocksJson = null
// add DocumentPages from hwResult.PageImages
// _dbContext.AnalysisResults.Add(analysisResult); SaveChangesAsync
await WriteEventAsync("done", new { documentId = analysisResult.Id });
```

SSE step messages reach the frontend unchanged — `UploadClient` step list renders them naturally.

---

## Feature 3 — Extracted Fields Table Redesign

**File:** `frontend/src/components/results/ResultsClient.tsx` (and extracted to a new `ExtractedFieldsTable.tsx` component)

### Layout

Three-column layout replacing the current flat list:

| Field Name | Extracted Value | Confidence |
|---|---|---|
| Invoice Number | INV-0042 | ● 91% |
| Total Amount | 1,240.00 | ● 87% |

- **Field name**: left column, muted label style (`text-gray-500 text-xs font-medium uppercase tracking-wide`)
- **Extracted value**: center column, bold (`text-gray-800 font-medium`)
- **Confidence badge**: right column, right-aligned, color-coded dot + percentage (green ≥80%, yellow 50–80%, red <50%)
- **Manual override input**: inline, revealed on row hover/focus — a small edit icon triggers an `<input>` replacing the value display; reduces visual noise when not editing
- **Empty fields** `<details>` disclosure: same three-column sub-table, consistent styling

---

## Data Model

No new database columns or migrations required.

- Handwriting documents: `TextBlocks` = empty list, `TableBlocksJson` = null, `SignatureImage` = null.
- `DocumentType` from the classifier labels handwriting results naturally (e.g. "Handwritten Note").
- `RecognitionMode` is **not** stored — document type is sufficient for display and filtering.

---

## Error Handling

| Scenario | Behaviour |
|---|---|
| Claude Vision failure (timeout / rate limit) | `HandwritingExtractorService` catches, returns empty `RawText`. Analysis continues with empty fields; result still saved. |
| `/extract-pages` failure (corrupt PDF) | .NET emits SSE `error` event. In batch: file marked failed, remaining files continue. |
| One file in batch fails | Red "Error" badge on that row; other files unaffected. |
| All batch files fail | All rows show red; no redirect; user re-uploads. |
| Handwriting mode on typed PDF | Works correctly — Claude Vision handles printed text. |
| Empty handwriting transcription | Field extraction returns empty list; result saved with `RawText = ""`; user sees zero extracted fields. |

---

## Files Changed

### Created
- `backend/Services/IHandwritingExtractorService.cs`
- `backend/Services/HandwritingExtractorService.cs`
- `frontend/src/components/results/ExtractedFieldsTable.tsx`

### Modified
- `ocr-service/main.py` — add `/extract-pages` endpoint
- `ocr-service/models.py` — add `ExtractPagesResponse`
- `backend/Models/DocumentModels.cs` — add `ExtractPagesResponse`, `HandwritingExtractionResult`
- `backend/Services/IOcrClient.cs` — add `ExtractPagesAsync`
- `backend/Services/OcrClient.cs` — implement `ExtractPagesAsync`
- `backend/Models/AnalyzeDocumentRequest.cs` (or inline in controller) — add `Mode`
- `backend/Controllers/DocumentsController.cs` — fork on `Mode`, wire `HandwritingExtractorService`
- `frontend/src/lib/api.ts` — add `mode` param to `analyzeDocumentStream`
- `frontend/src/types/ocr.ts` — no changes needed
- `frontend/src/components/upload/UploadClient.tsx` — tabs, batch queue, handwriting toggle
- `frontend/src/components/results/ResultsClient.tsx` — use `ExtractedFieldsTable`

---

## Out of Scope

- Storing recognition mode as a database column
- Per-page handwriting confidence scores
- Batch result grouping / batch ID concept
- Pagination on the batch queue (assume reasonable file counts, e.g. ≤20)
- Retrying individual failed files from the batch queue UI (user re-uploads)
