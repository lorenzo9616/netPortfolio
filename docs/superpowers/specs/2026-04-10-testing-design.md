---
name: OpenOCR Testing Suite
description: Unit, integration, and E2E tests across all three services, with lefthook pre-commit enforcement and a single make test command
type: project
---

# OpenOCR — Testing Design

**Date:** 2026-04-10  
**Status:** Approved  
**Audience:** All developers — junior devs write tests, senior devs enforce them

---

## Problem

The codebase has no tests. There is no way to verify that a change to the field matcher, the OCR pipeline, or the frontend didn't break existing behavior. Junior devs have no patterns to follow when writing new tests.

---

## Goals

- Every service has a working test suite runnable with a single command.
- Junior devs have clear patterns to follow when adding tests.
- Tests are enforced automatically before commits reach `main`.
- Running the full suite takes under 2 minutes on a developer machine.

---

## Architecture

Three independent test suites (one per service) plus one E2E suite. All suites are orchestrated by `make test`. Pre-commit enforcement via `lefthook` (single binary, no Node or Python dependency).

---

## Components

### 1. Backend Tests — xUnit (`backend/OcrApi.Tests/`)

**Setup:**
- New project: `dotnet new xunit -o backend/OcrApi.Tests`
- References `OcrApi.csproj`.
- Uses EF Core's `UseInMemoryDatabase` provider — no Docker needed to run backend tests.
- Run with: `dotnet test backend/OcrApi.Tests/`

**Test coverage:**

`FieldMatcherTests.cs`:
- Matches a field when the regex pattern is present in raw text.
- Returns null when no match is found.
- Handles null/empty raw text without throwing.
- Handles invalid regex pattern gracefully.

`OcrPropertiesControllerTests.cs` (via `WebApplicationFactory`):
- `GET /api/ocrproperties` returns seeded properties.
- `POST /api/ocrproperties` with valid DTO creates and returns a property.
- `POST /api/ocrproperties` with missing name returns `400`.
- `DELETE /api/ocrproperties/{id}` removes the property.
- `DELETE /api/ocrproperties/{id}` with unknown id returns `404`.

`DocumentsControllerTests.cs`:
- `GET /api/documents` returns paginated results with correct metadata.
- `GET /api/documents` with `page=2` returns the correct offset.
- `DELETE /api/documents/{id}` cascades to saved fields.
- `DELETE /api/documents/{id}` with unknown id returns `404`.

`AuthControllerTests.cs`:
- Valid credentials return a JWT token.
- Invalid credentials return `401`.
- Missing credentials return `400`.

`SeedDataTests.cs`:
- Seed initializes 3 OCR properties when table is empty.
- Seed does not re-run when table already has rows.

### 2. OCR Service Tests — pytest (`ocr-service/tests/`)

**Setup:**
- `tests/__init__.py` (empty)
- `tests/conftest.py` — shared fixtures
- Install `pytest` and `pytest-mock` (added to `requirements.txt`)
- Run with: `pytest ocr-service/tests/`

**Test coverage:**

`test_preprocessor.py`:
- Grayscale conversion produces a single-channel image.
- Deskew does not crash on an already-straight image.
- Preprocessing a zero-size image raises `ValueError`.

`test_extractor.py`:
- `extract_from_image` returns a list of `TextBlock` objects.
- Uses `pytest-mock` to mock `pytesseract.image_to_data` — no real Tesseract call in unit tests.
- Returns an empty list when mock returns no words.
- Each `TextBlock` has non-empty `text` and a confidence value between 0 and 100.

`test_pdf_handler.py`:
- `pdf_to_images` returns a list of numpy arrays.
- Mock `pdf2image.convert_from_bytes` — no real PDF rendering in unit tests.
- Raises `ValueError` for an empty byte string.

`test_main.py` (smoke — requires running container, skipped in unit mode):
- `GET /health` returns `{"status": "ok"}`.
- Mark with `@pytest.mark.integration` — excluded from the default `pytest` run, included in `make test-integration`.

### 3. Frontend Tests — Vitest + React Testing Library (`frontend/`)

