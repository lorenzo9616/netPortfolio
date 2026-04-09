"""
PDF-to-image conversion utilities using pdf2image (Poppler backend).

Converts PDF bytes into a list of NumPy arrays, one per page, ready for
downstream image processing and OCR.
"""

import numpy as np
from pdf2image import convert_from_bytes


def pdf_to_images(file_bytes: bytes, dpi: int = 200) -> list[np.ndarray]:
    """Convert PDF bytes into a list of per-page NumPy image arrays.

    Args:
        file_bytes: Raw bytes of the PDF file.
        dpi:        Resolution at which to rasterise each page (default 200).

    Returns:
        List of NumPy arrays (RGB, uint8), one element per PDF page.

    Raises:
        ValueError: If pdf2image fails to convert the provided bytes, for
                    example because the file is corrupted or not a valid PDF.
    """
    try:
        pil_images = convert_from_bytes(file_bytes, dpi=dpi)
    except Exception as e:
        raise ValueError(f"Failed to convert PDF: {e}") from e

    return [np.array(img) for img in pil_images]
