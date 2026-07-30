@echo off
setlocal
cd /d "%~dp0"

dotnet build "%~dp0Highlighter.csproj" -c Release
if errorlevel 1 exit /b 1

if exist "%~dp0pack" rmdir /s /q "%~dp0pack"
mkdir "%~dp0pack"

robocopy "%~dp0bin\Release\net48" "%~dp0pack" /E /XF *.config /NFL /NDL /NJH /NJS /NC /NS /NP >nul
if %ERRORLEVEL% GEQ 8 exit /b 1

echo Built: pack\
exit /b 0
