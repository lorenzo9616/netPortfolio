---
name: OpenOCR DX & Setup Improvements
description: One-command startup, auto-migrations, seed data, and Docker Desktop-friendly docs for junior developers
type: project
---

# OpenOCR — DX & Setup Design

**Date:** 2026-04-10  
**Status:** Approved  
**Audience:** Junior developers onboarding to OpenOCR locally

---

## Problem

The current setup requires multiple manual terminal steps: copying `.env.example`, starting Docker Compose, then shelling into the running backend container to run database migrations. This is fragile, easy to get wrong, and a barrier for junior developers who prefer Docker Desktop GUI over the terminal.

---

## Goals

- A junior dev with Docker Desktop installed goes from zero to a running app in under 5 minutes.
- No terminal commands beyond one copy-paste (or a double-click on Windows).
- First-time users immediately see sample data — not a blank screen.

---

## Architecture

No architectural changes. All improvements are tooling, scripts, and startup behavior layered on top of the existing 4-service Docker Compose stack.

---

## Components

### 1. `setup.bat` / `setup.sh`

A single bootstrap script at the repo root. Steps:

1. Check if `.env` exists — if not, copy `.env.example` → `.env` and print "Created .env with default credentials."
2. Detect whether Docker is running — if not, print a clear error with instructions to start Docker Desktop.
3. Run `docker compose up --build -d`.
4. Poll `http://localhost:5000/health` and `http://localhost:8000/health` every 3 seconds (max 60s) until both respond 200.
5. Print success banner:

```
OpenOCR is running!
  Frontend:       http://localhost:3000
  Backend Swagger: http://localhost:5000/swagger
  OCR Service:    http://localhost:8000/health

Default login: admin / openocr2024
```

On Windows, junior devs double-click `setup.bat`. On Mac/Linux, `./setup.sh`.

### 2. Auto-migration on Backend Startup

Replace the manual `dotnet ef database update` step with a call to `dbContext.Database.Migrate()` inside `Program.cs`, executed before `app.Run()`. This is idempotent — running it on every restart is safe and applies any pending migrations automatically.

```csharp
// After building the app, before app.Run():
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OcrDbContext>();
    db.Database.Migrate();
}
```

### 3. Seed Data

On first boot (when the `OcrProperties` table is empty), the backend seeds the database with:

- **3 sample OCR properties:** "Invoice Number" (regex: `INV-\d+`), "Date" (regex: `\d{2}/\d{2}/\d{4}`), "Total Amount" (regex: `\$[\d,]+\.\d{2}`)
- **1 pre-analyzed sample document result** with realistic extracted field values so testers open the History page and immediately see data.

Seed logic runs in the same startup block as migrations:

```csharp
if (!db.OcrProperties.Any())
    SeedData.Initialize(db);
```

A `make seed` target also allows re-seeding manually if the database is reset.

### 4. `Makefile`

Common dev operations wrapped as simple named commands:

| Target | What it does |
|---|---|
| `make up` | `docker compose up --build -d` |
| `make down` | `docker compose down` |
| `make logs` | `docker compose logs -f` |
| `make seed` | Re-run seed data against running backend |
| `make test` | Run all test suites (see Testing spec) |
| `make reset` | `docker compose down -v` then `make up` (wipes DB) |

### 5. Updated `README.md`

Rewritten for Docker Desktop GUI users:

- **Prerequisites** section: Docker Desktop download link, no other tools required.
- **Quick Start** reduced to 3 steps: Install Docker Desktop → Clone repo → Double-click `setup.bat`.
- **"What you should see"** checklist after each step.
- **Default Credentials** section prominently placed.
- **Troubleshooting** section: port already in use (how to find and stop the process), Docker not running, containers crash-loop (link to `make logs`).

### 6. `CONTRIBUTING.md`

Short guide for junior devs:

- How to run tests for each service.
- How to create a new database migration.
- How to reset the database to a clean state.
- How the pre-commit hook works and what to do if it fails.

---

## Data Flow

No changes to runtime data flow. Seed data is inserted at boot time via EF Core and is indistinguishable from user-created data once inserted.

---

## Error Handling

- If Docker is not running when `setup.bat` is executed, the script exits with a clear message and does not proceed.
- If health checks time out after 60 seconds, the script prints the last known Docker Compose log lines and suggests `make logs` for debugging.
- If migrations fail at startup, the backend logs the error and exits (container restarts, surfacing the error in `make logs`).

---

## Testing

- Manual smoke test: fresh clone on a machine with only Docker Desktop → run `setup.bat` → verify all 3 services healthy and seed data visible.
- Seed logic covered by backend unit tests (see Testing spec).

---

## Out of Scope

- CI/CD pipeline integration (future phase).
- Multi-user credential management (see Auth spec).
- Production deployment hardening.
