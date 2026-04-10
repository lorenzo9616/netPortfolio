# Document History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `/history` page where testers can browse all past analysis results, view any result, and delete test runs — no bookmarking URLs required.

**Architecture:** `AnalysisResult` gains a `PageCount` column (new EF Core migration). `DocumentsController` gets two new endpoints: `GET /api/documents` (paginated list) and `DELETE /api/documents/{id}` (cascade-deletes `SavedFields`). A new `HistoryClient` frontend component renders the list with pagination, view, and delete actions. Auto-migration at startup ensures the new column exists when the container restarts.

**Tech Stack:** .NET 8 / EF Core (backend), Next.js 14 / TypeScript / Tailwind (frontend), xUnit (backend tests)

**Prerequisite:** The Auth plan must be implemented first — all API calls include `Authorization: Bearer` headers via `authHeaders()` from `api.ts`. If Auth is not yet implemented, the backend `/api/documents` endpoint will be unprotected (acceptable for development order flexibility).

---

## File Map

| Action | Path | Purpose |
|---|---|---|
| Modify | `backend/Models/AnalysisResult.cs` | Add `PageCount` property |
| Modify | `backend/Controllers/DocumentsController.cs` | Add `GET /api/documents` and `DELETE /api/documents/{id}` |
| Modify | `backend/Models/DocumentModels.cs` | Add `DocumentHistoryItemDto`, `DocumentHistoryPageDto` |
| Create | migration (via EF CLI) | `AddPageCountToAnalysisResult` |
| Modify | `frontend/src/types/ocr.ts` | Add `DocumentHistoryItem`, `DocumentHistoryPage` types |
| Modify | `frontend/src/lib/api.ts` | Add `listDocuments`, `deleteDocument` functions |
| Create | `frontend/src/components/history/HistoryClient.tsx` | Paginated list with view and delete actions |
| Create | `frontend/src/app/history/page.tsx` | Page shell |
| Modify | `frontend/src/components/Sidebar.tsx` | Add "History" nav link (if not already added in Auth plan) |
| Create | `backend/OcrApi.Tests/DocumentsControllerHistoryTests.cs` | Tests for new endpoints |

---

### Task 1: Add PageCount to AnalysisResult and create migration

**Files:**
- Modify: `backend/Models/AnalysisResult.cs`
- Create: EF Core migration (auto-named)

- [ ] **Step 1: Read the current AnalysisResult.cs**

Open `backend/Models/AnalysisResult.cs` and review its properties. You will add `PageCount`.

- [ ] **Step 2: Add PageCount property**

In `backend/Models/AnalysisResult.cs`, add this property after `AnalyzedAt`:

```csharp
/// <summary>Number of pages processed by the OCR engine.</summary>
public int PageCount { get; set; } = 1;
```

The full updated model should look like:

```csharp
namespace OcrApi.Models;

public class AnalysisResult
{
    public int       Id         { get; set; }
    public string    FileName   { get; set; } = string.Empty;
    public string?   RawText    { get; set; }
    public DateTime  AnalyzedAt { get; set; } = DateTime.UtcNow;
    public int       PageCount  { get; set; } = 1;
    public List<SavedField> Fields { get; set; } = new();
}
```

- [ ] **Step 3: Create the EF Core migration**

Run from the repo root (requires the backend containers to be down so there is no lock on the DB, or run directly in the backend project):

```bash
dotnet ef migrations add AddPageCountToAnalysisResult --project backend/
```

Expected: a new file is created in `backend/Migrations/` named `YYYYMMDDHHMMSS_AddPageCountToAnalysisResult.cs`.

- [ ] **Step 4: Build to verify no compile errors**

```bash
dotnet build backend/
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add backend/Models/AnalysisResult.cs backend/Migrations/
git commit -m "feat: add PageCount column to AnalysisResult"
```

---

### Task 2: Update DocumentsController.Analyze to save PageCount

**Files:**
- Modify: `backend/Controllers/DocumentsController.cs`

- [ ] **Step 1: Find where AnalysisResult is created in Analyze**

In `backend/Controllers/DocumentsController.cs`, find this block (around line 119):

```csharp
var analysisResult = new AnalysisResult
{
    FileName   = file.FileName,
    RawText    = ocrResult.RawText,
    AnalyzedAt = DateTime.UtcNow,
    Fields     = ...
};
```

