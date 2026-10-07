@echo off
setlocal
cd /d "%~dp0"
if not exist "cmake-build-debug\MorseCore.exe" (
    echo Once derleyin: cmake --preset debug ^&^& cmake --build --preset debug
    exit /b 1
)
"cmake-build-debug\MorseCore.exe"
exit /b %errorlevel%
