@echo off
setlocal
cd /d "%~dp0"
title File Grid Setup

echo.
echo  File Grid - first-time setup
echo  ============================
echo.

where node >nul 2>&1
if errorlevel 1 (
  echo  Node.js is required. Install the LTS build from https://nodejs.org/
  echo.
  pause
  exit /b 1
)

echo  [1/4] Installing dependencies...
call npm install
if errorlevel 1 goto fail

echo  [2/4] Building application...
call npm run build
if errorlevel 1 goto fail

echo  [3/4] Generating shortcut icon...
call npm run icon
if errorlevel 1 goto fail

echo  [4/4] Creating desktop shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Create-DesktopShortcut.ps1" -Preview
if errorlevel 1 goto fail

echo.
echo  Setup complete. Use the "File Grid" shortcut on your desktop.
echo  To run setup again later, double-click Setup-FileGrid.bat in this folder.
echo.
pause
exit /b 0

:fail
echo.
echo  Setup failed. Check messages above or scripts\launch-error.log
echo.
pause
exit /b 1
