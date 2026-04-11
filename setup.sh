#!/usr/bin/env bash
set -e

echo ""
echo "================================================"
echo " OpenOCR Setup"
echo "================================================"
echo ""

# Step 1: Create .env if missing
if [ ! -f ".env" ]; then
    cp .env.example .env
    echo "[OK] Created .env from .env.example"
else
    echo "[OK] .env already exists - skipping"
fi

# Step 2: Check Docker is running
if ! docker info > /dev/null 2>&1; then
    echo ""
    echo "[ERROR] Docker is not running."
    echo "        Please start Docker Desktop and re-run this script."
    echo ""
    exit 1
fi
echo "[OK] Docker is running"

# Step 3: Start containers
echo ""
echo "Starting services (this may take a few minutes on first run)..."
docker compose up --build -d

# Step 4: Wait for backend health
echo ""
echo "Waiting for services to become healthy..."
attempts=0
until curl -sf http://localhost:5000/health > /dev/null 2>&1; do
    attempts=$((attempts + 1))
    if [ $attempts -ge 20 ]; then
        echo ""
        echo "[ERROR] Services did not become healthy after 60 seconds."
        echo "        Run 'docker compose logs' to diagnose."
        exit 1
    fi
    echo "  Attempt $attempts/20 - still waiting..."
    sleep 3
done

echo ""
echo "================================================"
echo " OpenOCR is running!"
echo ""
echo " Frontend:        http://localhost:3000"
echo " Backend Swagger: http://localhost:5000/swagger"
echo " OCR Health:      http://localhost:8000/health"
echo ""
echo " Default login:   admin / openocr2024"
echo "================================================"
echo ""