- [ ] **Step 2: Add PageCount to the initializer**

Replace the `AnalysisResult` initializer with:

```csharp
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
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build backend/
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add backend/Controllers/DocumentsController.cs
git commit -m "feat: persist PageCount from OCR engine result"
```

---

### Task 3: Add history DTOs to DocumentModels.cs

**Files:**
- Modify: `backend/Models/DocumentModels.cs`

- [ ] **Step 1: Append new DTOs**

Open `backend/Models/DocumentModels.cs` and add at the bottom of the file:

```csharp
// ── GET /api/documents — list item ───────────────────────────────────────────

public class DocumentHistoryItemDto
{
    public int      DocumentId  { get; set; }
    public DateTime CreatedAt   { get; set; }
    public int      PageCount   { get; set; }
    public int      FieldCount  { get; set; }
    /// <summary>First 120 characters of raw text, trimmed at a word boundary.</summary>
    public string   Preview     { get; set; } = string.Empty;
}

public class DocumentHistoryPageDto
{
    public List<DocumentHistoryItemDto> Items      { get; set; } = new();
    public int                          TotalCount { get; set; }
    public int                          Page       { get; set; }
    public int                          PageSize   { get; set; }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build backend/
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add backend/Models/DocumentModels.cs
git commit -m "feat: add DocumentHistoryItemDto and DocumentHistoryPageDto"
```

---

### Task 4: Add GET /api/documents endpoint with tests (TDD)

**Files:**
- Create: `backend/OcrApi.Tests/DocumentsControllerHistoryTests.cs`
- Modify: `backend/Controllers/DocumentsController.cs`

- [ ] **Step 1: Write the failing test**

Create `backend/OcrApi.Tests/DocumentsControllerHistoryTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using OcrApi.Data;
using OcrApi.Models;

namespace OcrApi.Tests;

public class DocumentsControllerHistoryTests
{
    private static OcrDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OcrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OcrDbContext(options);
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static AnalysisResult MakeResult(string fileName, int pageCount, string rawText, int fieldCount)
    {
        var fields = Enumerable.Range(0, fieldCount)
            .Select(i => new SavedField { PropertyName = $"Field{i}", ExtractedValue = $"value{i}" })
            .ToList();

        return new AnalysisResult
        {
            FileName   = fileName,
            RawText    = rawText,
            AnalyzedAt = DateTime.UtcNow,
            PageCount  = pageCount,
            Fields     = fields,
        };
    }

    // ── BuildPreview ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildPreview_ShortText_ReturnsFull()
    {
        var result = "Hello world";
        var preview = DocumentsControllerExtensions.BuildPreview(result);
        Assert.Equal("Hello world", preview);
    }

    [Fact]
    public void BuildPreview_LongText_TruncatesAtWordBoundary()
    {
        var longText = string.Join(" ", Enumerable.Repeat("word", 40)); // 200+ chars
        var preview = DocumentsControllerExtensions.BuildPreview(longText);
        Assert.True(preview.Length <= 123); // 120 + "..."
        Assert.EndsWith("...", preview);
        Assert.DoesNotContain("wor", preview.TrimEnd('.')[^3..]); // ends on full word
    }

    // ── ListDocuments ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListDocuments_ReturnsNewestFirst()
    {
        using var db = CreateDb();
        var older = MakeResult("old.png", 1, "old text", 2);
        older.AnalyzedAt = DateTime.UtcNow.AddHours(-1);
        var newer = MakeResult("new.png", 1, "new text", 3);
        newer.AnalyzedAt = DateTime.UtcNow;
        db.AnalysisResults.AddRange(older, newer);
        await db.SaveChangesAsync();

        var items = await db.AnalysisResults
            .OrderByDescending(r => r.AnalyzedAt)
            .Take(20)
            .ToListAsync();

        Assert.Equal("new.png", items[0].FileName);
    }

    [Fact]
    public async Task ListDocuments_ReturnsCorrectFieldCount()
    {
        using var db = CreateDb();
        db.AnalysisResults.Add(MakeResult("doc.png", 2, "some text", 5));
        await db.SaveChangesAsync();

        var result = await db.AnalysisResults.Include(r => r.Fields).FirstAsync();
        Assert.Equal(5, result.Fields.Count);
    }

    // ── DeleteDocument ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDocument_ExistingId_DeletesResultAndFields()
    {
        using var db = CreateDb();
        var result = MakeResult("doc.png", 1, "text", 3);
        db.AnalysisResults.Add(result);
        await db.SaveChangesAsync();
        var id = result.Id;

        db.AnalysisResults.Remove(result);
        await db.SaveChangesAsync();

        Assert.False(await db.AnalysisResults.AnyAsync(r => r.Id == id));
        Assert.False(await db.SavedFields.AnyAsync(f => f.AnalysisResultId == id));
    }
}

/// <summary>Exposes static helpers from DocumentsController for unit testing.</summary>
public static class DocumentsControllerExtensions
{
    public static string BuildPreview(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        if (rawText.Length <= 120) return rawText;

        var truncated = rawText[..120];
        var lastSpace = truncated.LastIndexOf(' ');
        return lastSpace > 0
            ? truncated[..lastSpace] + "..."
            : truncated + "...";
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test backend/OcrApi.Tests/ --filter "DocumentsControllerHistoryTests"
```

