"""
OCR Service — FastAPI application.

Provides a health-check endpoint, a synchronous multipart upload endpoint
that extracts text from images (PNG / JPEG) and PDFs using Tesseract OCR,
and a streaming endpoint that emits Server-Sent Events for real-time progress.
"""

import asyncio
import base64
import io
import json
import logging
import time
from collections.abc import AsyncGenerator
from typing import Optional

import numpy as np
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import JSONResponse, StreamingResponse
from PIL import Image
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request as StarletteRequest
from starlette.responses import Response

from extractor import extract_from_image
from models import ExtractPagesResponse, ExtractTextResponse, HealthResponse, TextBlock
from pdf_handler import pdf_to_images
from signature_detector import detect_signature
from table_detector import detect_tables

# ── Structured logging ─────────────────────────────────────────────────────────
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="OCR Service", version="1.0.0")

ALLOWED_CONTENT_TYPES = {"image/png", "image/jpeg", "image/jpg", "application/pdf"}
MAX_FILE_SIZE_BYTES   = 20 * 1024 * 1024  # 20 MB
MAX_REQUEST_BODY_MB   = 20
_PAGE_IMAGE_MAX_WIDTH = 1200  # cap display resolution to keep response size reasonable


# ── Payload size middleware ────────────────────────────────────────────────────
class MaxBodySizeMiddleware(BaseHTTPMiddleware):
    """Reject requests whose Content-Length header exceeds the configured limit."""

    async def dispatch(self, request: StarletteRequest, call_next):
        content_length = request.headers.get("content-length")
        if content_length:
            if int(content_length) > MAX_REQUEST_BODY_MB * 1024 * 1024:
                return Response(
                    content='{"detail": "Request body too large"}',
                    status_code=413,
                    media_type="application/json",
                )
        return await call_next(request)


app.add_middleware(MaxBodySizeMiddleware)


def _encode_page_image(img_array: np.ndarray) -> str:
    """Encode a numpy RGB image as a base64 JPEG string for storage."""
    pil = Image.fromarray(img_array)
    if pil.width > _PAGE_IMAGE_MAX_WIDTH:
        ratio = _PAGE_IMAGE_MAX_WIDTH / pil.width
        new_h = int(pil.height * ratio)
        pil   = pil.resize((_PAGE_IMAGE_MAX_WIDTH, new_h), Image.Resampling.LANCZOS)
    buf = io.BytesIO()
    pil.save(buf, format="JPEG", quality=85)
    return base64.b64encode(buf.getvalue()).decode("ascii")


def _sse(event: str, data: dict) -> str:
    """Format a single Server-Sent Event frame."""
    return f"event: {event}\ndata: {json.dumps(data)}\n\n"


@app.get("/health", response_model=HealthResponse)
async def health() -> HealthResponse:
    """Return a simple liveness response so orchestrators can probe the service."""
    return HealthResponse(status="ok")


@app.post("/extract-pages", response_model=ExtractPagesResponse)
async def extract_pages(
    file: UploadFile = File(...),
) -> ExtractPagesResponse:
    """Convert an uploaded image or PDF to page images without running OCR.

    Returns base64 JPEG images for each page. Used by the .NET backend when
    handwriting mode is active — it calls this endpoint to get page images,
    then sends each to Claude Vision for transcription.
    """
    if file.content_type not in ALLOWED_CONTENT_TYPES:
        raise HTTPException(
            status_code=415,
            detail=(
                f"Unsupported media type '{file.content_type}'. "
                f"Allowed: {', '.join(sorted(ALLOWED_CONTENT_TYPES))}"
            ),
        )

    file_bytes: bytes = await file.read()

    if len(file_bytes) > MAX_FILE_SIZE_BYTES:
        raise HTTPException(
            status_code=413,
            detail=f"File size {len(file_bytes)} bytes exceeds the {MAX_FILE_SIZE_BYTES // (1024 * 1024)} MB limit.",
        )

    if file.content_type == "application/pdf":
        images: list = await asyncio.to_thread(pdf_to_images, file_bytes)
    else:
        pil_image = Image.open(io.BytesIO(file_bytes)).convert("RGB")
        images = [np.array(pil_image)]

    page_images: list[str] = await asyncio.to_thread(
        lambda: [_encode_page_image(img) for img in images]
    )

    return ExtractPagesResponse(page_count=len(images), page_images=page_images)


