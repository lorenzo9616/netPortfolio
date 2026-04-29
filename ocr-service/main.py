"""
OCR Service — FastAPI application.

Provides a health-check endpoint and a multipart file upload endpoint that
extracts text from images (PNG / JPEG) and PDFs using Tesseract OCR.
"""

import base64
import io
import logging
import time
from typing import Optional

import numpy as np
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import JSONResponse
from PIL import Image
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request as StarletteRequest
from starlette.responses import Response

from extractor import extract_from_image
from models import ExtractTextResponse, HealthResponse, TextBlock
from pdf_handler import pdf_to_images

# ── Structured logging ─────────────────────────────────────────────────────────
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="OCR Service", version="1.0.0")

ALLOWED_CONTENT_TYPES = {"image/png", "image/jpeg", "image/jpg", "application/pdf"}
MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024  # 20 MB
MAX_REQUEST_BODY_MB = 20
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
        pil = pil.resize((_PAGE_IMAGE_MAX_WIDTH, new_h), Image.Resampling.LANCZOS)
    buf = io.BytesIO()
    pil.save(buf, format="JPEG", quality=85)
    return base64.b64encode(buf.getvalue()).decode("ascii")


@app.get("/health", response_model=HealthResponse)
async def health() -> HealthResponse:
    """Return a simple liveness response so orchestrators can probe the service."""
    return HealthResponse(status="ok")


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

    Returns structured text blocks, raw text, timing, and JPEG previews of
    each page (base64-encoded) for display in the frontend.
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
            images = [np.array(pil_image)]

        # ── Encode page images for storage / display ───────────────────────────
        page_images: list[str] = [_encode_page_image(img) for img in images]

        # ── Crop and run OCR on every page ─────────────────────────────────────
        crop_provided = all(
            param is not None for param in (crop_x, crop_y, crop_width, crop_height)
        )

        all_blocks: list[TextBlock] = []
        for i, image in enumerate(images):
            if crop_provided:
                image = image[
                    crop_y: crop_y + crop_height,  # type: ignore[index]
                    crop_x: crop_x + crop_width,   # type: ignore[index]
                ]
            all_blocks.extend(extract_from_image(image, page=i + 1, lang=lang))

        # ── Assemble response ──────────────────────────────────────────────────
        raw_text = " ".join(block.text for block in all_blocks)
        elapsed_ms = (time.perf_counter() - start) * 1000.0

        return ExtractTextResponse(
            success=True,
            page_count=len(images),
            text_blocks=all_blocks,
            raw_text=raw_text,
            processing_time_ms=elapsed_ms,
            page_images=page_images,
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
