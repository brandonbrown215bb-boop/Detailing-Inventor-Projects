@echo off
setlocal
cd /d "%~dp0"

echo ===================================================
echo Unit Progress Tracker - Build Executable (.EXE)
echo ===================================================
echo.
echo Publishing UnitProgressTracker.Wpf (Release)...
dotnet publish src/UnitProgressTracker.Wpf/UnitProgressTracker.Wpf.csproj -c Release -o bin/Publish

if errorlevel 1 (
    echo.
    echo [ERROR] Build/Publish failed!
    pause
    exit /b 1
)

echo.
echo ===================================================
echo [SUCCESS] Executable built successfully!
echo Executable Location: bin\Publish\UnitProgressTracker.Wpf.exe
echo ===================================================
echo.
pause
exit /b 0
