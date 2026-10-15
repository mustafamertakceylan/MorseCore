#requires -Version 7.0
param()
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskId=[Guid]::NewGuid().ToString('N')
$taskDirectory=Join-Path $taskRoot "cmake-build-prerequisite-tests/$taskId"
$taskTarget=Join-Path $taskDirectory 'Python runtime Türkçe'
$taskRegistryRoot="Software\MorseCore\PrerequisiteQA\$taskId"
$taskRegistry=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry64)
$taskPython313=$taskRegistry.OpenSubKey('Software\Python\PythonCore\3.13')
if($taskPython313) { $taskPython313.Dispose(); $taskRegistry.Dispose(); throw 'Python 3.13 is already installed. Use an account without 3.13 to test its installation safely.' }
. (Join-Path $PSScriptRoot 'setup-speech-windows.ps1')
$taskExistingPython=Find-SpeechPython
if(!$taskExistingPython) { throw 'A compatible existing Python is needed for the present-component test.' }
$taskExistingHash=(Get-FileHash -LiteralPath $taskExistingPython).Hash
New-Item -ItemType Directory -Force -Path $taskDirectory | Out-Null
$taskCompiler=& (Join-Path $PSScriptRoot 'ensure-inno-setup.ps1')
$taskPrerequisites=& (Join-Path $PSScriptRoot 'ensure-python-installer.ps1')
& $taskCompiler '/Q' "/DQaId=$taskId" "/DQaDirectory=$taskDirectory" "/DPrerequisitesDir=$taskPrerequisites" "/DPythonRegistryRoot=$taskRegistryRoot" "/DPythonDefaultTarget=$taskTarget" '/DPythonSkipUserPaths' "/DPythonLogPath=$taskDirectory/python.log" (Join-Path $taskRoot 'tests/installer/prerequisite.iss')
if($LASTEXITCODE -ne 0) { throw 'Prerequisite fixture compilation failed.' }
$taskChecks=[Collections.Generic.List[string]]::new()
$taskFailure=$null
function Assert-Prerequisite([bool]$Condition,[string]$Description) {
    if(!$Condition) { throw $Description }; $taskChecks.Add($Description)
}
function Invoke-PrerequisiteProcess([string]$File,[string[]]$Arguments) {
    $taskProcess=Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    try {
        if(!$taskProcess.WaitForExit(300000)) { $taskProcess.Kill($true); throw "Prerequisite process timed out: $File" }
        return $taskProcess.ExitCode
    } finally { $taskProcess.Dispose() }
}
function Remove-Fixture {
    $taskUninstall=Get-ChildItem -LiteralPath (Join-Path $taskDirectory 'app') -Filter 'unins*.exe' -ErrorAction SilentlyContinue | Select-Object -Last 1
    if($taskUninstall) {
        if((Invoke-PrerequisiteProcess $taskUninstall.FullName @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')) -ne 0) { throw 'Fixture uninstall failed.' }
        $taskDeadline=[DateTime]::UtcNow.AddSeconds(10)
        while((Test-Path -LiteralPath (Join-Path $taskDirectory 'app/LICENSE')) -and [DateTime]::UtcNow -lt $taskDeadline) { Start-Sleep -Milliseconds 100 }
        if(Test-Path -LiteralPath (Join-Path $taskDirectory 'app/LICENSE')) { throw 'Fixture application files were not removed.' }
    }
}
function Install-Fixture([string]$Scenario,[bool]$Consent) {
    $taskArguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS','/LANG=turkish',('/LOG="'+(Join-Path $taskDirectory "$Scenario.log")+'"'),('/INSTALLPYTHON='+[int]$Consent))
    return Invoke-PrerequisiteProcess (Join-Path $taskDirectory 'prerequisite-test.exe') $taskArguments
}
try {
    $taskKey=$taskRegistry.CreateSubKey("$taskRegistryRoot\3.12\InstallPath")
    try { $taskKey.SetValue('ExecutablePath',$taskExistingPython) } finally { $taskKey.Dispose() }
    Assert-Prerequisite ((Install-Fixture 'present' $true) -eq 0) 'Existing compatible Python is detected and reused even when installation is requested'
    $taskLog=Get-Content -LiteralPath (Join-Path $taskDirectory 'present.log') -Raw
    Assert-Prerequisite ($taskLog.Contains("Speech Python detected: $taskExistingPython") -and $taskLog.Contains('installation skipped')) 'Setup records the detected interpreter and skips its installer'
    Assert-Prerequisite (!(Test-Path -LiteralPath $taskTarget)) 'Existing Python does not trigger an extra installation'
    Remove-Fixture
    $taskRegistry.DeleteSubKeyTree($taskRegistryRoot)
    Assert-Prerequisite ((Install-Fixture 'declined' $false) -eq 0) 'Declining missing optional Python still allows installation'
    $taskLog=Get-Content -LiteralPath (Join-Path $taskDirectory 'declined.log') -Raw
    Assert-Prerequisite ($taskLog -match 'Speech Python detected: \r?\n') 'The missing-component case actually detects no compatible interpreter'
    Assert-Prerequisite (!(Test-Path -LiteralPath $taskTarget)) 'Declining Python creates no runtime'
    Remove-Fixture
    'Blocked install target' | Set-Content -LiteralPath $taskTarget
    Assert-Prerequisite ((Install-Fixture 'failure' $true) -ne 0) 'A failing official Python installer prevents false installation success'
    Assert-Prerequisite (!(Test-Path -LiteralPath (Join-Path $taskDirectory 'app/LICENSE'))) 'Prerequisite failure stops before application files are installed'
    Remove-Item -LiteralPath $taskTarget
    Assert-Prerequisite ((Install-Fixture 'accepted' $true) -eq 0) 'Accepting missing Python installs the bundled official x64 runtime'
    $taskPython=Join-Path $taskTarget 'python.exe'
    Assert-Prerequisite (Test-SpeechPython $taskPython) 'Installed Python supports x64, venv and pip in the allowed version range'
    & $taskPython -I -m venv (Join-Path $taskDirectory 'venv-check')
    Assert-Prerequisite ($LASTEXITCODE -eq 0) 'Installed Python creates a real virtual environment'
    & (Join-Path $taskDirectory 'venv-check/Scripts/python.exe') -I -m pip --version *> (Join-Path $taskDirectory 'pip-check.log')
    Assert-Prerequisite ($LASTEXITCODE -eq 0) 'The created virtual environment has working pip'
    $taskLog=Get-Content -LiteralPath (Join-Path $taskDirectory 'accepted.log') -Raw
    Assert-Prerequisite ($taskLog.Contains('Python installer exit code: 0')) 'Setup verifies successful completion of the real Python installer'
    Assert-Prerequisite ((Get-FileHash -LiteralPath $taskExistingPython).Hash -eq $taskExistingHash) 'The original Python executable is unchanged'
}
catch { $taskFailure=$_.Exception.Message; throw }
finally {
    Remove-Fixture
    $taskKey=$taskRegistry.OpenSubKey('Software\Python\PythonCore\3.13\InstallPath')
    try { $taskOwnedPython=if($taskKey) { [string]$taskKey.GetValue('ExecutablePath') } else { $null } } finally { if($taskKey) { $taskKey.Dispose() } }
    if($taskOwnedPython -and [IO.Path]::GetFullPath($taskOwnedPython) -eq (Join-Path $taskTarget 'python.exe')) {
        $taskCode=Invoke-PrerequisiteProcess (Join-Path $taskPrerequisites 'python-3.13.16-amd64.exe') @('/quiet','/uninstall','/norestart',('/log "'+(Join-Path $taskDirectory 'python-uninstall.log')+'"'))
        if($taskCode -ne 0) { Write-Warning "QA Python removal failed: $taskCode" }
    }
    # The official uninstaller can leave PythonPath/InstalledFeatures after removing InstallPath.
    # Remove only this test's residue; never a registered or unrelated interpreter.
    $taskRemaining=$taskRegistry.OpenSubKey('Software\Python\PythonCore\3.13\PythonPath')
    try { $taskRemainingPath=if($taskRemaining) { [string]$taskRemaining.GetValue('') } else { '' } } finally { if($taskRemaining) { $taskRemaining.Dispose() } }
    $taskRegistered=$taskRegistry.OpenSubKey('Software\Python\PythonCore\3.13\InstallPath')
    $taskHasRegistration=$null -ne $taskRegistered
    if($taskRegistered) { $taskRegistered.Dispose() }
    if(!$taskHasRegistration -and $taskRemainingPath.StartsWith($taskTarget+'\',[StringComparison]::OrdinalIgnoreCase)) { $taskRegistry.DeleteSubKeyTree('Software\Python\PythonCore\3.13') }
    $taskRegistry.DeleteSubKeyTree($taskRegistryRoot,$false)
    $taskRegistry.Dispose()
    @{ Passed=$null -eq $taskFailure; Failure=$taskFailure; Checks=$taskChecks.ToArray(); Evidence=$taskDirectory; ExistingPython=$taskExistingPython } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskDirectory 'prerequisite-checks.json') -Encoding utf8NoBOM
}
Write-Output "$($taskChecks.Count) prerequisite checks passed. Evidence: $taskDirectory"
