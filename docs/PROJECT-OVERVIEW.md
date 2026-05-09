# OpenOCR — Project Overview
### For Project Managers & Marketing

---

## What Does This System Do?

OpenOCR is a web application that reads text from uploaded documents — invoices, medical records, contracts, and any scanned paperwork — and automatically pulls out the important information (names, dates, amounts, case numbers, etc.) into a structured, editable table. Users can also mark a signature area on the document with their mouse, and the system crops and saves that signature image.

The system is **fully self-hosted and open-source**, meaning it runs on your own servers with no third-party AI or subscription fees.

---

## System Architecture at a Glance

The product is made of **three separate services** that work together:

```
User's Browser
      │
      ▼
┌─────────────┐     sends document     ┌──────────────┐     reads text     ┌─────────────┐
│   Frontend  │ ─────────────────────► │   Backend    │ ─────────────────► │ OCR Service │
│  (Website)  │ ◄───────────────────── │  (API)       │ ◄───────────────── │  (Engine)   │
└─────────────┘    returns results     └──────┬───────┘    structured data └─────────────┘
                                              │
                                              ▼
                                     ┌────────────────┐
                                     │   Database     │
                                     │  (PostgreSQL)  │
                                     └────────────────┘
```

| Service | What it does in plain English |
|---|---|
| **Frontend** | The website the user sees and interacts with |
| **Backend** | The brain — receives requests, applies business rules, saves data |
| **OCR Service** | The eyes — reads and extracts text from images and PDFs |
| **Database** | The memory — stores all documents, results, and saved fields |

---

## Service 1 — Frontend (The Website)

### Technology Stack

| Item | Technology | Version | Plain-English Explanation |
|---|---|---|---|
| **Programming Language** | TypeScript | 5.4 | A stricter, safer version of JavaScript |
| **UI Framework** | React | 18.3 | The industry-standard library for building interactive web pages |
| **Web Framework** | Next.js | 14.2 | Wraps React with routing, server-side rendering, and optimized builds |
| **Styling** | Tailwind CSS | 3.4 | A utility library that makes pages look polished without custom CSS files |
| **Runtime** | Node.js | 20+ | The engine that runs the website server |
| **Container** | Docker (Alpine Linux) | — | Packages everything into a portable, reproducible box |

### Pages

| Page URL | What the user sees |
|---|---|
| `/upload` | File upload screen — drag & drop or camera capture |
| `/results/{id}` | Results screen — extracted fields table, document image, signature canvas, OCR tokens |
| `/properties` | Admin screen — manage which data fields the system looks for |

### Components and What They Do

> **What is a component?** A reusable visual building block on a web page — like a button, a form, or a panel.

---

#### Upload Page Components

**`FileDropzone`**
The drag-and-drop upload area. Users can drag a file onto it or click to browse. Also contains a camera button for mobile use.

**`CameraCapture`**
Opens the device camera (on phones/tablets) so users can photograph a document directly instead of uploading a file.

**`CanvasPreview`**
Shows a preview of the selected image and lets users draw a rectangle to crop only part of the document before sending it for analysis.

**`UploadClient`**
The coordinator for the entire upload page. Holds the state (which file is selected, is analysis running, did it fail?) and wires all the smaller components together. When the user clicks "Analyze Document", this component calls the backend and redirects to the results page.

---

#### Results Page Components

**`ResultsClient`**
The coordinator for the results page. Shows the extracted fields table, handles the "Save Changes" button, and switches between two modes for the document preview panel: **Word Overlay** (showing bounding boxes) and **Capture Signature** (drawing a crop region). Also renders the detected tables section.

**`BoundingBoxOverlay`**
A canvas layer drawn on top of the document image that highlights every word Tesseract found, colour-coded by confidence: green (≥80%), yellow (50–80%), red (<50%). Hovering over a box shows a tooltip with the word text and exact confidence percentage. Supports multi-page documents with Previous/Next page controls.

**`SignatureCanvas`**
Overlays a transparent drawing layer on top of the document image. The user clicks and drags to draw a rectangle around the signature. Once released, a green "Capture Signature" button appears. Clicking it crops the image at that region and sends it to the backend for storage. The Signature row in the table then switches from a text box to a thumbnail of the cropped image.

**`TableBlocksView`**
Renders tables detected from the OCR layout — each table shows its page number, row/column count, and the cell contents formatted as an HTML table. The first row is styled as a header.

