"""Shared fixtures and configuration for pytest."""
import numpy as np
import pytest


@pytest.fixture
def blank_image() -> np.ndarray:
    """Returns a small blank white RGB image as a numpy array."""
    return np.full((100, 200, 3), 255, dtype=np.uint8)


@pytest.fixture
def grayscale_image() -> np.ndarray:
    """Returns a small blank white grayscale image as a numpy array."""
    return np.full((100, 200), 255, dtype=np.uint8)
