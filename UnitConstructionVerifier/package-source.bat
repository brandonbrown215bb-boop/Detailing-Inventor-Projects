@echo off
setlocal
cd /d "%~dp0"

set "MOBILE_DIR=C:\Users\esmun\Documents\Cursor\CursorRemote\mobile"
set "STAGING=%~dp0_package_staging"
set "ZIP_NAME=UnitConstructionVerifier_Source.zip"
set "ZIP_PATH=%MOBILE_DIR%\%ZIP_NAME%"

echo ===================================================
echo Unit Construction Verifier - Source Package
echo ===================================================

if exist "%STAGING%" rmdir /S /Q "%STAGING%"
mkdir "%STAGING%"

echo Staging source files...
xcopy /E /I /Y "%~dp0UnitConstructionVerifier" "%STAGING%\UnitConstructionVerifier\" /EXCLUDE:%~dp0_package_exclude.txt >NUL 2>&1
if errorlevel 1 (
    echo [WARNING] xcopy exclude list missing or partial copy; using robocopy fallback...
    robocopy "%~dp0UnitConstructionVerifier" "%STAGING%\UnitConstructionVerifier" /E /XD bin obj .vs /NFL /NDL /NJH /NJS /NC /NS >NUL
)

xcopy /E /I /Y "%~dp0UnitConstructionVerifier.Tests" "%STAGING%\UnitConstructionVerifier.Tests\" >NUL 2>&1
robocopy "%~dp0UnitConstructionVerifier.Tests" "%STAGING%\UnitConstructionVerifier.Tests" /E /XD bin obj .vs /NFL /NDL /NJH /NJS /NC /NS >NUL

copy /Y "%~dp0UnitConstructionVerifier.sln" "%STAGING%\" >NUL
copy /Y "%~dp0compile.bat" "%STAGING%\" >NUL
copy /Y "%~dp0build-and-install.bat" "%STAGING%\" >NUL
copy /Y "%~dp0deploy.bat" "%STAGING%\" >NUL
copy /Y "%~dp0install.bat" "%STAGING%\" >NUL
copy /Y "%~dp0create_dist.bat" "%STAGING%\" >NUL
copy /Y "%~dp0STABLE_BASE.md" "%STAGING%\" >NUL

if not exist "%MOBILE_DIR%" mkdir "%MOBILE_DIR%"
if exist "%ZIP_PATH%" del /F /Q "%ZIP_PATH%"

echo Creating %ZIP_PATH% ...
powershell -NoProfile -Command "Compress-Archive -Path '%STAGING%\*' -DestinationPath '%ZIP_PATH%' -Force"
if errorlevel 1 (
    echo [ERROR] Failed to create zip.
    rmdir /S /Q "%STAGING%"
    pause
    exit /b 1
)

rmdir /S /Q "%STAGING%"

echo.
echo [SUCCESS] Source package created:
echo   %ZIP_PATH%
echo.
echo On work laptop: extract, run compile.bat, then deploy.bat
pause
exit /b 0
