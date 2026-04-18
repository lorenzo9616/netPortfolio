# OpenOCR — Open-Source Document OCR System

## Overview

OpenOCR is a self-hosted document processing system that extracts structured text fields from uploaded images and PDFs. Users configure named OCR properties (with optional regex heuristics), upload documents through the web UI or a REST API, and receive extracted field values they can review and manually correct.

The system is composed of three services and a PostgreSQL database, all orchestrated via Docker Compose.

## Architecture

| Service | Tech | Port | Purpose |
|---|---|---|---|
| frontend | Next.js 14 | 3000 | UI for managing OCR properties, uploading documents, and reviewing results |
| backend | .NET 8 | 5000 | REST API, orchestration, field matching, and data persistence |
| ocr-service | Python FastAPI | 8000 | OCR engine (Tesseract, OpenCV, pdf2image) |
| db | PostgreSQL 16 | 5432 | Persistent storage for properties and analysis results |

## Prerequisites

- Docker Desktop 4.x+
- (Optional for local dev) Node 20, .NET 8 SDK, Python 3.11

## Quick Start

```bash
git clone <repo>
cd openocr
docker-compose up --build
```

The database schema is created automatically on first boot — no manual migration step required.

If you want to customise passwords or enable API key auth, copy `.env.example` to `.env` and edit before running:

```bash
cp .env.example .env
# Edit .env, then:
docker-compose up --build
```

## Access

Once all containers are healthy:

| URL | What |
|---|---|
| http://localhost:3000 | Web UI |
| http://localhost:5000/swagger | Backend REST API explorer |
| http://localhost:8000/health | OCR service health |

## Seed Data

Five OCR properties are pre-loaded on first run:

| Name | Data Type | Heuristic |
|---|---|---|
| Signature | string | (keyword match) |
| FullName | string | (keyword match) |
| DateOfBirth | date | `\b\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}\b` |
| BillingTotal | decimal | `\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?` |
| ProcessingFee | decimal | `\$?\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?` |

## Supported File Types

PDF, PNG, JPG — max 20 MB per upload.

## Development (without Docker)

### Frontend

```bash
cd frontend && npm install && npm run dev
```

### Backend

```bash
cd backend && dotnet run
```

Requires a local PostgreSQL instance. Update `appsettings.Development.json` with your connection string.

### OCR Service

```bash
cd ocr-service && pip install -r requirements.txt && uvicorn main:app --reload
```

Requires Tesseract OCR and Poppler installed on the host.

## API Key Authentication

Set `BACKEND_API_KEY` in `.env`. All `/api/*` requests must include:

```
X-Api-Key: your-key-here
```

Leave blank to disable auth (development/demo mode). When an invalid or missing key is supplied, the backend returns a standard `application/problem+json` 401 response. The `/health` endpoint is always unauthenticated.

## License

MIT
