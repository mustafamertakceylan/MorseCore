@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup-speech-windows.ps1" %*
if errorlevel 1 (
  echo Speech pack setup failed. Python 3.11-3.13 and internet are required for setup.
  pause
  exit /b 1
)
echo Offline speech pack is ready.
pause
