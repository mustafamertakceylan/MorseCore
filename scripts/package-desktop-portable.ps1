#requires -Version 7.0
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskVersion=[regex]::Match((Get-Content (Join-Path $taskRoot 'CMakeLists.txt') -Raw),'project\(MorseCore VERSION (\d+\.\d+\.\d+)').Groups[1].Value
if(!$taskVersion) { throw 'CMake project version is missing.' }
if(!$OutputDirectory) { $OutputDirectory=Join-Path $taskRoot ('cmake-build-desktop-portable/'+[Guid]::NewGuid().ToString('N')) }
$taskOutput=[System.IO.Path]::GetFullPath($OutputDirectory)
$taskPayload=Join-Path $taskOutput ('stage-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $taskPayload | Out-Null
$env:DOTNET_CLI_HOME=Join-Path $taskRoot 'cmake-build-interop/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $taskRoot 'cmake-build-interop/nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
Push-Location $taskRoot
try {
    & cmake --preset release
    if($LASTEXITCODE -ne 0) { throw 'Native configure failed.' }
    & cmake --build --preset release --target MorseCoreNative
    if($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
    Set-Location (Join-Path $taskRoot 'apps/MorseCore.Desktop')
    & dotnet publish MorseCore.Desktop/MorseCore.Desktop.csproj -c Release -f net10.0-windows10.0.26100 -r win-x64 -p:Platform=x64 -p:SelfContained=true -p:WindowsPackageType=None -p:PublishTrimmed=false "-p:NativeLibraryPath=$taskRoot/cmake-build-release/MorseCore.Native.dll" -o $taskPayload
    if($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }
    Set-Location $taskRoot
    Copy-Item -LiteralPath LICENSE,NOTICE,CHANGELOG.md,setup-speech.cmd -Destination $taskPayload
    Copy-Item -LiteralPath docs/desktop-package.md -Destination (Join-Path $taskPayload 'README.md')
    foreach($taskDirectory in @('scripts','docs','third_party')) { New-Item -ItemType Directory -Force -Path (Join-Path $taskPayload $taskDirectory) | Out-Null }
    Copy-Item -LiteralPath scripts/setup-speech.py,scripts/setup-speech-windows.ps1,scripts/speech-models.json,scripts/speech_worker.py -Destination (Join-Path $taskPayload 'scripts')
    Copy-Item -LiteralPath docs/desktop.md,docs/desktop.tr.md,docs/offline-speech.md,docs/offline-speech.tr.md,docs/native-api.md,docs/native-api.tr.md,docs/windows-setup.md -Destination (Join-Path $taskPayload 'docs')
    Copy-Item -LiteralPath third_party/speech -Recurse -Destination (Join-Path $taskPayload 'third_party')
    Copy-Item -LiteralPath third_party/installer -Recurse -Destination (Join-Path $taskPayload 'third_party')
    & python -X utf8 scripts/collect-desktop-licenses.py $taskPayload $env:NUGET_PACKAGES
    if($LASTEXITCODE -ne 0) { throw 'Runtime license collection failed.' }
    $taskExecutable=Join-Path $taskPayload 'MorseCore.Desktop.exe'
    if((Get-Item -LiteralPath $taskExecutable).VersionInfo.ProductVersion -notlike "$taskVersion*") { throw 'Published executable version differs from release version.' }
    $taskZip=Join-Path $taskOutput "MorseCore-$taskVersion-Windows-x64-Desktop.zip"
    Compress-Archive -Path (Join-Path $taskPayload '*') -DestinationPath $taskZip -CompressionLevel Optimal
    $taskHash=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText((Join-Path $taskOutput 'SHA256SUMS'), "$taskHash  $([System.IO.Path]::GetFileName($taskZip))`n", [System.Text.UTF8Encoding]::new($false))
    Write-Output "Desktop ZIP: $taskZip"
    Write-Output "Uncompressed payload: $taskPayload"
}
finally { Pop-Location }
