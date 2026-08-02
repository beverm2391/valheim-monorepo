@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-windows.ps1"
set "INSTALL_EXIT=%ERRORLEVEL%"

if not "%BENHEIM_QOL_NONINTERACTIVE%"=="1" pause
exit /b %INSTALL_EXIT%
