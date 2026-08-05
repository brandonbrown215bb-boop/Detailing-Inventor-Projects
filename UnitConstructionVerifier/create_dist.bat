@echo off
setlocal enabledelayedexpansion

:: Source Paths (Relative to solution root)
set "BIN_DIR=UnitConstructionVerifier\bin\Debug\net48"
set "SRC_DIR=UnitConstructionVerifier"
set "DIST_DIR=dist"
set "ZIP_NAME=UnitConstructionVerifier_Dist.zip"

echo ===================================================
echo JCI Construction Verifier - Create Dist Package
echo ===================================================
echo.

:: Clean up previous distribution artifacts
if exist "%DIST_DIR%" (
    echo Cleaning old dist folder...
    rmdir /S /Q "%DIST_DIR%"
)
if exist "%ZIP_NAME%" (
    echo Deleting old distribution zip...
    del /F /Q "%ZIP_NAME%"
)

:: Create fresh dist directory
echo Creating dist folder...
mkdir "%DIST_DIR%"

:: Copy verifier files to dist folder
echo Copying binaries and configurations...

copy /Y "%BIN_DIR%\UnitConstructionVerifier.dll" "%DIST_DIR%\" >NUL
if errorlevel 1 goto :error

copy /Y "%BIN_DIR%\Newtonsoft.Json.dll" "%DIST_DIR%\" >NUL
if errorlevel 1 goto :error

copy /Y "%SRC_DIR%\UnitConstructionVerifier.addin" "%DIST_DIR%\" >NUL
if errorlevel 1 goto :error

copy /Y "%SRC_DIR%\materials_config.json" "%DIST_DIR%\" >NUL
if errorlevel 1 goto :error

copy /Y "%SRC_DIR%\materials_thickness_map.json" "%DIST_DIR%\" >NUL
if errorlevel 1 goto :error

:: Copy installer script to dist folder
copy /Y "install.bat" "%DIST_DIR%\" >NUL
if errorlevel 1 goto :error

:: Zip the dist folder contents using PowerShell
echo Creating ZIP archive %ZIP_NAME%...
powershell -Command "Compress-Archive -Path '%DIST_DIR%\*' -DestinationPath '%ZIP_NAME%' -Force"
if errorlevel 1 (
    echo [ERROR] Failed to compress distribution package.
    goto :error
)

:: Clean up temp dist folder
echo Cleaning up temporary files...
rmdir /S /Q "%DIST_DIR%"

echo.
echo ===================================================
echo [SUCCESS] Distribution package created successfully!
echo Archive saved to: %ZIP_NAME%
echo ===================================================
pause
exit /b 0

:error
echo.
echo ===================================================
echo [ERROR] Failed to create distribution package!
echo Please compile the project and check file paths.
echo ===================================================
if exist "%DIST_DIR%" rmdir /S /Q "%DIST_DIR%"
pause
exit /b 1
