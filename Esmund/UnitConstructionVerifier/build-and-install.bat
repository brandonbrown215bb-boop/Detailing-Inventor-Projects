@echo off
setlocal
cd /d "%~dp0"
call "%~dp0compile.bat"
if errorlevel 1 exit /b 1
call "%~dp0deploy.bat"
exit /b %ERRORLEVEL%