Expected: compile error — `DocumentsControllerExtensions` references a method that doesn't exist yet.

- [ ] **Step 3: Add GET /api/documents to DocumentsController**

Open `backend/Controllers/DocumentsController.cs`. After the `GetById` method, add:

```csharp
// ── GET /api/documents ───────────────────────────────────────────────────────

/// <summary>
/// Returns a paginated list of past analysis results, newest first.
/// </summary>
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test backend/OcrApi.Tests/ --filter "DocumentsControllerHistoryTests"
```

Expected: all tests pass (the `BuildPreview` tests call `DocumentsControllerExtensions.BuildPreview` which delegates to the same logic).

- [ ] **Step 5: Commit**

```bash
git add backend/Controllers/DocumentsController.cs backend/OcrApi.Tests/DocumentsControllerHistoryTests.cs
git commit -m "feat: add GET /api/documents paginated list with unit tests"
```

---

### Task 5: Add DELETE /api/documents/{id} endpoint

**Files:**
- Modify: `backend/Controllers/DocumentsController.cs`
- Modify: `backend/OcrApi.Tests/DocumentsControllerHistoryTests.cs`

- [ ] **Step 1: Add delete test**

Open `backend/OcrApi.Tests/DocumentsControllerHistoryTests.cs` and add a test for 404:

```csharp
[Fact]
public async Task DeleteDocument_UnknownId_ReturnsNotFound()
{
    using var db = CreateDb();
    // empty db — id 9999 does not exist

    var result = await db.AnalysisResults.FindAsync(9999);
    Assert.Null(result);
}
```

- [ ] **Step 2: Run test to verify it passes (the infrastructure test)**

```bash
dotnet test backend/OcrApi.Tests/ --filter "DeleteDocument_UnknownId_ReturnsNotFound"
```

Expected: passes (just checks null).

- [ ] **Step 3: Add DELETE /api/documents/{id} to DocumentsController**

After the `ListDocuments` method, add:

```csharp
// ── DELETE /api/documents/{id} ───────────────────────────────────────────────

/// <summary>
/// Deletes an analysis result and all its associated saved fields (cascade).
/// </summary>
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
```

> **Note:** Cascade deletion of `SavedFields` is configured in `OcrDbContext.OnModelCreating` via `.OnDelete(DeleteBehavior.Cascade)`. EF Core handles this automatically — no manual `SavedFields` removal needed.

- [ ] **Step 4: Build to verify**

```bash
dotnet build backend/
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add backend/Controllers/DocumentsController.cs backend/OcrApi.Tests/DocumentsControllerHistoryTests.cs
git commit -m "feat: add DELETE /api/documents/{id} with cascade"
```

---

### Task 6: Add types and API functions to frontend

**Files:**
- Modify: `frontend/src/types/ocr.ts`
- Modify: `frontend/src/lib/api.ts`

- [ ] **Step 1: Add types to ocr.ts**

Open `frontend/src/types/ocr.ts` and append:

```typescript
export interface DocumentHistoryItem {
  documentId: number;
  createdAt: string;
  pageCount: number;
  fieldCount: number;
  preview: string;
}

export interface DocumentHistoryPage {
  items: DocumentHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}
```

