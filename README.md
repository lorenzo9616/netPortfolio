# OpenOCR — Open-Source Document OCR System

Extract structured text fields from images and PDFs. Upload a document, review the extracted fields, and save corrections.

## Default Login Credentials

```
Username: admin
Password: openocr2024
```

---

## Quick Start (Docker Desktop)

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

### 1. Clone the repository

```bash
git clone <repo-url>
cd openocr
```

### 2. Run the setup script

**Windows:** Double-click `setup.bat`

**Mac / Linux:**
```bash
chmod +x setup.sh && ./setup.sh
```

The script will:
- Create a `.env` file with default credentials
- Build and start all services
- Wait until the app is healthy

### 3. Open the app

- **App:** http://localhost:3000
- **API docs (Swagger):** http://localhost:5000/swagger

Sign in with `admin` / `openocr2024`.

---

## What you should see

After setup completes:

- [ ] http://localhost:3000 loads the login page
- [ ] Signing in shows the Properties page with 5 sample OCR fields
- [ ] The History page shows one sample document result
- [ ] http://localhost:5000/swagger shows the API documentation

---

## Common Commands

```bash
make up        # Start services
make down      # Stop services
make logs      # View live logs
make test      # Run all tests
make reset     # Wipe data and restart (destructive)
```

---

## Troubleshooting

**Port already in use**
Another app is using port 3000, 5000, or 5432. Stop it, then run `make up`.

**Docker not running**
Open Docker Desktop and wait for the whale icon to stop animating, then re-run the setup script.

**Containers crash and restart**
Run `make logs` to see the error. Common causes: invalid `.env` values or a port conflict.

**Database errors**
Run `make reset` to wipe the database and start fresh. This deletes all uploaded documents.

---

## Architecture

| Service | Tech | Port | Purpose |
|---|---|---|---|
| frontend | Next.js 14 | 3000 | UI |
| backend | .NET 8 | 5000 | REST API, field matching, persistence |
| ocr-service | Python FastAPI | 8000 | Tesseract OCR engine |
| db | PostgreSQL 16 | 5432 | Storage |

See `CONTRIBUTING.md` for development setup.
