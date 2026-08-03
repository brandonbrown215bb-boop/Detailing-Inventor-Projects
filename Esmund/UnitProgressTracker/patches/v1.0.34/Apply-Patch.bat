@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo ========================================
echo  Unit Progress Tracker Patch v1.0.34
echo  Unit Config.xml import for BOM segments
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
copy /Y "%~dp0src\bom-folder-maker.js" "%TARGET%\src\bom-folder-maker.js" >nul
copy /Y "%~dp0src\bom-page.js" "%TARGET%\src\bom-page.js" >nul
copy /Y "%~dp0src\unit-config-parser.js" "%TARGET%\src\unit-config-parser.js" >nul
copy /Y "%~dp0src\app.js" "%TARGET%\src\app.js" >nul
echo.
echo Patched: %TARGET%
echo 1. Import BOM as usual
echo 2. Import Config.xml for the unit (BOM tab)
echo 3. Re-check export list ? segment folders use Config shipping skids
pause
