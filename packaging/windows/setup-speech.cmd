@echo off
setlocal
python -X utf8 "%~dp0scripts\setup-speech.py" --home "%LOCALAPPDATA%\MorseCore\Speech" %*
if errorlevel 1 (
  echo Speech pack setup failed. Python 3.11-3.13 and internet are required for setup.
  pause
  exit /b 1
)
echo Offline speech pack is ready.
pause
