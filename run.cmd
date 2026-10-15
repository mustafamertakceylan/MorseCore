@echo off
setlocal
set "MORSECORE_HOME=%~dp0"
if exist "%~dp0MorseCore.exe" (
    "%~dp0MorseCore.exe" %*
    exit /b
)
if exist "%~dp0cmake-build-debug\MorseCore.exe" (
    "%~dp0cmake-build-debug\MorseCore.exe" %*
    exit /b
)
if exist "%~dp0cmake-build-release\MorseCore.exe" (
    "%~dp0cmake-build-release\MorseCore.exe" %*
    exit /b
)
echo Build first: cmake --preset release ^&^& cmake --build --preset release
exit /b 1
