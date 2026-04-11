"""Unit tests for pdf_handler.py — pdf2image mocked."""
import numpy as np
import pytest
from PIL import Image

from pdf_handler import pdf_to_images


class TestPdfToImages:
    def test_returns_list_of_numpy_arrays(self, mocker):
        fake_pil = Image.fromarray(np.full((100, 100, 3), 200, dtype=np.uint8))
        mocker.patch("pdf2image.convert_from_bytes", return_value=[fake_pil, fake_pil])

        result = pdf_to_images(b"fake-pdf-bytes")

        assert isinstance(result, list)
        assert len(result) == 2
        assert all(isinstance(img, np.ndarray) for img in result)

    def test_raises_on_empty_bytes(self, mocker):
        mocker.patch(
            "pdf2image.convert_from_bytes",
            side_effect=ValueError("Empty PDF"),
        )
        with pytest.raises(ValueError):
            pdf_to_images(b"")

    def test_each_image_is_rgb(self, mocker):
        fake_pil = Image.fromarray(np.full((50, 50, 3), 128, dtype=np.uint8))
        mocker.patch("pdf2image.convert_from_bytes", return_value=[fake_pil])

        result = pdf_to_images(b"fake-pdf-bytes")

        assert result[0].ndim == 3
        assert result[0].shape[2] == 3
