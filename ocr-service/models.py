"""
Pydantic models for the OCR Service API.

Defines request and response schemas used by the FastAPI endpoints.
"""

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


class ExtractTextResponse(BaseModel):
    success: bool
    page_count: int
    text_blocks: list[TextBlock]
    raw_text: str
    processing_time_ms: float


class HealthResponse(BaseModel):
    status: str
