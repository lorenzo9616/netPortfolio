.PHONY: up down logs seed test test-e2e reset help

## Start all services (build if needed)
up:
	docker compose up --build -d

## Stop all services
down:
	docker compose down

## Tail logs for all services
logs:
	docker compose logs -f

## Re-run seed data (restart backend to re-trigger startup seed)
seed:
	docker compose restart backend

## Run all unit test suites (backend + ocr-service + frontend)
test:
	@echo "=== Backend tests ==="
	dotnet test backend/OcrApi.Tests/
	@echo "=== OCR service tests ==="
	cd ocr-service && pip install -q -r requirements.txt && pytest tests/ -q
	@echo "=== Frontend tests ==="
	cd frontend && npm test -- --run

## Run E2E tests (requires running stack)
test-e2e:
	cd e2e && npx playwright test

## Stop services and delete all data volumes (full reset)
reset:
	@echo "WARNING: This deletes all data. Press Ctrl+C to cancel, or Enter to continue."
	@bash -c 'read -r confirm'
	docker compose down -v
	$(MAKE) up

## Show available commands
help:
	@echo "Available commands:"
	@echo "  make up        - Start all services"
	@echo "  make down      - Stop all services"
	@echo "  make logs      - Tail all service logs"
	@echo "  make seed      - Re-run seed data"
	@echo "  make test      - Run unit test suites"
	@echo "  make test-e2e  - Run E2E tests (stack must be running)"
	@echo "  make reset     - Wipe data and restart (destructive!)"
