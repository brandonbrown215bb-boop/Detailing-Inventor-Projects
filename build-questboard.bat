@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo Building Standalone Self-Contained Quest Board App...
echo ===================================================

cd /d "%~dp0"

if exist "QuestBoard.UI\bin\Publish" rmdir /s /q "QuestBoard.UI\bin\Publish"

dotnet publish QuestBoard.UI\QuestBoard.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "QuestBoard.UI\bin\Publish"

if %ERRORLEVEL% EQU 0 (
    copy /y "QuestBoard.UI\bin\Publish\QuestBoard.exe" "%~dp0QuestBoard.exe" >nul
    if exist "%~dp0QuestBoard.pdb" del /f /q "%~dp0QuestBoard.pdb"
    echo.
    echo ===================================================
    echo SUCCESS: Standalone QuestBoard.exe created in root!
    echo Launching Quest Board...
    echo ===================================================
    start "" "%~dp0QuestBoard.exe"
) else (
    echo.
    echo [ERROR] Build failed with exit code %ERRORLEVEL%.
)

pause
