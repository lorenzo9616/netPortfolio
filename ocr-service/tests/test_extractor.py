"""Unit tests for extractor.py — pytesseract mocked."""
import numpy as np
import pytest

from extractor import extract_from_image
from models import TextBlock


def _make_tesseract_dict(words: list[dict]) -> dict:
    """Build a pytesseract Output.DICT-style dictionary from a list of word specs."""
    defaults = {
        "level": 5, "page_num": 1, "block_num": 1, "par_num": 1,
        "line_num": 1, "word_num": 1, "left": 10, "top": 10,
        "width": 50, "height": 20, "conf": 90, "text": "word",
    }
    rows = [{**defaults, **w} for w in words]
    # Transpose list-of-dicts to dict-of-lists (pytesseract Output.DICT format)
    if not rows:
        return {k: [] for k in defaults}
    return {k: [row[k] for row in rows] for k in rows[0]}


class TestExtractFromImage:
    def test_returns_list(self, mocker, blank_image):
        mocker.patch(
            "pytesseract.image_to_data",
            return_value=_make_tesseract_dict([{"text": "Hello", "conf": 95}]),
        )
        blocks = extract_from_image(blank_image, page=1)
        assert isinstance(blocks, list)

    def test_each_block_has_text_and_confidence(self, mocker, blank_image):
        mocker.patch(
            "pytesseract.image_to_data",
            return_value=_make_tesseract_dict([
                {"text": "Hello", "conf": 95},
                {"text": "World", "conf": 80},
            ]),
        )
        blocks = extract_from_image(blank_image, page=1)
        for block in blocks:
            assert hasattr(block, "text")
            assert hasattr(block, "confidence")

    def test_empty_output_returns_empty_list(self, mocker, blank_image):
        mocker.patch(
            "pytesseract.image_to_data",
            return_value=_make_tesseract_dict([]),
        )
        blocks = extract_from_image(blank_image, page=1)
        assert blocks == []

    def test_low_confidence_filtered(self, mocker, blank_image):
        mocker.patch(
            "pytesseract.image_to_data",
            return_value=_make_tesseract_dict([
                {"text": "GoodWord", "conf": 85},
                {"text": "",         "conf": -1},
            ]),
        )
        blocks = extract_from_image(blank_image, page=1)
        assert all(b.text.strip() != "" for b in blocks)
