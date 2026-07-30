@echo off
setlocal
cd /d "%~dp0"

if not exist "%~dp0pack\Highlighter.dll" (
    echo pack\Highlighter.dll not found. Run build.bat first.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -PackDir "%~dp0pack"
set ERR=%ERRORLEVEL%
if %ERR% NEQ 0 pause
exit /b %ERR%
