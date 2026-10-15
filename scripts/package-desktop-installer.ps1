#requires -Version 7.0
param(
    [string]$PayloadDirectory,
    [string]$OutputDirectory,
    [string]$IsccPath
)
$ErrorActionPreference='Stop'
if(!$IsWindows) { throw 'The desktop installer must be built on Windows.' }
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskVersion=[regex]::Match((Get-Content (Join-Path $taskRoot 'CMakeLists.txt') -Raw),'project\(MorseCore VERSION (\d+\.\d+\.\d+)').Groups[1].Value
if(!$taskVersion) { throw 'CMake project version is missing.' }
if(!$OutputDirectory) { $OutputDirectory=Join-Path $taskRoot ('cmake-build-desktop-installer/'+[Guid]::NewGuid().ToString('N')) }
$taskOutput=[System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
if(!$PayloadDirectory) {
    & (Join-Path $PSScriptRoot 'package-desktop-portable.ps1') -OutputDirectory $taskOutput
    $taskStages=@(Get-ChildItem -LiteralPath $taskOutput -Directory -Filter 'stage-*')
    if($taskStages.Count -ne 1) { throw 'Exactly one freshly published desktop payload is required; use a new output folder.' }
    $PayloadDirectory=$taskStages[0].FullName
}
$taskPayload=(Resolve-Path -LiteralPath $PayloadDirectory).Path
foreach($taskRequired in @('MorseCore.Desktop.exe','MorseCore.Native.dll','icon.ico','LICENSE','NOTICE','licenses/dependencies.json','scripts/setup-speech.py','scripts/speech-models.json','scripts/speech_worker.py','third_party/speech/antalia-mini/NOTICE','third_party/installer/Inno-Setup-LICENSE.txt')) {
    if(!(Test-Path -LiteralPath (Join-Path $taskPayload $taskRequired) -PathType Leaf)) { throw "Desktop payload is missing $taskRequired" }
}
if(Test-Path -LiteralPath (Join-Path $taskPayload 'speech-runtime')) { throw 'Model weights and local Python environments must not be included in the application installer.' }
if((Get-Item -LiteralPath (Join-Path $taskPayload 'MorseCore.Desktop.exe')).VersionInfo.ProductVersion -notmatch ('^'+[regex]::Escape($taskVersion)+'(?:\+|$)')) { throw 'Published executable version differs from the CMake project version.' }
if(!$IsccPath) { $IsccPath=& (Join-Path $PSScriptRoot 'ensure-inno-setup.ps1') }
if(!(Test-Path -LiteralPath $IsccPath -PathType Leaf)) { throw 'Inno Setup compiler is missing.' }
& $IsccPath '/Qp' "/DPayloadDir=$taskPayload" "/DPackageVersion=$taskVersion" "/DOutputDir=$taskOutput" (Join-Path $taskRoot 'packaging/windows/MorseCore.iss')
if($LASTEXITCODE -ne 0) { throw 'Desktop installer compilation failed.' }
$taskSetup=Join-Path $taskOutput "MorseCore-$taskVersion-Windows-x64-Setup.exe"
if(!(Test-Path -LiteralPath $taskSetup -PathType Leaf)) { throw 'Desktop installer was not produced.' }
$taskSums=foreach($taskFile in Get-ChildItem -LiteralPath $taskOutput -File | Where-Object { $_.Extension -in @('.zip','.exe') } | Sort-Object Name) {
    $taskHash=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$taskHash  $($taskFile.Name)"
}
[System.IO.File]::WriteAllText((Join-Path $taskOutput 'SHA256SUMS'), ($taskSums -join "`n")+"`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Desktop Setup: $taskSetup"
Write-Output 'Unsigned per-user installer. Optional speech models are installed separately.'
