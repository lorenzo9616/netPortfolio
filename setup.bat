@echo off
setlocal

echo.
echo ================================================
echo  OpenOCR Setup
echo ================================================
echo.

:: Step 1: Create .env if missing
if not exist ".env" (
    copy ".env.example" ".env" > nul
    echo [OK] Created .env from .env.example
) else (
    echo [OK] .env already exists - skipping
)

:: Step 2: Check Docker is running
docker info > nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Docker is not running.
    echo         Please open Docker Desktop and wait for it to start, then run this script again.
    echo.
    pause
    exit /b 1
)
echo [OK] Docker is running

:: Step 3: Start containers
echo.
echo Starting services (this may take a few minutes on first run)...
docker compose up --build -d
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] docker compose failed. Run "docker compose logs" to see what went wrong.
    pause
    exit /b 1
)

:: Step 4: Wait for backend health
echo.
echo Waiting for services to become healthy...
set /a attempts=0
:wait_loop
    set /a attempts+=1
    if %attempts% gtr 20 (
        echo.
        echo [ERROR] Services did not become healthy after 60 seconds.
        echo         Run "docker compose logs" to diagnose the problem.
        pause
        exit /b 1
    )
    timeout /t 3 /nobreak > nul
    curl -s -f http://localhost:5000/health > nul 2>&1
    if %errorlevel% equ 0 goto :ready
    echo   Attempt %attempts%/20 - still waiting...
    goto :wait_loop

:ready
echo.
echo ================================================
echo  OpenOCR is running!
echo.
echo  Frontend:        http://localhost:3000
echo  Backend Swagger: http://localhost:5000/swagger
echo  OCR Health:      http://localhost:8000/health
echo.
echo  Default login:   admin / openocr2024
echo ================================================
echo.
pause
