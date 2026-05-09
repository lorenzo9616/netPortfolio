"""
Pydantic models for the OCR Service API.

Defines request and response schemas used by the FastAPI endpoints.
"""

from typing import Optional

from pydantic import BaseModel


class BoundingBox(BaseModel):
    x: int
    y: int
    width: int
    height: int


class TextBlock(BaseModel):
    text: str
    confidence: float
    bounding_box: BoundingBox
    page: int


class TableCell(BaseModel):
    row: int
    col: int
    text: str
    bounding_box: BoundingBox


class TableBlock(BaseModel):
    page: int
    rows: int
    cols: int
    cells: list[TableCell]
    bounding_box: BoundingBox


class ExtractTextResponse(BaseModel):
    success: bool
    page_count: int
    text_blocks: list[TextBlock]
    raw_text: str
    processing_time_ms: float
    page_images: list[str] = []
    signature_image: Optional[str] = None  # base64 JPEG, auto-detected from the document
    table_blocks: list[TableBlock] = []


class HealthResponse(BaseModel):
    status: str