**Setup:**
- `npm install --save-dev vitest @testing-library/react @testing-library/user-event jsdom`
- `vitest.config.ts` at `frontend/` root.
- Tests live in `__tests__/` folders adjacent to the files they test.
- Run with: `npm test` (maps to `vitest run`)

**Test coverage:**

`__tests__/lib/api.test.ts`:
- `getProperties` calls the correct URL and returns parsed JSON.
- `createProperty` sends POST with correct body.
- `deleteProperty` sends DELETE to correct URL.
- `ApiError` is thrown when response is not `ok`.
- Uses `vi.stubGlobal('fetch', ...)` to mock `fetch`.

`__tests__/components/properties/PropertyForm.test.tsx`:
- Renders with empty fields by default.
- Submit with empty name shows validation error.
- Submit with valid name calls `onSave` prop.

`__tests__/components/upload/FileDropzone.test.tsx`:
- Renders drop zone with instructional text.
- Calls `onFileSelected` when a file is dropped.

`__tests__/app/login/LoginPage.test.tsx`:
- Renders username and password fields.
- Shows error message when API returns 401.
- Calls auth API with entered credentials on submit.

`__tests__/app/history/HistoryClient.test.tsx`:
- Shows empty state when API returns zero items.
- Renders rows for each returned item.
- Calls delete API and removes row on confirm.

### 4. E2E Tests — Playwright (`e2e/`)

**Setup:**
- `npm init playwright@latest` at repo root, output to `e2e/`.
- `playwright.config.ts` — base URL `http://localhost:3000`, runs against the running Docker Compose stack.
- Run with: `npx playwright test` (or `make test-e2e`).
- E2E tests are NOT included in the pre-commit hook — they require the full stack running.

**Test coverage:**

`e2e/auth.spec.ts`:
- Visiting `/` redirects to `/login` when not authenticated.
- Login with default credentials redirects to `/properties`.
- Sign out redirects to `/login`.
- Login with wrong password shows error message.

`e2e/upload.spec.ts`:
- Upload a small sample PNG image → click Analyze → redirected to `/results/{id}`.
- Results page shows at least one extracted field.

`e2e/history.spec.ts`:
- Navigate to `/history` → see the document just uploaded.
- Click "View" → navigated to results page.
- Click "Delete" → confirm → document removed from list.

### 5. `lefthook` Pre-commit Hook

**Setup:**
- Install `lefthook` binary (single download, no runtime dependency).
- `lefthook.yml` at repo root:

```yaml
pre-commit:
  parallel: true
  commands:
    backend-tests:
      root: "backend/"
      run: dotnet test OcrApi.Tests/
    ocr-tests:
      root: "ocr-service/"
      run: pytest tests/
    frontend-tests:
      root: "frontend/"
      run: npm test
```

- `CONTRIBUTING.md` documents: `lefthook install` (one-time setup) and what to do when a test fails before committing.

### 6. `Makefile` Targets

| Target | What it runs |
|---|---|
| `make test` | All three unit suites via lefthook (no E2E) |
| `make test-e2e` | Playwright E2E suite (requires running stack) |
| `make test-integration` | OCR service integration tests (`pytest -m integration`) |

---

## Data Flow

Tests are hermetic — each suite manages its own state:
- Backend tests use in-memory SQLite, reset between test classes.
- OCR service tests mock external calls (Tesseract, pdf2image).
- Frontend tests mock `fetch`.
- E2E tests run against the live stack; the `e2e/` suite includes a `beforeAll` that logs in via the UI before running protected-route tests.

---

## Error Handling

- If a pre-commit test suite fails, `lefthook` prints the failing test output and blocks the commit.
- `CONTRIBUTING.md` explains: fix the test, stage the fix, then commit again. Do not skip hooks.
- `make test` exits with a non-zero code if any suite fails, making it CI-ready without extra configuration.

---

## Out of Scope

- GitHub Actions CI pipeline (can be added by copying `make test` into a workflow YAML).
- Performance/load testing.
- Visual regression testing.
- Code coverage thresholds.
