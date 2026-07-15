@echo off
cd /d "%~dp0"
dotnet build Highlighter.csproj -c Release
if errorlevel 1 exit /b 1
if not exist pack mkdir pack
copy /y "bin\Release\net48\Highlighter.dll" pack\ >nul
xcopy /y /i /q "bin\Release\net48\assets" pack\assets\ >nul
echo Built pack\
