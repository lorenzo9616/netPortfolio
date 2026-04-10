---
name: OpenOCR Document History Page
description: A browse page listing all past analysis results with pagination, view, and delete actions — no bookmarking URLs required
type: project
---

# OpenOCR — Document History Design

**Date:** 2026-04-10  
**Status:** Approved  
**Audience:** Marketing product testers browsing past OCR results

---

## Problem

Once a document is analyzed, the only way to view the result is to remember the URL (`/results/{id}`). Testers have no way to browse past results, compare runs, or clean up test data without database access.

---

## Goals

- Testers can browse all past analysis results from a dedicated page.
- Each result shows enough context (date, page count, field count, text preview) to identify it without opening it.
- Testers can open any past result or delete test runs they don't need.
- Empty state is friendly — guides new testers to upload their first document.

---

## Architecture

The `AnalysisResult` rows already exist in PostgreSQL. This feature adds one new backend list endpoint and one new frontend page. No schema changes required.

---

## Components

### 1. Backend — `GET /api/documents`

New endpoint on the existing `DocumentsController`:

**Query parameters:**
- `page` (int, default: 1)
- `pageSize` (int, default: 20, max: 100)

**Response:**
```json
{
  "items": [
    {
      "documentId": 42,
      "createdAt": "2026-04-10T14:32:00Z",
      "pageCount": 2,
      "fieldCount": 5,
      "preview": "Invoice INV-00423 dated 03/15/2026 Total $1,234.56..."
    }
  ],
  "totalCount": 87,
  "page": 1,
  "pageSize": 20
}
```

`preview` is the first 120 characters of `raw_text`, trimmed at a word boundary.

Items are sorted by `createdAt` descending (newest first).

### 2. Backend — `DELETE /api/documents/{id}`

New endpoint on `DocumentsController`. Deletes the `AnalysisResult` row and its associated `SavedField` rows (cascade). Returns `204 No Content` on success, `404` if not found.

### 3. Frontend — `api.ts` — New Functions

```typescript
export async function listDocuments(page?: number): Promise<DocumentHistoryPage> { ... }
export async function deleteDocument(id: number): Promise<void> { ... }
```

New type `DocumentHistoryPage` added to `types/ocr.ts`.

### 4. Frontend — `/history` Page

New page at `frontend/src/app/history/page.tsx` with a client component `HistoryClient.tsx`.

**Layout:**

- Page title: "Document History"
- Results displayed as a table on desktop, stacked cards on mobile.
- Each row/card shows: date (formatted as "Apr 10, 2026 at 2:32 PM"), page count, field count, text preview (truncated with ellipsis), and two action buttons: "View" and "Delete".

**Pagination:**

- Previous / Next buttons at the bottom.
- "Showing 1–20 of 87 results" count label.
- Buttons disabled when at the first/last page.

**Delete flow:**

- Clicking "Delete" shows a confirm dialog (`window.confirm` — no custom modal needed for this phase): "Delete this result? This cannot be undone."
- On confirm: calls `deleteDocument(id)`, removes the row from the list optimistically, shows a brief "Deleted" toast notification.
- On error: shows an inline error message on the row.

**Empty state:**

When `totalCount === 0`:
```
No documents analyzed yet.
Upload your first document to get started. → [Upload Document] button
```

**Loading state:** Skeleton rows while the first page loads.

**Error state:** "Could not load history. Try refreshing the page." with a Retry button.

### 5. Frontend — Sidebar

Add "History" link to `Sidebar.tsx` between "Properties" and "Upload":

```
Properties
History       ← new
Upload
```

---

## Data Flow

```
User navigates to /history
  → HistoryClient fetches GET /api/documents?page=1
  → Renders table of results

User clicks "View"
  → navigate to /results/{documentId}  (existing page, no changes)

User clicks "Delete" → confirms
  → DELETE /api/documents/{id}
  → Row removed from list; total count decremented
  → If page is now empty and page > 1, navigate to page - 1

User clicks Next/Previous
  → fetch GET /api/documents?page=N
  → Re-render table
```

---

## Error Handling

- `GET /api/documents` fails: show error state with retry button.
- `DELETE /api/documents/{id}` fails: show inline error on the row, row is not removed.
- `documentId` not found when navigating to `/results/{id}` from history: existing results page already handles this with a 404 state.

---

## Testing

- Backend unit test: `GET /api/documents` returns correct pagination metadata and preview truncation.
- Backend unit test: `DELETE /api/documents/{id}` cascades to `SavedField` rows.
- Frontend component test: empty state renders correctly; delete confirm flow works.
- E2E (Playwright): upload a document → navigate to History → verify it appears → delete it → verify it disappears (see Testing spec).

---

## Out of Scope

- Search or filter by date/field value (add after testers identify whether they need it).
- Bulk delete.
- Export to CSV.
- Sorting by columns other than date.
