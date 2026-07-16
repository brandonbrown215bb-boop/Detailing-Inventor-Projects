@echo off
setlocal enabledelayedexpansion

:: Target Add-Ins Directory
set "TARGET_DIR=%APPDATA%\Autodesk\Inventor 2020\Addins"

:: Source Paths (Relative to this batch file's location in the solution folder)
set "BIN_DIR=UnitConstructionVerifier\bin\Release\net48"
set "SRC_DIR=UnitConstructionVerifier"

echo ===================================================
echo JCI AHU Construction Verifier Add-In Deployment
echo ===================================================
echo Target: %TARGET_DIR%

:: Create target directory if it doesn't exist
if not exist "%TARGET_DIR%" (
    echo Creating target directory...
    mkdir "%TARGET_DIR%"
)

:: Handle Active Locks (Hot-Reload)
if exist "%TARGET_DIR%\UnitConstructionVerifier.dll" (
    :: Generate a unique timestamp for backup (YYYYMMDD_HHMMSS)
    set "TIMESTAMP=%date:~-4%%date:~4,2%%date:~7,2%_%time:~0,2%%time:~3,2%%time:~6,2%"
    set "TIMESTAMP=!TIMESTAMP: =0!"
    set "BACKUP_NAME=UnitConstructionVerifier.dll.old_!TIMESTAMP!"
    
    echo File is locked or exists. Renaming active DLL to: !BACKUP_NAME!
    rename "%TARGET_DIR%\UnitConstructionVerifier.dll" "!BACKUP_NAME!"
    if errorlevel 1 (
        echo [WARNING] Could not rename DLL. It might be locked and already renamed. Proceeding...
    )
)

:: Copy Manifest, DLLs and Config Database Files
echo Copying files...

copy /Y "%BIN_DIR%\UnitConstructionVerifier.dll" "%TARGET_DIR%\"
if errorlevel 1 goto :error

copy /Y "%BIN_DIR%\Newtonsoft.Json.dll" "%TARGET_DIR%\"
if errorlevel 1 goto :error

copy /Y "%SRC_DIR%\UnitConstructionVerifier.addin" "%TARGET_DIR%\"
if errorlevel 1 goto :error

copy /Y "%SRC_DIR%\materials_config.json" "%TARGET_DIR%\"
if errorlevel 1 goto :error

copy /Y "%SRC_DIR%\materials_thickness_map.json" "%TARGET_DIR%\"
if errorlevel 1 goto :error

echo ===================================================
echo [SUCCESS] Add-In deployed successfully!
echo Restart Autodesk Inventor to load the updated version.
echo ===================================================
pause
exit /b 0

:error
echo ===================================================
echo [ERROR] Deployment failed! Make sure the project is compiled.
echo ===================================================
pause
exit /b 1
