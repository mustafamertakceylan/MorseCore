#requires -Version 7.0
param()
$ErrorActionPreference='Stop'
if(!$IsWindows) { throw 'The Python prerequisite is for Windows x64.' }
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskDirectory=Join-Path $taskRoot 'cmake-build-installer-tools/prerequisites'
$taskInstaller=Join-Path $taskDirectory 'python-3.13.16-amd64.exe'
# Official release: https://www.python.org/downloads/release/python-31316/
$taskExpected='fb4f9f5d438b2396da0086dc70b935c530cb578e37adc6d354f7ad2037fee83b'
New-Item -ItemType Directory -Force -Path $taskDirectory | Out-Null
if(!(Test-Path -LiteralPath $taskInstaller) -or (Get-FileHash -LiteralPath $taskInstaller).Hash.ToLowerInvariant() -ne $taskExpected) {
    Invoke-WebRequest -Uri 'https://www.python.org/ftp/python/3.13.16/python-3.13.16-amd64.exe' -OutFile $taskInstaller
}
if((Get-FileHash -LiteralPath $taskInstaller).Hash.ToLowerInvariant() -ne $taskExpected) { throw 'Python installer SHA256 mismatch.' }
$taskSignature=Get-AuthenticodeSignature -LiteralPath $taskInstaller
if($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'CN=Python Software Foundation(?:,|$)') { throw 'Python installer publisher verification failed.' }
Write-Output $taskDirectory
