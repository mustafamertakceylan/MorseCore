#requires -Version 7.0
param()
$ErrorActionPreference='Stop'
if(!$IsWindows) { throw 'Inno Setup requires Windows.' }
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskTools=Join-Path $taskRoot 'cmake-build-installer-tools'
$taskCompilerDirectory=Join-Path $taskTools 'inno-7.1.0'
$taskCompiler=Join-Path $taskCompilerDirectory 'ISCC.exe'
if(!(Test-Path -LiteralPath $taskCompiler -PathType Leaf)) {
    New-Item -ItemType Directory -Force -Path $taskTools | Out-Null
    $taskDownload=Join-Path $taskTools 'innosetup-7.1.0-x64.exe'
    $taskExpected='0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f'
    if(!(Test-Path -LiteralPath $taskDownload) -or (Get-FileHash -LiteralPath $taskDownload -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskExpected) {
        Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $taskDownload
    }
    if((Get-FileHash -LiteralPath $taskDownload -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskExpected) { throw 'Inno Setup SHA256 mismatch.' }
    $taskSignature=Get-AuthenticodeSignature -LiteralPath $taskDownload
    if($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.') { throw 'Inno Setup publisher verification failed.' }
    $taskProcess=Start-Process -FilePath $taskDownload -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/TASKS=""',('/DIR="'+$taskCompilerDirectory+'"')) -WindowStyle Hidden -PassThru
    if(!$taskProcess.WaitForExit(60000)) { throw 'Inno Setup compiler installation timed out.' }
    if($taskProcess.ExitCode -ne 0) { throw "Inno Setup compiler installation failed: $($taskProcess.ExitCode)" }
}
if(!(Test-Path -LiteralPath $taskCompiler -PathType Leaf)) { throw 'Inno Setup compiler is missing.' }
Write-Output $taskCompiler
