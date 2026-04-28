# OCR App — Signature Capture, OCR Text Table & Save Fix

**Date:** 2026-04-28  
**Branch target:** main  
**Scope:** Four improvements to the existing OpenOCR system

---

## 1. Goals

1. **Signature capture** — After analyzing a document, the user draws a bounding box on the document image on the results page. The selected region is cropped and stored as the signature screenshot, displayed inline in the Signature field row.
2. **Document image preview** — Store the uploaded image server-side so the results page can display it instead of the current placeholder.
3. **OCR text blocks table** — Persist per-word Tesseract output and display it as a structured table (Text | Confidence | Page | Position) on the results page.
4. **Save Changes fix** — Fix the 500 error caused by JSON circular references when returning EF entities from `PUT /api/documents/{id}/fields`.

---

## 2. Architecture Overview

No new services. Changes span three layers:

```
Frontend (Next.js)
  └── ResultsClient.tsx    — image canvas, signature draw, text blocks table
  └── api.ts               — new captureSignature() call; extended getAnalysisResult()
  └── types/ocr.ts         — new OcrTextBlock interface; extended AnalysisResultDetail

Backend (.NET 8)
  └── Models/              — AnalysisResult gains ImageBytes + SignatureImage; new OcrTextBlock model
  └── Data/OcrDbContext.cs  — new ocr_text_blocks table; updated AnalysisResult mapping
  └── Controllers/DocumentsController.cs
      ├── POST /analyze     — buffer file bytes → store ImageBytes; map text_blocks → OcrTextBlock entities
      ├── GET  /{id}/image  — stream ImageBytes
      ├── PATCH/{id}/signature — decode base64 → store SignatureImage
      └── PUT  /{id}/fields — return DTO (fix circular ref)
  └── Migrations/          — one new migration

OCR Service (Python FastAPI)
  └── No changes (already returns text_blocks)
```

---

## 3. Database Schema

### 3.1 `analysis_results` table — new columns

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `image_bytes` | bytea | yes | Raw uploaded file bytes |
| `signature_image` | bytea | yes | User-cropped PNG, set later via PATCH |

### 3.2 New `ocr_text_blocks` table

| Column | Type | Notes |
|---|---|---|
| `id` | serial PK | |
| `analysis_result_id` | int FK | → `analysis_results.id`, cascade delete |
| `text` | varchar(500) | Single word/token from Tesseract |
| `confidence` | float | 0–100 Tesseract confidence scale |
| `page` | int | 1-based page number |
| `bbox_x` | int | Pixel x origin |
| `bbox_y` | int | Pixel y origin |
| `bbox_width` | int | Pixel width |
| `bbox_height` | int | Pixel height |

Delivered via a single EF migration.

---

## 4. Backend API

### 4.1 `POST /api/documents/analyze` — changes

Current: reads file stream once, passes to OcrClient.  
New:
1. Buffer file bytes into a `byte[]` before calling OcrClient (so bytes can be stored).
2. Set `analysisResult.ImageBytes = fileBytes`.
3. Map `ocrResult.TextBlocks` → `List<OcrTextBlock>`, assign to `analysisResult.TextBlocks`.
4. The response DTO gains a `textBlocks` array (same shape as the DB model).

### 4.2 `GET /api/documents/{id}` — response change

`AnalysisResultDetailDto` gains:
```json
{
  "textBlocks": [
    { "text": "John", "confidence": 94.2, "page": 1, "bboxX": 12, "bboxY": 45, "bboxWidth": 40, "bboxHeight": 14 }
  ]
}
```
`signatureImage` (nullable base64 string) is also included so the results page can show a previously captured signature on load.

### 4.3 `GET /api/documents/{id}/image` — new endpoint

- Returns 404 if document not found or `ImageBytes` is null.
- Detects content-type from the file extension in `FileName`: `.png` → `image/png`, `.jpg`/`.jpeg` → `image/jpeg`, `.pdf` → `application/pdf`, anything else → `application/octet-stream`.
- Streams bytes with `File(bytes, contentType)`.

### 4.4 `PATCH /api/documents/{id}/signature` — new endpoint

Request body:
```json
{ "imageData": "<base64-encoded PNG>" }
```
- Decodes base64 → stores as `SignatureImage` bytea.
- Returns `{ "imageData": "<same base64>" }` so the frontend can display it immediately without a second fetch.
- Returns 400 if `imageData` is missing or not valid base64.
- Returns 404 if document not found.

### 4.5 `PUT /api/documents/{id}/fields` — fix

Currently returns `Ok(analysisResult.Fields)` — the loaded EF entities include the `AnalysisResult` navigation property, causing a JSON circular reference and a 500.

