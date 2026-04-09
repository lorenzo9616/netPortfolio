"""
Image preprocessing utilities for OCR quality improvement.

Applies grayscale conversion, noise reduction, and adaptive thresholding
to prepare images for Tesseract OCR.
"""

import cv2
import numpy as np


def preprocess_image(image: np.ndarray) -> np.ndarray:
    """Convert an image to a binarised, noise-reduced form suitable for OCR.

    Steps applied in order:
    1. Grayscale conversion — reduces colour channels to one.
    2. Gaussian blur (3x3) — suppresses high-frequency noise.
    3. Adaptive Gaussian thresholding — produces a clean binary image that
       handles varying illumination across the document.

    Args:
        image: Input image as a NumPy array (BGR or grayscale, uint8).

    Returns:
        Preprocessed binary image as a NumPy array (single channel, uint8).
    """
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    blurred = cv2.GaussianBlur(gray, (3, 3), 0)

    binary = cv2.adaptiveThreshold(
        blurred,
        255,
        cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
        cv2.THRESH_BINARY,
        blockSize=11,
        C=2,
    )

    return binary