**`OcrTokensTable`**
A scrollable table at the bottom of the results page that shows every single word Tesseract found in the document — the text, a colour-coded confidence score (green = high, yellow = medium, red = low), the page number, and the position on the page.

---

#### Properties Page Components

**`PropertiesList`**
A table showing all the data fields the system currently looks for (e.g. "InvoiceNumber", "DateOfBirth", "PatientId"). Each row has Edit and Delete buttons.

**`PropertyForm`**
A form inside a modal popup for creating a new field or editing an existing one. Accepts the field name, data type, and optional search pattern.

**`Modal`**
A reusable popup container used by `PropertyForm`. Handles the backdrop and close button.

---

#### Shared / Library

**`api.ts` — API communication functions**

All calls to the backend are centralised in this one file. Each function has a single purpose:

| Function | What it does |
|---|---|
| `analyzeDocumentStream(file, crop?, lang, onProgress)` | Uploads a document and receives live progress events (Server-Sent Events) as OCR runs. Calls `onProgress` for each step completed. Returns the document ID when done. |
| `analyzeDocument(file, crop?)` | Single-shot upload without streaming — kept for non-streaming contexts. Returns a document ID. |
| `getAnalysisResult(id)` | Fetches the full analysis result (all extracted fields, text, image, table blocks) for a given document ID. |
| `saveFieldOverrides(documentId, fields)` | Sends the user's manual corrections back to the backend to be saved. |
| `captureSignature(documentId, imageData)` | Sends a base64-encoded PNG crop of the signature region to the backend for permanent storage. |
| `getProperties()` | Fetches the list of all data fields the system is configured to look for. |
| `createProperty(dto)` | Creates a new data field (e.g. add "EmployeeID" as a new field to extract). |
| `updateProperty(id, dto)` | Updates an existing field's name, type, or search pattern. |
| `deleteProperty(id)` | Permanently removes a data field from the system. |

**`Sidebar`**
The left-hand navigation bar visible on all pages. Contains links to Upload, Results history, and Properties.

---

## Service 2 — Backend (The API)

### Technology Stack

| Item | Technology | Version | Plain-English Explanation |
|---|---|---|---|
| **Programming Language** | C# | 12 | A Microsoft language widely used for enterprise web services |
| **Runtime / Platform** | .NET | 8.0 LTS | Microsoft's modern, cross-platform application runtime |
| **Web Framework** | ASP.NET Core | 8.0 | The framework that handles HTTP requests and routes |
| **Database ORM** | Entity Framework Core | 8.0 | Translates C# code into database queries — no manual SQL needed |
| **Database Driver** | Npgsql | 8.0 | Connects to PostgreSQL specifically |
| **API Documentation** | Swagger / OpenAPI | 6.6 | Auto-generates a browser-based API explorer at `/swagger` |
| **Container** | Docker (Alpine Linux) | — | Packaged for consistent deployment |

### What the Backend Is Responsible For

- Receiving uploaded files from the browser
- Forwarding them to the OCR Service and waiting for results
- Matching extracted text against configured rules (regex patterns and keywords)
- Storing everything in the database
- Serving saved results back to the browser
- Enforcing security (API key) and rate limiting (max 10 document analyses per minute)

### API Endpoints

> **What is an API endpoint?** A specific address (URL) on the server that performs one job when called.

#### Documents (`/api/documents`)

| Endpoint | Action | Plain-English Description |
|---|---|---|
| `POST /api/documents/analyze` | Upload & analyze | Accepts a document file, sends it to the OCR Service, matches fields, stores everything, and returns extracted data. Limited to 10 uploads per minute. |
| `POST /api/documents/analyze/stream` | Upload & analyze (streaming) | Same as above but streams live progress events (Server-Sent Events) as OCR runs — the browser sees step-by-step updates instead of waiting for the full response. Emits `status`, `done`, and `error` events. |
| `GET /api/documents/{id}` | Fetch results | Returns the full analysis for a previously processed document: all fields, raw OCR text, token list, detected tables, and any saved signature image. |
| `PUT /api/documents/{id}/fields` | Save edits | Saves the user's manual corrections to extracted field values. |
| `GET /api/documents/{id}/image` | Get document image | Streams the original uploaded image back to the browser so it can be shown in the preview panel. |
| `PATCH /api/documents/{id}/signature` | Save signature | Receives a cropped PNG image (as base64 text) and stores it as the document's signature. |

#### OCR Properties (`/api/ocrproperties`)

