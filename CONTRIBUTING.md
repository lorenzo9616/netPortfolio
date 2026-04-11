# Contributing to OpenOCR

## Running tests

```bash
make test           # All unit suites
make test-e2e       # E2E (requires running stack)
```

Individual suites:

```bash
dotnet test backend/OcrApi.Tests/         # Backend (C#)
cd ocr-service && pytest tests/           # OCR service (Python)
cd frontend && npm test -- --run          # Frontend (TypeScript)
```

## Adding a database migration

When you change a backend model, create a migration:

```bash
dotnet ef migrations add YourMigrationName --project backend/
```

Migrations run automatically on next startup. Commit the generated files.

## Resetting the database

```bash
make reset
```

This stops all containers, deletes all data volumes, and restarts fresh.

## Pre-commit hook

Install once:

```bash
# Requires lefthook binary — see https://github.com/evilmartians/lefthook
lefthook install
```

The hook runs all unit tests before each commit. If it fails, fix the failing test and commit again. Never skip the hook with `--no-verify`.

## Project structure

```
backend/              .NET 8 REST API
backend/OcrApi.Tests/ xUnit tests
frontend/             Next.js 14 UI
ocr-service/          Python FastAPI OCR engine
ocr-service/tests/    pytest tests
e2e/                  Playwright E2E tests
docs/                 Design specs and implementation plans
```

## Pre-commit hooks (lefthook)

[lefthook](https://github.com/evilmartians/lefthook) runs backend, OCR-service, and frontend unit tests on every commit.

Install lefthook (one-time, per developer):

```bash
# macOS / Linux (Homebrew)
brew install lefthook

# Windows (Scoop)
scoop install lefthook

# Or via npm (works everywhere)
npm install -g lefthook
```

Then install hooks in the repo:

```bash
lefthook install
```

The pre-commit hook runs in parallel:
- `dotnet test backend/OcrApi.Tests/` — only when `.cs` files change
- `npm test` in `frontend/` — only when `.ts`/`.tsx` files change
- `pytest tests/` in `ocr-service/` — only when `.py` files change
