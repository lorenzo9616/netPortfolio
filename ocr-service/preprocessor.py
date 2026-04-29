"""
Image preprocessing utilities for OCR quality improvement.

Applies grayscale conversion, deskew, noise reduction, and adaptive
thresholding to prepare images for Tesseract OCR.
"""

import cv2
import numpy as np


def _deskew(gray: np.ndarray) -> np.ndarray:
    """Correct rotational skew in a grayscale image.

    Uses Otsu thresholding to isolate text pixels, fits a minimum bounding
    rectangle to find the skew angle, then rotates to correct it.
    Skips correction when the detected angle is less than 0.5° (negligible).

    Args:
        gray: Grayscale image as a NumPy array (single channel, uint8).

    Returns:
        Deskewed grayscale image, or the original if no correction is needed.
    """
    _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    # np.where returns (rows, cols); minAreaRect expects (x, y) = (col, row)
    coords = np.column_stack(np.where(binary > 0))
    if len(coords) < 5:
        return gray
    coords_xy = np.float32(coords[:, ::-1])
    angle = cv2.minAreaRect(coords_xy)[-1]
    if angle < -45:
        angle = -(90 + angle)
    else:
        angle = -angle
    if abs(angle) < 0.5:
        return gray
    h, w = gray.shape
    M = cv2.getRotationMatrix2D((w / 2.0, h / 2.0), angle, 1.0)
    return cv2.warpAffine(gray, M, (w, h), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)


def preprocess_image(image: np.ndarray) -> np.ndarray:
    """Convert an image to a binarised, noise-reduced form suitable for OCR.

    Steps applied in order:
    1. Grayscale conversion — reduces colour channels to one.
    2. Deskew — corrects rotational tilt using minimum bounding rectangle.
    3. Gaussian blur (3x3) — suppresses high-frequency noise.
    4. Adaptive Gaussian thresholding — produces a clean binary image that
       handles varying illumination across the document.

    Args:
        image: Input image as a NumPy array (BGR or grayscale, uint8).

    Returns:
        Preprocessed binary image as a NumPy array (single channel, uint8).
    """
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    gray = _deskew(gray)

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
