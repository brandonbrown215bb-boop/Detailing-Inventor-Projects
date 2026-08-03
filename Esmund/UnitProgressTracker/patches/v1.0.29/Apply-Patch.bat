@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ========================================
echo  Unit Progress Tracker Patch v1.0.29
echo  BOM slash-segment matching fix
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
  echo Point to your extracted UnitSurfaceViewer folder.
  echo.
  pause
  exit /b 1
)

if not exist "%TARGET%\src" mkdir "%TARGET%\src"
copy /Y "%~dp0src\bom-folder-maker.js" "%TARGET%\src\bom-folder-maker.js" >nul
if errorlevel 1 (
  echo ERROR: Could not copy bom-folder-maker.js
  pause
  exit /b 1
)
copy /Y "%~dp0package.json" "%TARGET%\package.json" >nul
if errorlevel 1 (
  echo ERROR: Could not copy package.json
  pause
  exit /b 1
)

echo.
echo Patched: %TARGET%
echo   src\bom-folder-maker.js
echo   package.json (v1.0.29)
echo.
echo Restart Unit Progress Tracker. Re-import BOM if already loaded.
echo.
pause