| Endpoint | Action | Plain-English Description |
|---|---|---|
| `GET /api/ocrproperties` | List all fields | Returns every data field the system is configured to extract. |
| `GET /api/ocrproperties/{id}` | Get one field | Returns a single field's configuration. |
| `POST /api/ocrproperties` | Create field | Adds a new extraction field (e.g. "ContractValue"). |
| `PUT /api/ocrproperties/{id}` | Update field | Edits an existing field's name, type, or search rule. |
| `DELETE /api/ocrproperties/{id}` | Delete field | Removes a field. Future documents won't extract this value. |

#### System

| Endpoint | Description |
|---|---|
| `GET /health` | Health check — used by Docker to confirm the service is alive. Returns `{ "status": "Healthy" }`. |
| `GET /swagger` | Interactive API documentation — browse and test all endpoints in the browser. |

### Internal Backend Functions

#### `DocumentsController` — handles all document operations

| Method | What it does |
|---|---|
| `Analyze` | Buffers the uploaded file, sends it to OCR, matches extracted text against active field rules, stores the result (including the raw image bytes, every OCR word token, and any detected tables), and returns the document ID. |
| `AnalyzeStream` | Same pipeline as `Analyze` but proxies the OCR service's SSE stream directly to the browser — the browser sees incremental progress steps. Emits `status` events while processing and a final `done` event with the document ID. |
| `GetById` | Loads a saved analysis from the database and returns it as structured JSON — fields, text, signature image (as base64), the full token list, and detected table blocks. |
| `UpdateFields` | Applies the user's manual override values to saved fields and returns the updated list. |
| `GetImage` | Looks up the stored image bytes for a document and streams the original file back to the browser. |
| `CaptureSignature` | Decodes the base64 PNG from the request, stores it as the signature for that document, and echoes it back. |
| `GetContentType` | Helper — maps a filename extension (`.png`, `.pdf`, etc.) to the correct MIME type so browsers display files correctly. |

#### `OcrPropertiesController` — manages extraction field configuration

| Method | What it does |
|---|---|
| `GetAll` | Returns the full list of configured extraction fields. |
| `GetById` | Returns a single field by ID. |
| `Create` | Adds a new field definition to the database. |
| `Update` | Edits an existing field — only the supplied properties are changed. |
| `Delete` | Removes a field by ID. |

#### `FieldMatcher` — the matching engine

This is the logic that takes raw OCR text and finds the values corresponding to each configured field.

| Method | What it does |
|---|---|
| `MatchFields` | Takes a list of active fields and the OCR result; returns extracted values for each field. |
| `MatchSingleField` | Runs either the regex matcher or the keyword matcher for a single field. |
| `KeywordMatch` | Searches for an exact keyword phrase (e.g. "Notary Public") and returns the text that follows it on the same line. |
| `ReconstructText` | Assembles the raw OCR word blocks into a readable multi-line string, ordered by position on the page. |
| `NormalizeValue` | Cleans up a raw value based on its type: formats dates as `YYYY-MM-DD`, strips non-numeric characters from money amounts, trims whitespace from strings. |

#### `OcrClient` — communicates with the OCR Service

| Method | What it does |
|---|---|
| `ExtractTextAsync` | Sends the document file (and optional crop region) to the Python OCR Service as a multipart upload and deserializes the full response. |
| `ExtractTextStreamAsync` | Sends the same multipart upload but returns as soon as response headers arrive, leaving the body as a readable stream for the caller to proxy as SSE. |

---

## Service 3 — OCR Service (The Text Reader)

### Technology Stack

| Item | Technology | Version | Plain-English Explanation |
|---|---|---|---|
| **Programming Language** | Python | 3.11 | A widely-used language popular for data processing and AI tooling |
| **Web Framework** | FastAPI | 0.111+ | A modern Python framework for building fast APIs |
| **OCR Engine** | Tesseract OCR | 5.x | Google's open-source optical character recognition engine — reads text from images |
| **Tesseract Wrapper** | pytesseract | 0.3.10+ | Python library that makes it easy to call Tesseract |
| **Image Processing** | OpenCV | 4.9+ | Industry-standard computer vision library — used to clean up images before OCR |
| **Image Handling** | Pillow | 10.3+ | Python imaging library — opens and converts image files |
| **PDF Conversion** | pdf2image / Poppler | 1.17+ | Converts PDF pages into images so Tesseract can read them |
| **Data Validation** | Pydantic | 2.7+ | Ensures API request/response data is always in the correct format |
| **Server** | Uvicorn | 0.29+ | The high-performance web server that runs the FastAPI application |
| **Container** | Docker (Debian slim) | — | Pre-installs Tesseract and Poppler so the service is ready without manual setup |

