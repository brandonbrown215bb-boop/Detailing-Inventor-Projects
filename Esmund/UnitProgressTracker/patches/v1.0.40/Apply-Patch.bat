@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo ========================================
echo  Unit Progress Tracker Patch v1.0.40
echo  CURRENT BUILD
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
if not exist "%TARGET%\sidecar" mkdir "%TARGET%\sidecar"
copy /Y "%~dp0package.json" "%TARGET%\package.json" >nul
copy /Y "%~dp0main.js" "%TARGET%\main.js" >nul
copy /Y "%~dp0preload.js" "%TARGET%\preload.js" >nul
copy /Y "%~dp0index.html" "%TARGET%\index.html" >nul
copy /Y "%~dp0styles.css" "%TARGET%\styles.css" >nul
copy /Y "%~dp0src\app.js" "%TARGET%\src\app.js" >nul
copy /Y "%~dp0src\iam-scan.js" "%TARGET%\src\iam-scan.js" >nul
copy /Y "%~dp0src\project-data.js" "%TARGET%\src\project-data.js" >nul
copy /Y "%~dp0src\bom-folder-maker.js" "%TARGET%\src\bom-folder-maker.js" >nul
copy /Y "%~dp0src\bom-page.js" "%TARGET%\src\bom-page.js" >nul
copy /Y "%~dp0src\unit-config-parser.js" "%TARGET%\src\unit-config-parser.js" >nul
copy /Y "%~dp0sidecar\inventor-config-read.py" "%TARGET%\sidecar\inventor-config-read.py" >nul
echo.
echo Patched: %TARGET%
echo.
echo v1.0.40 CURRENT BUILD
echo   - Replace: OK = pick .iam, Cancel = cancel
echo   - Includes v1.0.34-39 (Config BOM, replace scan, py -3)
pause
