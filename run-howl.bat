@echo off
echo ====================================
echo         Howl - Starting...
echo ====================================
echo.

REM Check if GEMINI_API_KEY is set (optional now)
if "%GEMINI_API_KEY%"=="" (
    echo Note: GEMINI_API_KEY environment variable is not set.
    echo You can:
    echo   1. Enter it in the app when needed, OR
    echo   2. Use Debug Mode to skip AI processing
    echo.
) else (
    echo Gemini API Key: Found in environment
    echo.
)

echo Building Howl...
dotnet build --nologo --verbosity quiet

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo Build successful!
echo.
echo Launching Howl...
echo.

start "" "src\Howl.Desktop\Howl.Desktop\bin\Debug\net9.0-windows\Howl.Desktop.exe"

echo Howl is running!
echo You can close this window.
echo.
