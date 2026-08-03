@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo ========================================
echo  Unit Progress Tracker Patch v1.0.36
echo  Replace-from-IAM diagnostics + sidecar
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
copy /Y "%~dp0index.html" "%TARGET%\index.html" >nul
copy /Y "%~dp0styles.css" "%TARGET%\styles.css" >nul
copy /Y "%~dp0src\app.js" "%TARGET%\src\app.js" >nul
copy /Y "%~dp0src\project-data.js" "%TARGET%\src\project-data.js" >nul
copy /Y "%~dp0src\bom-folder-maker.js" "%TARGET%\src\bom-folder-maker.js" >nul
copy /Y "%~dp0src\bom-page.js" "%TARGET%\src\bom-page.js" >nul
copy /Y "%~dp0src\unit-config-parser.js" "%TARGET%\src\unit-config-parser.js" >nul
echo.
echo Patched: %TARGET%
echo.
echo New in v1.0.36:
echo   - Replace from IAM... (pick .iam directly)
echo   - Detailed scan failure messages (Inventor / DOCUMENT_CONFIG_JSON)
echo   - Sidecar .json fallback when IAM lacks embedded config
echo   - v1.0.35 incremental scan + v1.0.34 BOM config (included)
pause
