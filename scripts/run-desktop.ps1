#requires -Version 7.0
param([ValidateSet('Debug','Release')][string]$Configuration='Debug')
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$env:DOTNET_CLI_HOME=Join-Path $taskRoot 'cmake-build-interop/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $taskRoot 'cmake-build-interop/nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$taskPreset=$Configuration.ToLowerInvariant()
Push-Location $taskRoot
try {
    & cmake --preset $taskPreset
    if($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
    & cmake --build --preset $taskPreset --target MorseCoreNative
    if($LASTEXITCODE -ne 0) { throw 'Native library build failed.' }
    $taskNative=Join-Path $taskRoot "cmake-build-$taskPreset/MorseCore.Native.dll"
    Set-Location (Join-Path $taskRoot 'apps/MorseCore.Desktop')
    & dotnet build MorseCore.Desktop/MorseCore.Desktop.csproj -c $Configuration -f net10.0-windows10.0.26100 -p:Platform=x64 "-p:NativeLibraryPath=$taskNative"
    if($LASTEXITCODE -ne 0) { throw 'WinUI build failed.' }
    $taskExecutable=Join-Path $PWD "MorseCore.Desktop/bin/x64/$Configuration/net10.0-windows10.0.26100/win-x64/MorseCore.Desktop.exe"
    Start-Process -FilePath $taskExecutable -WindowStyle Normal
}
finally { Pop-Location }
