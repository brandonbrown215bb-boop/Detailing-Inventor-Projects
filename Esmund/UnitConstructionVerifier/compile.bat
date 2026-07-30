@echo off
setlocal
cd /d "%~dp0"

echo ===================================================
echo Unit Construction Verifier - Compile
echo ===================================================
dotnet build UnitConstructionVerifier.sln -c Debug
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Build completed.
echo Output: UnitConstructionVerifier\bin\Debug\net48\
pause
exit /b 0