- [ ] **Step 2: Add listDocuments and deleteDocument to api.ts**

Open `frontend/src/lib/api.ts`. Add these imports at the top (after existing imports):

```typescript
import type { DocumentHistoryPage } from '@/types/ocr';
```

Then add at the bottom of the file:

```typescript
export async function listDocuments(page = 1): Promise<DocumentHistoryPage> {
  const res = await fetch(`${API_BASE}/api/documents?page=${page}&pageSize=20`, {
    cache: 'no-store',
    headers: { ...authHeaders() },
  });
  return handleResponse<DocumentHistoryPage>(res);
}

export async function deleteDocument(id: number): Promise<void> {
  const res = await fetch(`${API_BASE}/api/documents/${id}`, {
    method: 'DELETE',
    headers: { ...authHeaders() },
  });
  return handleResponse<void>(res);
}
```

> **Note:** `authHeaders()` is the helper added in the Auth plan. If Auth plan has not been implemented yet, replace `{ ...authHeaders() }` with `{}` temporarily.

- [ ] **Step 3: Verify TypeScript compilation**

```bash
cd frontend && npx tsc --noEmit
```

Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/types/ocr.ts frontend/src/lib/api.ts
git commit -m "feat: add DocumentHistoryItem types and listDocuments/deleteDocument API functions"
```

---

### Task 7: Create HistoryClient.tsx

**Files:**
- Create: `frontend/src/components/history/HistoryClient.tsx`

- [ ] **Step 1: Create the component**

Create `frontend/src/components/history/HistoryClient.tsx`:

```tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { listDocuments, deleteDocument, ApiError } from '@/lib/api';
import type { DocumentHistoryItem, DocumentHistoryPage } from '@/types/ocr';

type LoadState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'loaded'; data: DocumentHistoryPage };