> **Note:** This service uses **no paid AI models**. All text recognition is performed by Tesseract, which is 100% free and runs entirely on your own hardware.

### API Endpoints

| Endpoint | Description |
|---|---|
| `GET /health` | Returns `{ "status": "ok" }`. Used by Docker health checks. |
| `POST /extract-text` | Accepts an image or PDF file (up to 20 MB) plus an optional crop region, runs OCR, and returns every word found with confidence scores and positions, plus any detected table blocks. |
| `POST /extract-text-stream` | Same as `/extract-text` but streams Server-Sent Events in real time: one `status` event per processing step (decode, preprocess, OCR per page, signature detection, table detection), then a `complete` event with the full JSON payload, or an `error` event on failure. |

### Python Functions

#### `main.py` — the API entry point

| Function | What it does |
|---|---|
| `health()` | Returns a simple OK response so the system knows the service is running. |
| `extract_text(file, crop_x, crop_y, crop_width, crop_height)` | The main endpoint. Validates the file type and size, decodes it into images (one per page for PDFs), applies the optional crop region, runs OCR on each page, detects tables, and assembles the full response. |
| `extract_text_stream(file, ...)` | SSE streaming endpoint. Runs the same OCR pipeline but yields a `status` event after each step so the browser can show live progress. Wraps all blocking calls in `asyncio.to_thread` to keep the async event loop free. |
| `_sse(event, data)` | Helper that formats one Server-Sent Event string (`event: ...\ndata: ...\n\n`). |
| `MaxBodySizeMiddleware.dispatch` | A gatekeeper that rejects files larger than 20 MB before any processing begins, protecting the server from being overloaded. |

#### `extractor.py` — runs Tesseract

| Function | What it does |
|---|---|
| `extract_from_image(image, page)` | Preprocesses the image, runs it through Tesseract, and returns a list of individual word results — each containing the text, a confidence score (0–100), the page number, and the pixel coordinates of the word's bounding box. |

#### `preprocessor.py` — cleans images before OCR

| Function | What it does |
|---|---|
| `preprocess_image(image)` | The main cleaning pipeline. Runs four steps in sequence: converts to grayscale → corrects tilt → reduces noise → sharpens text into pure black and white. This significantly improves accuracy on scanned or photographed documents. |
| `_deskew(gray)` | Detects if the document is rotated (e.g. scanned at a slight angle) and corrects it. Skips correction if the tilt is less than 0.5° to avoid degrading already-straight images. |

#### `pdf_handler.py` — converts PDFs to images

| Function | What it does |
|---|---|
| `pdf_to_images(file_bytes, dpi)` | Takes the raw bytes of a PDF file and converts every page into an image at 200 DPI (a quality level that balances accuracy with processing speed). Returns a list of images ready for OCR. |

#### `table_detector.py` — finds tabular structure in OCR output

| Function | What it does |
|---|---|
| `detect_tables(blocks, page)` | Takes the OCR word blocks for one page and returns a list of detected tables using pure positional analysis — no image processing required. Groups words into rows by vertical proximity, then identifies columns by horizontal gaps. Only groups with ≥ 2 rows and ≥ 2 columns are returned. |

#### `models.py` — data structures

These define the exact shape of data flowing in and out of the service. Non-technical analogy: these are the standard form templates every response must follow.

| Model | Fields | Purpose |
|---|---|---|
| `BoundingBox` | x, y, width, height | The pixel rectangle surrounding a single word in the document |
| `TextBlock` | text, confidence, bounding_box, page | One recognised word: what it says, how confident the engine is, where it appears |
| `TableCell` | row, col, text, bounding_box | One cell inside a detected table |
| `TableBlock` | page, rows, cols, cells, bounding_box | One detected table: its location on the page and all its cell contents |
| `ExtractTextResponse` | success, page_count, text_blocks, raw_text, table_blocks, signature_image, processing_time_ms | The full response from one OCR job |
| `HealthResponse` | status | Simple liveness response |

---

## Database

**Technology:** PostgreSQL 16

> **Plain English:** A battle-tested, open-source relational database — like a set of well-organised spreadsheets that can handle thousands of simultaneous users.

### Tables

