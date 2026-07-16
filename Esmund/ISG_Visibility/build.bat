@echo off
cd /d "%~dp0"
dotnet build VisTog.csproj -c Release
if errorlevel 1 exit /b 1
if not exist pack mkdir pack
copy /y "bin\Release\net48\VisTog.dll" pack\ >nul
copy /y "bin\Release\net48\vis-tog-rules.json" pack\ >nul
copy /y "bin\Release\net48\vistog-ui-settings.json" pack\ >nul
xcopy /y /i /q "bin\Release\net48\assets" pack\assets\ >nul
echo Built pack\
