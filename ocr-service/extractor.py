"""
OCR extraction logic wrapping Tesseract via pytesseract.

Preprocesses the image and returns structured TextBlock results with
per-word confidence scores and bounding-box coordinates.
"""

import numpy as np
import pytesseract

from models import BoundingBox, TextBlock
from preprocessor import preprocess_image


def extract_from_image(image: np.ndarray, page: int = 1) -> list[TextBlock]:
    """Run OCR on a single image and return a list of TextBlock objects.

    The image is preprocessed before being passed to Tesseract. Only entries
    with a valid confidence score (conf != -1) and non-empty text are kept.

    Args:
        image: Input image as a NumPy array (BGR, uint8).
        page:  1-based page number to embed in each TextBlock (default 1).

    Returns:
        List of TextBlock instances, one per recognised word, sorted in the
        order Tesseract emits them (top-to-bottom, left-to-right).
    """
    preprocessed = preprocess_image(image)

    data = pytesseract.image_to_data(
        preprocessed,
        output_type=pytesseract.Output.DICT,
    )

    blocks: list[TextBlock] = []

    for i, conf in enumerate(data["conf"]):
        if conf == -1:
            continue

        text: str = data["text"][i]
        if not text.strip():
            continue

        bounding_box = BoundingBox(
            x=data["left"][i],
            y=data["top"][i],
            width=data["width"][i],
            height=data["height"][i],
        )

        blocks.append(
            TextBlock(
                text=text,
                confidence=float(conf),
                bounding_box=bounding_box,
                page=page,
            )
        )

    return blocks
