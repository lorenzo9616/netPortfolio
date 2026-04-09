# ---- Base image ----
FROM python:3.11-slim AS base

# Install OS-level dependencies required by Tesseract, poppler, and OpenCV
RUN apt-get update && apt-get install -y --no-install-recommends \
        tesseract-ocr \
        tesseract-ocr-eng \
        poppler-utils \
        libgl1 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# ---- Dependencies ----
FROM base AS deps
WORKDIR /app

COPY requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip \
 && pip install --no-cache-dir -r requirements.txt

# ---- Runtime ----
FROM deps AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd --system --gid 1001 appgroup \
 && useradd  --system --uid 1001 --gid appgroup appuser

COPY --chown=appuser:appgroup . .

USER appuser

EXPOSE 8000

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
