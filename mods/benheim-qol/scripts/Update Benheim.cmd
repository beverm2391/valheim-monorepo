@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-windows.ps1"
set "UPDATE_EXIT=%ERRORLEVEL%"

if not "%BENHEIM_QOL_NONINTERACTIVE%"=="1" pause
exit /b %UPDATE_EXIT%
