@echo off
setlocal

if not defined QUESTBOARD_ACTOR set "QUESTBOARD_ACTOR=Esmund/Cursor"

where python >nul 2>nul
if %errorlevel% equ 0 goto use_python

where py >nul 2>nul
if %errorlevel% equ 0 goto use_py

for /f "usebackq delims=" %%P in (`powershell.exe -NoProfile -Command "(Get-Command python.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source"`) do set "QUESTBOARD_PYTHON=%%P"
if defined QUESTBOARD_PYTHON goto use_discovered_python

for /f "usebackq delims=" %%P in (`powershell.exe -NoProfile -Command "(Get-Command py.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source"`) do set "QUESTBOARD_PYTHON=%%P"
if defined QUESTBOARD_PYTHON goto use_discovered_py

echo Quest Board needs Python 3, but neither python nor py was found on PATH. 1>&2
exit /b 9009

:use_python
python "%~dp0quest.py" %*
exit /b %errorlevel%

:use_py
py -3 "%~dp0quest.py" %*
exit /b %errorlevel%

:use_discovered_python
"%QUESTBOARD_PYTHON%" "%~dp0quest.py" %*
exit /b %errorlevel%

:use_discovered_py
"%QUESTBOARD_PYTHON%" -3 "%~dp0quest.py" %*
exit /b %errorlevel%
