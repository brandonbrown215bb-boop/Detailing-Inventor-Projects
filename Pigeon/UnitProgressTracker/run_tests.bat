@echo off
setlocal
cd /d "%~dp0"

echo ===================================================
echo Unit Progress Tracker - Run Test Suite
echo ===================================================
echo.
echo Running unit tests...
dotnet test UnitProgressTracker.sln -c Release --verbosity normal

if errorlevel 1 (
    echo.
    echo [ERROR] Test execution failed or some tests did not pass.
    pause
    exit /b 1
)

echo.
echo ===================================================
echo [SUCCESS] All unit tests passed!
echo ===================================================
echo.
pause
exit /b 0
