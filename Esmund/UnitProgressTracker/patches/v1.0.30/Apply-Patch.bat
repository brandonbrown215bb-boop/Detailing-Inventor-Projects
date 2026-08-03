@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ========================================
echo  Unit Progress Tracker Patch v1.0.30
echo  BOM label rename: Shell -^> Inventor
echo ========================================
echo.

set "TARGET=%~1"
if "%TARGET%"=="" (
  set /p TARGET="Paste install folder path (UnitSurfaceViewer): "
)
set "TARGET=%TARGET:"=%"
if "%TARGET:~-1%"=="\" set "TARGET=%TARGET:~0,-1%"

if not exist "%TARGET%\package.json" (
  echo.
  echo ERROR: Not found: %TARGET%\package.json
  pause
  exit /b 1
)

if not exist "%TARGET%\src" mkdir "%TARGET%\src"

copy /Y "%~dp0package.json" "%TARGET%\package.json" >nul
copy /Y "%~dp0main.js" "%TARGET%\main.js" >nul
copy /Y "%~dp0index.html" "%TARGET%\index.html" >nul
copy /Y "%~dp0src\app.js" "%TARGET%\src\app.js" >nul
copy /Y "%~dp0src\bom-page.js" "%TARGET%\src\bom-page.js" >nul
copy /Y "%~dp0src\bom-list-display.js" "%TARGET%\src\bom-list-display.js" >nul

if errorlevel 1 (
  echo ERROR: Copy failed.
  pause
  exit /b 1
)

echo.
echo Patched: %TARGET%
echo   package.json (v1.0.30)
echo   main.js, index.html
echo   src\app.js, src\bom-page.js, src\bom-list-display.js
echo.
echo Restart Unit Progress Tracker.
echo.
pause