@app.post("/extract-text", response_model=ExtractTextResponse)
async def extract_text(
    file: UploadFile = File(...),
    lang: str = Form("eng"),
    crop_x: Optional[int] = Form(None),
    crop_y: Optional[int] = Form(None),
    crop_width: Optional[int] = Form(None),
    crop_height: Optional[int] = Form(None),
) -> ExtractTextResponse:
    """Extract text from an uploaded image or PDF file.

    Accepts PNG, JPEG, and PDF uploads up to 20 MB. An optional crop region
    (crop_x, crop_y, crop_width, crop_height) may be supplied to restrict OCR
    to a rectangular area of each page. The lang parameter accepts any
    Tesseract language code (e.g. "eng", "spa", "eng+fra").

    Returns structured text blocks, raw text, timing, JPEG previews of each
    page, auto-detected signature, and detected table structure.
    """
    start = time.perf_counter()

    try:
        # ── Validate content type ──────────────────────────────────────────────
        if file.content_type not in ALLOWED_CONTENT_TYPES:
            logger.warning("Unsupported content type: %s", file.content_type)
            raise HTTPException(
                status_code=415,
                detail=(
                    f"Unsupported media type '{file.content_type}'. "
                    f"Allowed: {', '.join(sorted(ALLOWED_CONTENT_TYPES))}"
                ),
            )

        # ── Read and size-check the upload ─────────────────────────────────────
        file_bytes: bytes = await file.read()

        logger.info(
            "Received file: type=%s size=%d bytes lang=%s",
            file.content_type,
            len(file_bytes),
            lang,
        )

        if len(file_bytes) > MAX_FILE_SIZE_BYTES:
            raise HTTPException(
                status_code=413,
                detail=(
                    f"File size {len(file_bytes)} bytes exceeds the "
                    f"{MAX_FILE_SIZE_BYTES // (1024 * 1024)} MB limit."
                ),
            )

        # ── Decode to list of numpy arrays (one per page) ──────────────────────
        if file.content_type == "application/pdf":
            images: list[np.ndarray] = pdf_to_images(file_bytes)
        else:
            pil_image = Image.open(io.BytesIO(file_bytes)).convert("RGB")
            images    = [np.array(pil_image)]

        # ── Encode page images for storage / display ───────────────────────────
        page_images: list[str] = [_encode_page_image(img) for img in images]

        # ── Crop and run OCR on every page ─────────────────────────────────────
        crop_provided = all(
            param is not None for param in (crop_x, crop_y, crop_width, crop_height)
        )

        all_blocks: list[TextBlock] = []
        for i, image in enumerate(images):
            ocr_image = image
            if crop_provided:
                ocr_image = image[
                    crop_y: crop_y + crop_height,  # type: ignore[index]
                    crop_x: crop_x + crop_width,   # type: ignore[index]
                ]
            all_blocks.extend(extract_from_image(ocr_image, page=i + 1, lang=lang))

        # ── Auto-detect signature (scan each page, return first hit) ───────────
        signature_image: str | None = None
        for i, image in enumerate(images):
            sig = detect_signature(image, all_blocks, page=i + 1)
            if sig:
                signature_image = sig
                break

        # ── Detect table structure ─────────────────────────────────────────────
        table_blocks = []
        for page_num in range(1, len(images) + 1):
            table_blocks.extend(detect_tables(all_blocks, page_num))

        # ── Assemble response ──────────────────────────────────────────────────
        raw_text   = " ".join(block.text for block in all_blocks)
        elapsed_ms = (time.perf_counter() - start) * 1000.0

        return ExtractTextResponse(
            success=True,
            page_count=len(images),
            text_blocks=all_blocks,
            raw_text=raw_text,
            processing_time_ms=elapsed_ms,
            page_images=page_images,
            signature_image=signature_image,
            table_blocks=table_blocks,
        )

    except HTTPException:
        raise
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except Exception as e:
        logger.error("OCR processing failed: %s", str(e))
        raise HTTPException(
            status_code=500,
            detail="An unexpected error occurred while processing the file.",
        )


