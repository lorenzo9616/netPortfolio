# Design: Empty Fields Collapse + AI Auto-Properties + Analyzed Documents Page

**Date:** 2026-05-05
**Status:** Approved

---

## Overview

Three features added to the OCR portfolio system:

1. **Empty Fields Dropdown** — In the results view, fields with no extracted value are collapsed to a disclosure section at the bottom of the fields table, reducing visual clutter.
2. **AI Auto-Temporary Properties** — When a document is analyzed, Claude classifies the document type from raw OCR text and injects a set of temporary extraction properties for that analysis only. These are never persisted to the OcrProperty table.
3. **Analyzed Documents Page** — A new `/documents` page listing every analyzed document with its extracted fields, confidence scores, and a link to the full results view.

---

## Feature 1 — Empty Fields Collapsed to Dropdown

### Scope
Frontend only. No backend or data model changes.

### Behavior
- `ResultsClient.tsx` splits `extractedFields` into two arrays at render time:
  - **filledFields**: `extractedValue` is non-null and non-empty string
  - **emptyFields**: `extractedValue` is null or empty string
- `filledFields` render in the existing table layout (field name, extracted value, confidence badge, manual override input), unchanged.
- Below the main table, a `<details>` disclosure element shows the label `"{N} empty fields"` in its `<summary>`. When expanded, `emptyFields` render in an identical sub-table. Manual override inputs remain active so users can still fill empty fields by hand.
- If there are zero empty fields, the disclosure element is not rendered.

### Files Changed
- `frontend/src/components/results/ResultsClient.tsx`

---

## Feature 2 — AI Auto-Temporary Properties

### Architecture
Approach A: backend-integrated. The classification and temporary property injection happen inside the existing `.NET` analysis pipeline, after the OCR service returns raw text and before `FieldMatcher` runs.

### Backend Pipeline Addition

**New service:** `DocumentClassifierService.cs`

Responsibility: given raw OCR text, call Claude API and return a `ClassificationResult`:

```csharp
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
```

**Claude call details:**
- Model: `claude-haiku-4-5-20251001` (fast, low cost)
- Input: first 2000 characters of `rawText`
- System prompt instructs Claude to return valid JSON only
- Prompt asks for document type and a list of suggested extraction properties (name, dataType, searchHeuristic, isRegex)
- Response is deserialized; on parse failure the service returns an empty result with `DocumentType = "Unknown"` — analysis continues unaffected

**Integration point in `DocumentsController.cs` / `OcrService.cs`:**

```
1. OCR service returns ExtractTextResponse (raw text, text blocks, pages)
2. → DocumentClassifierService.ClassifyAsync(rawText)            [NEW]
3. → SSE event: "Classifying document type with AI..."           [NEW]
4. → SSE event: "{Type} detected — adding {N} temporary fields"  [NEW]
5. → FieldMatcher.ExtractFields(textBlocks, dbProperties + tempProperties)
6. → Save AnalysisResult (DocumentType column populated)          [NEW]
```

**FieldMatcher change:** the method signature accepts `IEnumerable<OcrProperty>` — callers already pass the DB list. The controller now passes `dbProperties.Concat(tempProperties)`. Temporary properties are `OcrProperty`-shaped in-memory objects with `Id = 0` and `IsActive = true`; they are never passed to any repository.

**Data model change:**
- New nullable `string? DocumentType` column on `AnalysisResult`
- New EF migration required
- `AnalysisResultDetail` DTO gains a `documentType` field

**Frontend streaming:**
- Two new SSE event types handled in `UploadClient.tsx`: `classifying` and `type_detected`
- Results metadata row added: **Document Type** shown alongside File Name and Analyzed Date

### Files Changed
- `backend/Services/DocumentClassifierService.cs` (new)
- `backend/Services/OcrService.cs` or `DocumentsController.cs` (integration)
- `backend/Services/FieldMatcher.cs` (signature update)
- `backend/Models/AnalysisResult.cs` (new column)
- `backend/Data/OcrDbContext.cs` (model config)
- `backend/Migrations/` (new migration)
- `backend/Controllers/DocumentsController.cs` (pass temp properties, SSE events)
- `frontend/src/components/upload/UploadClient.tsx` (new SSE event types)
- `frontend/src/components/results/ResultsClient.tsx` (show documentType)
- `frontend/src/types/ocr.ts` (add documentType to AnalysisResultDetail)

---

## Feature 3 — Analyzed Documents Page

### New Backend Endpoint

`GET /api/documents`

Returns all analysis results, newest first. Empty fields (null or empty `extractedValue`) are excluded from the field list in this endpoint — the page is a summary view, not a full audit.

Response shape:
```json
[
  {
    "documentId": 42,
    "fileName": "invoice_march.pdf",
    "analyzedAt": "2026-05-05T14:32:00Z",
    "documentType": "Invoice",
    "fields": [
      { "propertyName": "Invoice Number", "extractedValue": "INV-0042", "confidence": 0.91 },
      { "propertyName": "Total Amount", "extractedValue": "1,240.00", "confidence": 0.87 }
    ]
  }
]
```

### New Frontend Page

**Route:** `frontend/src/app/documents/page.tsx`

**Layout:**
- Page heading: "Analyzed Documents"
- Top-level table with columns: **Document Name | Type | Analyzed Date | Fields Extracted**
- Each row is clickable / expandable via an accordion. Expanding a row reveals a nested sub-table: **Field Name | Extracted Value | Confidence** (confidence uses the same color-coded badge as the results page: green ≥ 0.8, yellow 0.5–0.8, red < 0.5)
- Bottom-right of each expanded row: "View Full Results →" link to `/results/{documentId}`
- Empty state: "No documents analyzed yet." with a link to the upload page
- Data is fetched client-side on mount via the existing `api.ts` pattern

### New API Function

`getAnalyzedDocuments()` — `GET /api/documents` added to `frontend/src/lib/api.ts`

### New Type

`AnalyzedDocumentSummary` added to `frontend/src/types/ocr.ts`

### Navigation

A "Analyzed Documents" link added to the main nav (wherever the current nav lives in `layout.tsx`).

### Files Changed
- `backend/Controllers/DocumentsController.cs` (new GET list endpoint)
- `frontend/src/app/documents/page.tsx` (new page, new file)
- `frontend/src/lib/api.ts` (new function)
- `frontend/src/types/ocr.ts` (new type)
- `frontend/src/app/layout.tsx` (nav link)

---

## Error Handling

- **Claude API failure** (network error, rate limit, invalid JSON): `DocumentClassifierService` catches all exceptions, logs a warning, and returns `ClassificationResult("Unknown", [])`. Analysis proceeds with only DB properties — user experience is unaffected.
- **GET /api/documents failure**: the Analyzed Documents page shows an error banner; does not crash.
- **Zero properties (DB + temporary)**: `FieldMatcher` returns an empty list; existing behavior, no change.

---

## Out of Scope

- Persisting AI-suggested properties as permanent OcrProperties (user can add them manually via the existing properties UI if desired)
- Pagination on the Analyzed Documents page (add later if the list grows large)
- Deleting documents from the Analyzed Documents page
