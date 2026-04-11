"""Integration smoke test — only runs when marked -m integration."""
import pytest
import httpx


@pytest.mark.integration
def test_health_returns_ok():
    response = httpx.get("http://localhost:8000/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}
