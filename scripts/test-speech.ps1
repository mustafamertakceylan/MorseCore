#requires -Version 7.0
param()
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskSpeech=Join-Path $taskRoot 'speech-runtime'
if(!(Test-Path -LiteralPath (Join-Path $taskSpeech 'pack-ready.json'))) { throw 'Run setup-speech.cmd first.' }
$env:DOTNET_CLI_HOME=Join-Path $taskRoot 'cmake-build-interop/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $taskRoot 'cmake-build-interop/nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$taskPreviousSpeech=$env:MORSECORE_SPEECH_HOME
Push-Location $taskRoot
try {
    & cmake --preset release
    if($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
    & cmake --build --preset release --target MorseCoreNative
    if($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
    $env:MORSECORE_SPEECH_HOME=$taskSpeech
    & dotnet run --project tests/dotnet/MorseCore.AppCore.Smoke/MorseCore.AppCore.Smoke.csproj -- (Join-Path $taskRoot 'cmake-build-release/MorseCore.Native.dll')
    if($LASTEXITCODE -ne 0) { throw 'Offline speech tests failed.' }
}
finally { $env:MORSECORE_SPEECH_HOME=$taskPreviousSpeech; Pop-Location }