@app.post("/extract-text-stream")
async def extract_text_stream(
    file: UploadFile = File(...),
    lang: str = Form("eng"),
    crop_x: Optional[int] = Form(None),
    crop_y: Optional[int] = Form(None),
    crop_width: Optional[int] = Form(None),
    crop_height: Optional[int] = Form(None),
) -> StreamingResponse:
    """Stream OCR progress as Server-Sent Events.

    Emits ``status`` events for each processing step and a final ``complete``
    event whose data is the full ExtractTextResponse JSON.  On error emits an
    ``error`` event and closes the stream.
    """
    start = time.perf_counter()

    async def event_stream() -> AsyncGenerator[str, None]:
        try:
            if file.content_type not in ALLOWED_CONTENT_TYPES:
                yield _sse("error", {
                    "message": (
                        f"Unsupported media type '{file.content_type}'. "
                        f"Allowed: {', '.join(sorted(ALLOWED_CONTENT_TYPES))}"
                    )
                })
                return

            yield _sse("status", {"step": "decode", "message": "Reading and validating document"})
            file_bytes: bytes = await file.read()

            if len(file_bytes) > MAX_FILE_SIZE_BYTES:
                yield _sse("error", {
                    "message": (
                        f"File size {len(file_bytes)} bytes exceeds the "
                        f"{MAX_FILE_SIZE_BYTES // (1024 * 1024)} MB limit."
                    )
                })
                return

            if file.content_type == "application/pdf":
                yield _sse("status", {"step": "decode", "message": "Converting PDF pages to images"})
                images: list = await asyncio.to_thread(pdf_to_images, file_bytes)
            else:
                pil_image = Image.open(io.BytesIO(file_bytes)).convert("RGB")
                images    = [np.array(pil_image)]

            total = len(images)

            yield _sse("status", {"step": "preprocess", "message": f"Preprocessing {total} page(s)"})
            page_images: list[str] = await asyncio.to_thread(
                lambda: [_encode_page_image(img) for img in images]
            )

            crop_provided = all(
                p is not None for p in (crop_x, crop_y, crop_width, crop_height)
            )

            all_blocks: list = []
            for i, image in enumerate(images):
                yield _sse("status", {
                    "step":    "ocr",
                    "message": f"Running OCR on page {i + 1} of {total}",
                    "page":    i + 1,
                    "total":   total,
                })
                ocr_image = image
                if crop_provided:
                    ocr_image = image[
                        crop_y: crop_y + crop_height,  # type: ignore[index]
                        crop_x: crop_x + crop_width,   # type: ignore[index]
                    ]
                blocks = await asyncio.to_thread(extract_from_image, ocr_image, i + 1, lang)
                all_blocks.extend(blocks)

            yield _sse("status", {"step": "signature", "message": "Detecting signatures"})
            signature_image: str | None = None
            for i, image in enumerate(images):
                sig = await asyncio.to_thread(detect_signature, image, all_blocks, i + 1)
                if sig:
                    signature_image = sig
                    break

            yield _sse("status", {"step": "tables", "message": "Detecting table structure"})
            table_blocks = []
            for page_num in range(1, total + 1):
                table_blocks.extend(detect_tables(all_blocks, page_num))

            raw_text   = " ".join(b.text for b in all_blocks)
            elapsed_ms = (time.perf_counter() - start) * 1000.0

            result = ExtractTextResponse(
                success=True,
                page_count=total,
                text_blocks=all_blocks,
                raw_text=raw_text,
                processing_time_ms=elapsed_ms,
                page_images=page_images,
                signature_image=signature_image,
                table_blocks=table_blocks,
            )
            yield _sse("complete", result.model_dump())

        except Exception as exc:
            logger.error("extract_text_stream error: %s", str(exc))
            yield _sse("error", {"message": "An unexpected error occurred during processing."})

    return StreamingResponse(event_stream(), media_type="text/event-stream")
