@echo off
setlocal
cd /d "%~dp0"

echo ===================================================
echo Unit Progress Tracker - Compile
echo ===================================================
dotnet build UnitProgressTracker.sln -c Debug
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Build completed.
echo Output: src\UnitProgressTracker.Wpf\bin\Debug\net8.0-windows\
pause
exit /b 0
