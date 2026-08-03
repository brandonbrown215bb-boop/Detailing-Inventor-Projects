@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ========================================
echo  Unit Progress Tracker Patch v1.0.31
echo  Disk path: Shell -^> Inventor
echo ========================================
echo.

set "TARGET=%~1"
if "%TARGET%"=="" set /p TARGET="Paste install folder path (UnitSurfaceViewer): "
set "TARGET=%TARGET:"=%"
if "%TARGET:~-1%"=="\" set "TARGET=%TARGET:~0,-1%"

if not exist "%TARGET%\package.json" (
  echo ERROR: Not found: %TARGET%\package.json
  pause
  exit /b 1
)

if not exist "%TARGET%\src" mkdir "%TARGET%\src"
copy /Y "%~dp0package.json" "%TARGET%\package.json" >nul
copy /Y "%~dp0src\bom-folder-maker.js" "%TARGET%\src\bom-folder-maker.js" >nul

echo.
echo Patched: %TARGET%
echo   src\bom-folder-maker.js  (Inventor/Skid… paths)
echo   package.json (v1.0.31)
echo.
echo Re-import BOM and create folders again for new paths.
pause
