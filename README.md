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

### 1. Clone & configure

```bash
git clone <repo>
cd openocr
cp .env.example .env
# Edit .env — set POSTGRES_PASSWORD and optionally BACKEND_API_KEY
```

### 2. Start the cluster

```bash
docker-compose up --build
```

### 3. Access

- Frontend: http://localhost:3000
- Backend Swagger: http://localhost:5000/swagger
- OCR Service health: http://localhost:8000/health

### 4. First-time database setup

```bash
# After containers are running:
docker-compose exec backend dotnet ef migrations add InitialCreate
docker-compose exec backend dotnet ef database update
```

## Development (without Docker)

### Frontend

```bash
cd frontend && npm install && npm run dev
```

### Backend

```bash
cd backend && dotnet run
```

### OCR Service

```bash
cd ocr-service && pip install -r requirements.txt && uvicorn main:app --reload
```

## API Key Authentication

Set `BACKEND_API_KEY` in `.env`. All `/api/*` requests must include:

```
X-Api-Key: your-key-here
```

Leave blank to disable auth (development mode). When an invalid or missing key is supplied, the backend returns a standard `application/problem+json` 401 response.

## Outstanding TODOs

No outstanding `// TODO` comments were found in the codebase at the time of this cleanup session.

## License

MIT
