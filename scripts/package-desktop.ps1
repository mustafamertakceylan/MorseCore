#requires -Version 7.0
param(
    [Parameter(Mandatory,ParameterSetName='Unsigned')][switch]$Unsigned,
    [Parameter(Mandatory,ParameterSetName='Signed')][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$CertificateThumbprint
)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$env:DOTNET_CLI_HOME=Join-Path $taskRoot 'cmake-build-interop/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $taskRoot 'cmake-build-interop/nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$taskOutput=Join-Path $taskRoot ('cmake-build-desktop-package/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
if(!$Unsigned) {
    $taskCertificate=Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
    if(!$taskCertificate.HasPrivateKey -or $taskCertificate.Subject -cne 'O=Mustafa Mert Akceylan') {
        throw 'A signing certificate with a private key and the exact package publisher is required.'
    }
}
Push-Location $taskRoot
try {
    & cmake --preset release
    if($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
    & cmake --build --preset release --target MorseCoreNative
    if($LASTEXITCODE -ne 0) { throw 'Native library build failed.' }
    $taskNative=Join-Path $taskRoot 'cmake-build-release/MorseCore.Native.dll'
    Set-Location (Join-Path $taskRoot 'apps/MorseCore.Desktop')
    $taskSigningEnabled=(!$Unsigned).ToString().ToLowerInvariant()
    $taskArguments=@('build','MorseCore.Desktop/MorseCore.Desktop.csproj','-c','Release','-f','net10.0-windows10.0.26100',
        '-p:Platform=x64','-p:RuntimeIdentifier=win-x64','-p:SelfContained=true','-p:WindowsPackageType=MSIX',
        '-p:GenerateAppxPackageOnBuild=true','-p:AppxBundle=Never','-p:UapAppxPackageBuildMode=SideloadOnly','-p:AppxSymbolPackageEnabled=false',
        "-p:AppxPackageDir=$taskOutput/","-p:NativeLibraryPath=$taskNative","-p:AppxPackageSigningEnabled=$taskSigningEnabled")
    if(!$Unsigned) { $taskArguments+="-p:PackageCertificateThumbprint=$CertificateThumbprint" }
    & dotnet @taskArguments
    if($LASTEXITCODE -ne 0) { throw 'MSIX build failed.' }
    $taskPackage=Get-ChildItem -LiteralPath $taskOutput -Recurse -Filter '*.msix' | Where-Object { $_.FullName -notmatch '[\\/]Dependencies[\\/]' } | Select-Object -First 1
    if(!$taskPackage) { throw 'The build did not produce an MSIX package.' }
    $taskDestination=Join-Path $taskOutput 'MorseCore.msix'
    Copy-Item -LiteralPath $taskPackage.FullName -Destination $taskDestination
    $taskArchive=[System.IO.Compression.ZipFile]::OpenRead($taskDestination)
    try {
        $taskReader=[System.IO.StreamReader]::new($taskArchive.GetEntry('AppxManifest.xml').Open())
        try { [xml]$taskManifest=$taskReader.ReadToEnd() } finally { $taskReader.Dispose() }
    }
    finally { $taskArchive.Dispose() }
    $taskIdentity=$taskManifest.Package.Identity
    [xml]$taskFeed=@'
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller xmlns="http://schemas.microsoft.com/appx/appinstaller/2018" Uri="https://github.com/mustafamertakceylan/MorseCore/releases/latest/download/MorseCore.appinstaller" Version="0.0.0.0">
 <MainPackage Name="" Publisher="" Version="0.0.0.0" ProcessorArchitecture="x64" Uri="https://github.com/mustafamertakceylan/MorseCore/releases/latest/download/MorseCore.msix"/>
 <UpdateSettings><OnLaunch HoursBetweenUpdateChecks="6" ShowPrompt="true" UpdateBlocksActivation="false"/><AutomaticBackgroundTask/></UpdateSettings>
</AppInstaller>
'@
    $taskFeed.AppInstaller.Version=[string]$taskIdentity.Version
    $taskFeed.AppInstaller.MainPackage.Name=[string]$taskIdentity.Name
    $taskFeed.AppInstaller.MainPackage.Publisher=[string]$taskIdentity.Publisher
    $taskFeed.AppInstaller.MainPackage.Version=[string]$taskIdentity.Version
    $taskFeed.Save((Join-Path $taskOutput 'MorseCore.appinstaller'))
    $taskSums=foreach($taskFile in @('MorseCore.msix','MorseCore.appinstaller')) {
        $taskHash=(Get-FileHash -LiteralPath (Join-Path $taskOutput $taskFile) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$taskHash  $taskFile"
    }
    $taskSums | Set-Content -LiteralPath (Join-Path $taskOutput 'SHA256SUMS') -Encoding utf8
    Write-Output "Artifacts: $taskOutput"
    if($Unsigned) { Write-Output 'Unsigned verification package only. Trusted signing is required before distribution.' }
}
finally { Pop-Location }
