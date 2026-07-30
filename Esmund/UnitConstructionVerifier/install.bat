@echo off
setlocal enabledelayedexpansion

set "TARGET_DIR=%APPDATA%\Autodesk\Inventor 2020\Addins"

echo ===================================================
echo JCI AHU Construction Verifier - Add-In Installer
echo ===================================================
echo.
echo This will install the Construction Verifier Add-in on your machine.
echo Target directory: %TARGET_DIR%
echo.

:: Check if Autodesk Inventor is running
tasklist /FI "IMAGENAME eq Inventor.exe" 2>NUL | find /I /N "Inventor.exe" >NUL
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] Autodesk Inventor is currently running.
    echo Please save your work and close Inventor before proceeding.
    echo.
    pause
)

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
    
    echo Renaming existing active DLL to: !BACKUP_NAME!
    rename "%TARGET_DIR%\UnitConstructionVerifier.dll" "!BACKUP_NAME!" 2>NUL
)

echo Installing files...
copy /Y "UnitConstructionVerifier.dll" "%TARGET_DIR%\" >NUL
copy /Y "Newtonsoft.Json.dll" "%TARGET_DIR%\" >NUL
copy /Y "UnitConstructionVerifier.addin" "%TARGET_DIR%\" >NUL
copy /Y "materials_config.json" "%TARGET_DIR%\" >NUL
copy /Y "materials_thickness_map.json" "%TARGET_DIR%\" >NUL

if errorlevel 1 (
    echo.
    echo [ERROR] Installation failed! Please check write permissions to %TARGET_DIR%
    pause
    exit /b 1
)

echo.
echo ===================================================
echo [SUCCESS] Add-in installed successfully!
echo Restart Autodesk Inventor to load the verifier.
echo ===================================================
pause
exit /b 0