| Table | What it stores |
|---|---|
| `ocr_properties` | The list of data fields to extract (e.g. InvoiceNumber, DateOfBirth). Pre-loaded with 20 fields across Billing, Legal, and Hospital categories. |
| `analysis_results` | One row per uploaded document: the filename, raw OCR text, the original image bytes, any saved signature image, and a JSON column storing detected table blocks. |
| `saved_fields` | One row per extracted field per document: the field name, the value OCR found, the user's manual correction (if any), and a confidence score. |
| `saved_text_blocks` | One row per word found by Tesseract: the text, confidence score, page number, and pixel position. Powers the Raw OCR Tokens table on the results page. |

---

## Pre-loaded Extraction Fields

The system ships with 20 ready-to-use fields, grouped by document type:

| Category | Fields |
|---|---|
| **General** | Signature, FullName, DateOfBirth, BillingTotal, ProcessingFee |
| **Billing / Invoice** | InvoiceNumber, InvoiceDate, DueDate, AccountNumber, TaxAmount |
| **Legal** | CaseNumber, ContractDate, LegalParty, NotaryPublic, DocumentNumber |
| **Hospital / Medical** | PatientId, DiagnosisCode, AdmissionDate, DischargeDate, PhysicianName |

New fields can be added at any time through the Properties management page without any code changes.

---

## Security & Reliability Features

| Feature | What it means in practice |
|---|---|
| **API Key Authentication** | Every request from the frontend must include a secret key. Unauthenticated requests are rejected. The `/health` endpoint is exempt so monitoring tools always work. |
| **Rate Limiting** | The document analysis endpoint is limited to 10 uploads per minute to prevent overload. |
| **File Type Validation** | Only PNG, JPEG, and PDF files are accepted. Other file types are rejected immediately. |
| **File Size Limit** | Files over 20 MB are rejected before any processing begins. |
| **CORS Policy** | The backend only accepts browser requests from the configured frontend domain. |
| **Non-root Containers** | All three services run as non-privileged users inside Docker for defence-in-depth. |
| **Health Checks** | Docker automatically restarts any service that stops responding. |
| **Database Migration on Start** | The database schema is automatically kept up to date every time the backend starts. No manual upgrade steps. |

---

## Deployment

All three services and the database start with a single command:

```
docker-compose up --build
```

Once running, the application is available at:

| Address | What it opens |
|---|---|
| `http://localhost:3000` | The web application |
| `http://localhost:5000/swagger` | Interactive API documentation |
| `http://localhost:8000/health` | OCR service health check |

---

---

## Session 11 Additions (2026-05-05)

Three new capabilities were added in this session:

### 1 — Word-Level Bounding Box Overlay

The document preview panel on the results page now has a toggle between **Word Overlay** mode and **Capture Signature** mode.

In overlay mode, a canvas is drawn on top of the document image showing every word Tesseract recognised, colour-coded:
- **Green** — confidence ≥ 80% (reliable)
- **Yellow** — confidence 50–79% (marginal)
- **Red** — confidence < 50% (low confidence)

Hovering over any box shows a tooltip with the exact word and confidence percentage. Multi-page documents show Previous/Next controls.

New component: `frontend/src/components/results/BoundingBoxOverlay.tsx`

### 2 — Table Structure Detection

The OCR service now analyses the positional layout of word blocks on each page to find tables — rows and columns of aligned text — without any additional image processing.

Detected tables are stored in a `table_blocks_json` column in the `analysis_results` database table and displayed in a new **Detected Tables** section on the results page, below the OCR tokens table.

New files:
- `ocr-service/table_detector.py` — pure positional table detection
- `frontend/src/components/results/TableBlocksView.tsx` — renders detected tables as HTML tables
- `backend/Migrations/20260505000000_AddTableBlocksJson.cs` — database migration

### 3 — Real-Time Analysis Progress (Server-Sent Events)

When the user clicks "Analyze Document", instead of a spinner the upload page now shows a live step-by-step progress list:

> ✓ Decoding document…  
> ✓ Preprocessing image…  
> ✓ Running OCR on page 1…  
> ⟳ Processing…

This is implemented end-to-end with Server-Sent Events (SSE):
- **OCR service** (`/extract-text-stream`) yields a `status` event after each step using `asyncio.to_thread` for blocking calls
- **Backend** (`POST /api/documents/analyze/stream`) proxies the OCR stream and emits `done` with the document ID when complete
- **Frontend** (`api.ts` → `analyzeDocumentStream`) reads the stream with `fetch()` + `ReadableStream` and calls an `onProgress` callback for each step

**Note:** To apply the database migration, run `dotnet ef database update` inside the `backend/` directory.

---

*Last updated: 2026-05-05*
