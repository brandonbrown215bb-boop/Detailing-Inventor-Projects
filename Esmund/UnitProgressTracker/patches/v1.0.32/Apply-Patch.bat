@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo ========================================
echo  Unit Progress Tracker Patch v1.0.32
echo  Shared parts across skids
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
copy /Y "%~dp0main.js" "%TARGET%\main.js" >nul
copy /Y "%~dp0preload.js" "%TARGET%\preload.js" >nul
copy /Y "%~dp0styles.css" "%TARGET%\styles.css" >nul
copy /Y "%~dp0src\bom-folder-maker.js" "%TARGET%\src\bom-folder-maker.js" >nul
copy /Y "%~dp0src\bom-page.js" "%TARGET%\src\bom-page.js" >nul
copy /Y "%~dp0src\app.js" "%TARGET%\src\app.js" >nul
echo.
echo Patched: %TARGET%
echo Re-import BOM to refresh export list.
pause
