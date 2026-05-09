"""
Auto-detect and crop a handwritten signature from document images.

Uses title-keyword anchoring to locate the signature region above signatory
text blocks, then validates the region contains handwriting via pixel-density
analysis before cropping from the original image.
"""

import base64
import io
import logging

import cv2
import numpy as np
from PIL import Image

from models import TextBlock

logger = logging.getLogger(__name__)

_TITLE_KEYWORDS = frozenset({
    "president", "director", "ceo", "cfo", "coo", "vp", "chairman",
    "manager", "officer", "secretary", "signatory", "treasurer",
    "principal", "administrator", "partner", "authorized",
})

_MAX_SIG_HEIGHT = 280    # max pixel height to search above anchor (at 200 DPI)
_MIN_SIG_HEIGHT = 50     # skip region if it would be smaller than this
_MIN_DARK_RATIO  = 0.004 # minimum dark-pixel density — non-blank region
_MAX_DARK_RATIO  = 0.30  # maximum density — above this it's dense text, not a signature
_PADDING = 24            # white-space border added to each side of the crop


def detect_signature(
    original_image: np.ndarray,
    text_blocks: list[TextBlock],
    page: int = 1,
) -> str | None:
    """Detect a handwritten signature in a document page and return it as
    a base64-encoded JPEG string, or None if no signature region is found.

    Two-pass detection:
      1. Anchor search — finds title-keyword blocks (e.g. "President") and
         examines the region immediately above them for handwriting.
      2. Low-confidence fallback — clusters OCR tokens with confidence < 45
         as a proxy for cursive / handwritten content.

    Args:
        original_image: Original RGB page image as a NumPy array (not preprocessed).
        text_blocks:    All OCR text blocks returned by the extractor.
        page:           1-based page number to restrict block filtering.

    Returns:
        Base64 JPEG string of the cropped signature region, or None.
    """
    page_blocks = [b for b in text_blocks if b.page == page]
    if not page_blocks:
        return None

    h, w = original_image.shape[:2]

    # ── Pass 1: anchor-based detection ────────────────────────────────────────
    anchor_blocks = [
        b for b in page_blocks
        if b.text.strip().lower() in _TITLE_KEYWORDS
    ]

    if anchor_blocks:
        anchor_y_top = min(b.bounding_box.y for b in anchor_blocks)
        anchor_x_min = min(b.bounding_box.x for b in anchor_blocks)
        anchor_x_max = max(b.bounding_box.x + b.bounding_box.width for b in anchor_blocks)

        sig_y_end   = max(0, anchor_y_top - 8)
        sig_y_start = max(0, sig_y_end - _MAX_SIG_HEIGHT)

        if sig_y_end - sig_y_start >= _MIN_SIG_HEIGHT:
            sig_x_start = max(0, anchor_x_min - _PADDING * 2)
            sig_x_end   = min(w, anchor_x_max + _PADDING * 2)
            region = original_image[sig_y_start:sig_y_end, sig_x_start:sig_x_end]

            if _has_handwriting(region):
                logger.info(
                    "Signature detected via anchor '%s' at y=%d–%d x=%d–%d (page %d)",
                    anchor_blocks[0].text, sig_y_start, sig_y_end,
                    sig_x_start, sig_x_end, page,
                )
                return _encode_crop(original_image, h, w,
                                    sig_y_start, sig_y_end, sig_x_start, sig_x_end)

    # ── Pass 2: low-confidence cluster fallback ────────────────────────────────
    low_conf = [
        b for b in page_blocks
        if 0 < b.confidence < 45
        and b.bounding_box.y > h * 0.45  # signatures live in the lower half
    ]

    if len(low_conf) >= 2:
        lc_y_min = min(b.bounding_box.y for b in low_conf)
        lc_y_max = max(b.bounding_box.y + b.bounding_box.height for b in low_conf)
        lc_x_min = min(b.bounding_box.x for b in low_conf)
        lc_x_max = max(b.bounding_box.x + b.bounding_box.width for b in low_conf)
        region = original_image[lc_y_min:lc_y_max, lc_x_min:lc_x_max]

        if _has_handwriting(region):
            logger.info(
                "Signature detected via low-confidence cluster at y=%d–%d x=%d–%d (page %d)",
                lc_y_min, lc_y_max, lc_x_min, lc_x_max, page,
            )
            return _encode_crop(original_image, h, w,
                                lc_y_min, lc_y_max, lc_x_min, lc_x_max)

    return None


def _has_handwriting(region: np.ndarray) -> bool:
    """Return True if the region's dark-pixel ratio falls in the handwriting range."""
    if region.size == 0:
        return False
    gray = cv2.cvtColor(region, cv2.COLOR_RGB2GRAY) if region.ndim == 3 else region
    _, binary = cv2.threshold(gray, 200, 255, cv2.THRESH_BINARY_INV)
    ratio = float(np.sum(binary > 0)) / binary.size
    return _MIN_DARK_RATIO < ratio < _MAX_DARK_RATIO


def _encode_crop(
    image: np.ndarray,
    img_h: int,
    img_w: int,
    y_start: int,
    y_end: int,
    x_start: int,
    x_end: int,
) -> str:
    """Crop with padding and return the region as a base64 JPEG string."""
    y1 = max(0, y_start - _PADDING)
    y2 = min(img_h, y_end + _PADDING)
    x1 = max(0, x_start - _PADDING)
    x2 = min(img_w, x_end + _PADDING)

    cropped = image[y1:y2, x1:x2]
    pil_img = Image.fromarray(cropped)

    buf = io.BytesIO()
    pil_img.save(buf, format="JPEG", quality=95)
    return base64.b64encode(buf.getvalue()).decode("ascii")