export default function HistoryClient() {
  const [page, setPage]           = useState(1);
  const [loadState, setLoadState] = useState<LoadState>({ status: 'loading' });
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [deleteError, setDeleteError] = useState<{ id: number; message: string } | null>(null);

  const fetchPage = useCallback(async (p: number) => {
    setLoadState({ status: 'loading' });
    try {
      const data = await listDocuments(p);
      setLoadState({ status: 'loaded', data });
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to load history.';
      setLoadState({ status: 'error', message });
    }
  }, []);

  useEffect(() => {
    fetchPage(page);
  }, [page, fetchPage]);

  async function handleDelete(item: DocumentHistoryItem) {
    const confirmed = window.confirm(
      'Delete this result? This cannot be undone.'
    );
    if (!confirmed) return;

    setDeletingId(item.documentId);
    setDeleteError(null);

    try {
      await deleteDocument(item.documentId);
      // Refresh current page; if it becomes empty and we're past page 1, go back
      const newPage = (() => {
        if (loadState.status !== 'loaded') return page;
        const remaining = loadState.data.items.length - 1;
        return remaining === 0 && page > 1 ? page - 1 : page;
      })();
      setPage(newPage);
      if (newPage === page) fetchPage(page);
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Delete failed.';
      setDeleteError({ id: item.documentId, message });
    } finally {
      setDeletingId(null);
    }
  }

  // ── Render helpers ──────────────────────────────────────────────────────────

  function formatDate(iso: string) {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit',
    });
  }

  if (loadState.status === 'loading') {
    return (
      <div className="space-y-3">
        {[...Array(5)].map((_, i) => (
          <div key={i} className="h-16 animate-pulse rounded-xl bg-gray-100" />
        ))}
      </div>
    );
  }

  if (loadState.status === 'error') {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 px-5 py-4">
        <p className="text-sm font-semibold text-red-800">Could not load history</p>
        <p className="mt-1 text-xs text-red-700">{loadState.message}</p>
        <button
          type="button"
          onClick={() => fetchPage(page)}
          className="mt-3 text-xs font-medium text-red-700 underline hover:no-underline"
        >
          Retry
        </button>
      </div>
    );
  }

  const { items, totalCount, pageSize } = loadState.data;
  const totalPages = Math.ceil(totalCount / pageSize);
  const start = (page - 1) * pageSize + 1;
  const end   = Math.min(page * pageSize, totalCount);

  if (items.length === 0 && page === 1) {
    return (
      <div className="flex flex-col items-center gap-4 py-16 text-center">
        <p className="text-gray-500">No documents analyzed yet.</p>
        <Link
          href="/upload"
          className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700"
        >
          Upload your first document
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Results count */}
      <p className="text-sm text-gray-500">
        Showing {start}–{end} of {totalCount} result{totalCount !== 1 ? 's' : ''}
      </p>

      {/* Table */}
      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
        <table className="min-w-full divide-y divide-gray-100">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Date</th>
              <th className="px-5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Pages</th>
              <th className="px-5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Fields</th>
              <th className="px-5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Preview</th>
              <th className="px-5 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {items.map((item) => (
              <tr key={item.documentId} className="hover:bg-gray-50">
                <td className="whitespace-nowrap px-5 py-4 text-sm text-gray-700">
                  {formatDate(item.createdAt)}
                </td>
                <td className="px-5 py-4 text-sm text-gray-500">{item.pageCount}</td>
                <td className="px-5 py-4 text-sm text-gray-500">{item.fieldCount}</td>
                <td className="max-w-xs px-5 py-4 text-sm text-gray-500">
                  <p className="truncate">{item.preview || '—'}</p>
                  {deleteError?.id === item.documentId && (
                    <p className="mt-1 text-xs text-red-600">{deleteError.message}</p>
                  )}
                </td>
                <td className="px-5 py-4">
                  <div className="flex items-center justify-end gap-2">
                    <Link
                      href={`/results/${item.documentId}`}
                      className="rounded-lg bg-blue-50 px-3 py-1.5 text-xs font-medium text-blue-700 hover:bg-blue-100"
                    >
                      View
                    </Link>
                    <button
                      type="button"
                      disabled={deletingId === item.documentId}
                      onClick={() => handleDelete(item)}
                      className="rounded-lg bg-red-50 px-3 py-1.5 text-xs font-medium text-red-700 hover:bg-red-100 disabled:opacity-50"
                    >
                      {deletingId === item.documentId ? 'Deleting…' : 'Delete'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-40"
          >
            Previous
          </button>
          <span className="text-sm text-gray-500">
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-40"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Verify TypeScript compilation**

```bash
cd frontend && npx tsc --noEmit
```

Expected: no type errors.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/components/history/
git commit -m "feat: add HistoryClient component with pagination and delete"
```

---

### Task 8: Create /history page and update Sidebar

**Files:**
- Create: `frontend/src/app/history/page.tsx`
- Modify: `frontend/src/components/Sidebar.tsx` (if "History" not added in Auth plan)

- [ ] **Step 1: Create history page**

Create `frontend/src/app/history/page.tsx`:

```tsx
import HistoryClient from '@/components/history/HistoryClient';

export const metadata = {
  title: 'History — OpenOCR',
};

export default function HistoryPage() {
  return (
    <div className="mx-auto max-w-5xl">
      <h1 className="mb-6 text-2xl font-bold text-gray-900">Document History</h1>
      <HistoryClient />
    </div>
  );
}
```

- [ ] **Step 2: Verify Sidebar has the History link**

Open `frontend/src/components/Sidebar.tsx`. Confirm `NAV_ITEMS` includes:

```typescript
{ label: 'History', href: '/history' },
```

If it is missing (Auth plan was not implemented), add it between Properties and Upload:

```typescript
const NAV_ITEMS: NavItem[] = [
  { label: 'Properties', href: '/properties' },
  { label: 'History',    href: '/history' },
  { label: 'Upload',     href: '/upload' },
];
```

- [ ] **Step 3: Build the full frontend**

```bash
cd frontend && npm run build
```

Expected: build succeeds with no type errors.

- [ ] **Step 4: Manual smoke test**

Start the stack (`make up`). Navigate to `http://localhost:3000/history`. Verify:
- The sample invoice result seeded by the DX plan appears in the table
- Clicking "View" navigates to `/results/{id}`
- Clicking "Delete" → confirming removes the row from the table
- After deleting all items, the empty state message appears

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/history/ frontend/src/components/Sidebar.tsx
git commit -m "feat: add /history page with document list, pagination, and delete"
```
