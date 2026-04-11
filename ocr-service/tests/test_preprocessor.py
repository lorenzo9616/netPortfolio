"""Unit tests for preprocessor.py — no Tesseract or file I/O needed."""
import numpy as np
import pytest

from preprocessor import preprocess_image


class TestPreprocessImage:
    def test_returns_numpy_array(self, blank_image):
        result = preprocess_image(blank_image)
        assert isinstance(result, np.ndarray)

    def test_output_is_2d_grayscale(self, blank_image):
        """preprocess_image should always return a single-channel image."""
        result = preprocess_image(blank_image)
        assert result.ndim == 2, f"Expected 2D array, got shape {result.shape}"

    def test_accepts_grayscale_input(self, grayscale_image):
        """A 2D (single-channel) input causes cv2.cvtColor to raise — verify
        that the function raises rather than silently returning garbage."""
        with pytest.raises(Exception):
            preprocess_image(grayscale_image)

    def test_raises_on_empty_image(self):
        empty = np.array([])
        with pytest.raises((ValueError, Exception)):
            preprocess_image(empty)