Fix: map each `SavedField` → `ExtractedFieldDto` before returning:
```csharp
return Ok(analysisResult.Fields.Select(f => new ExtractedFieldDto {
    PropertyName   = f.PropertyName,
    ExtractedValue = f.ExtractedValue,
    ManualOverride = f.ManualOverride,
    Confidence     = f.Confidence,
}));
```

---

## 5. Frontend

### 5.1 `types/ocr.ts`

Add:
```ts
export interface OcrTextBlock {
  text: string;
  confidence: number;
  page: number;
  bboxX: number;
  bboxY: number;
  bboxWidth: number;
  bboxHeight: number;
}
```

Extend `AnalysisResultDetail`:
```ts
textBlocks: OcrTextBlock[];
signatureImage: string | null;   // base64 PNG, null if not yet captured
```

### 5.2 `api.ts`

Add:
```ts
export async function captureSignature(
  documentId: number,
  imageData: string,  // base64 PNG
): Promise<{ imageData: string }>
```

Uses `PATCH /api/documents/{documentId}/signature`.

### 5.3 `ResultsClient.tsx` — right panel

Replace the "Image preview not available" placeholder with:

1. **Document image + canvas overlay**
   - Fetch image via `GET /api/documents/{id}/image`; render in an `<img>` inside a `relative` container.
   - A `<canvas>` positioned absolutely on top captures `mousedown`/`mousemove`/`mouseup` events and draws a red dashed rectangle while dragging.
   - Once a valid rectangle exists, a "Capture Signature" button appears below.

2. **Signature capture flow**
   - The overlay canvas and the `<img>` are the same CSS size, so the drawn rectangle is in display-space pixels. Before cropping, scale to natural-image coordinates: `naturalX = drawX * (img.naturalWidth / img.clientWidth)` (same formula for y, width, height).
   - On "Capture Signature" click: create an off-screen canvas sized to the natural-coordinate crop, draw the full image (`drawImage(img, -naturalX, -naturalY, img.naturalWidth, img.naturalHeight)`), call `canvas.toDataURL('image/png')` to get base64.
   - Call `captureSignature(documentId, base64)`.
   - On success: store the returned base64 in component state as `signatureImage`.

3. **Signature field row**
   - When `signatureImage` is non-null (either from load or from a fresh capture), the "Signature" row in the extracted fields table shows `<img src={...} />` instead of the text input.
   - Use `data:image/png;base64,...` as the `src`.

### 5.4 `ResultsClient.tsx` — OCR text blocks table

Below the "Extracted Fields" section, add a new collapsible section "Raw OCR Tokens" (default: expanded).

Table columns:

| # | Text | Confidence | Page | Position |
|---|---|---|---|---|
| 1 | John | 94% (green) | 1 | (12, 45) |

- Reuse the existing `ConfidenceBadge` component.
- Confidence values from Tesseract are 0–100; divide by 100 to pass into `ConfidenceBadge`.
- Position rendered as `(x, y)` compact string.
- Table wrapped in `max-h-96 overflow-y-auto` scroll container.
- If `textBlocks` is empty, show a "No OCR tokens found" placeholder.

---

## 6. Error Handling

| Scenario | Behavior |
|---|---|
| Image fetch fails (e.g. old document with no `image_bytes`) | Right panel shows a "Preview unavailable" placeholder — no crash |
| Signature PATCH fails | Error message shown below the Capture button; selection cleared |
| No OCR blocks returned | "No OCR tokens found" message in the table section |
| Save Changes (after fix) | Success / error banners already wired — no change needed |

---

## 7. Out of Scope

- Automatic signature region detection (no ML model)
- Multiple signature regions per document
- Image storage migration for existing documents (old rows simply have `image_bytes = null`)
- Any changes to the OCR service

---

## 8. File Change Summary

| File | Change |
|---|---|
| `backend/Models/AnalysisResult.cs` | Add `ImageBytes`, `SignatureImage`, `TextBlocks` nav |
| `backend/Models/OcrTextBlock.cs` | New entity |
| `backend/Data/OcrDbContext.cs` | Map `OcrTextBlock` table; update `AnalysisResult` mapping |
| `backend/Migrations/` | One new migration |
| `backend/Controllers/DocumentsController.cs` | Buffer file bytes; store image+blocks; new image/signature endpoints; fix UpdateFields DTO |
| `frontend/src/types/ocr.ts` | Add `OcrTextBlock`; extend `AnalysisResultDetail` |
| `frontend/src/lib/api.ts` | Add `captureSignature()` |
| `frontend/src/components/results/ResultsClient.tsx` | Canvas overlay; signature capture; OCR table |
