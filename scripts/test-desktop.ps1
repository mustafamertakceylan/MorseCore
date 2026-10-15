#requires -Version 7.0
param([switch]$MouseInput)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$env:DOTNET_CLI_HOME=Join-Path $taskRoot 'cmake-build-interop/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $taskRoot 'cmake-build-interop/nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$taskReport=Join-Path $taskRoot ('cmake-build-desktop-tests/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $taskReport | Out-Null
Push-Location $taskRoot
try {
    & cmake --preset debug
    if($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
    & cmake --build --preset debug --target MorseCoreNative
    if($LASTEXITCODE -ne 0) { throw 'Native library build failed.' }
    Set-Location (Join-Path $taskRoot 'apps/MorseCore.Desktop')
    $taskBuildOutput=Join-Path $taskReport 'build/'
    & dotnet build MorseCore.Desktop/MorseCore.Desktop.csproj -c Debug -f net10.0-windows10.0.26100 -p:Platform=x64 --disable-build-servers -m:1 "-p:BaseOutputPath=$taskBuildOutput"
    if($LASTEXITCODE -ne 0) { throw 'WinUI build failed.' }
    $taskPreviousSmoke=$env:MORSECORE_DESKTOP_SMOKE
    $taskPreviousMouseSmoke=$env:MORSECORE_DESKTOP_MOUSE_SMOKE
    try {
        $env:MORSECORE_DESKTOP_SMOKE=$taskReport
        $env:MORSECORE_DESKTOP_MOUSE_SMOKE=if($MouseInput) { '1' } else { $null }
        $taskExecutable=Get-ChildItem -LiteralPath $taskBuildOutput -Filter MorseCore.Desktop.exe -Recurse | Select-Object -First 1
        $taskProcess=Start-Process -FilePath $taskExecutable.FullName -WindowStyle Hidden -PassThru
    }
    finally { $env:MORSECORE_DESKTOP_SMOKE=$taskPreviousSmoke; $env:MORSECORE_DESKTOP_MOUSE_SMOKE=$taskPreviousMouseSmoke }
    if(!$taskProcess.WaitForExit(60000)) {
        Stop-Process -Id $taskProcess.Id
        throw 'UI checks did not finish within 60 seconds.'
    }
    $taskResult=Get-Content -LiteralPath (Join-Path $taskReport 'ui-smoke.json') -Raw | ConvertFrom-Json
    if(!$taskResult.Passed) { throw $taskResult.Failure }
    if($MouseInput -and !$taskResult.MouseDragTested) { throw 'Mouse drag checks were requested but did not run.' }
    Write-Output ("{0} UI checks passed. Evidence: {1}" -f $taskResult.Checks.Count,$taskReport)
}
finally { Pop-Location }
