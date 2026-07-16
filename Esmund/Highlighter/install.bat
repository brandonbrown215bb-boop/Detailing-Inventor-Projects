@echo off
cd /d "%~dp0"
if not exist pack\Highlighter.dll (
  echo Run build.bat first.
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
