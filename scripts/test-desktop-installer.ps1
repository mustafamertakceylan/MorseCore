#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [string]$PreviousInstallerPath,
    [switch]$SkipLaunch
)
$ErrorActionPreference='Stop'
if(!$IsWindows) { throw 'Installer verification requires Windows.' }
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskInstaller=(Resolve-Path -LiteralPath $InstallerPath).Path
$taskInstallerVersion=[Version](Get-Item -LiteralPath $taskInstaller).VersionInfo.ProductVersion
if($PreviousInstallerPath) { $PreviousInstallerPath=(Resolve-Path -LiteralPath $PreviousInstallerPath).Path }
$taskId=[Guid]::NewGuid().ToString('N')
$taskEvidence=Join-Path $taskRoot "cmake-build-installer-tests/$taskId"
$taskInstall=Join-Path $taskEvidence 'kurulum denemesi Türkçe'
$taskGroup="MorseCore QA $taskId"
$taskShortcutDirectory=Join-Path ([Environment]::GetFolderPath('Programs')) $taskGroup
$taskRegistryPath='Software\Microsoft\Windows\CurrentVersion\Uninstall\io.github.mustafamertakceylan.morsecore.desktop_is1'
$taskRegistry=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry64)
$taskExisting=$taskRegistry.OpenSubKey($taskRegistryPath)
if($taskExisting) { $taskExisting.Dispose(); $taskRegistry.Dispose(); throw 'MorseCore is already installed. Run this test on a clean account to preserve its installation.' }
New-Item -ItemType Directory -Force -Path $taskEvidence | Out-Null
$taskChecks=[System.Collections.Generic.List[string]]::new()
$taskApplication=$null
$taskFailure=$null
function Assert-InstallerCheck([bool]$Condition,[string]$Description) {
    if(!$Condition) { throw $Description }
    $taskChecks.Add($Description)
}
function Invoke-InstallerProcess([string]$File,[string[]]$Arguments) {
    $taskProcess=Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if(!$taskProcess.WaitForExit(60000)) { $taskProcess.Kill($true); throw "Installer process timed out: $File" }
    return $taskProcess.ExitCode
}
try {
    $taskBaseArguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS','/LANG=turkish',('/GROUP="'+$taskGroup+'"'))
    if($PreviousInstallerPath) {
        $taskPreviousVersion=[Version](Get-Item -LiteralPath $PreviousInstallerPath).VersionInfo.ProductVersion
        Assert-InstallerCheck ($taskPreviousVersion -lt $taskInstallerVersion) 'Upgrade baseline is an older application release'
        $taskCode=Invoke-InstallerProcess $PreviousInstallerPath ($taskBaseArguments+@('/TASKS=""',('/DIR="'+$taskInstall+'"'),('/LOG="'+(Join-Path $taskEvidence 'previous-install.log')+'"')))
        Assert-InstallerCheck ($taskCode -eq 0) "Previous release $taskPreviousVersion installs successfully"
        $taskPreviousExecutableVersion=(Get-Item -LiteralPath (Join-Path $taskInstall 'MorseCore.Desktop.exe')).VersionInfo.ProductVersion
        Assert-InstallerCheck ($taskPreviousExecutableVersion -like "$taskPreviousVersion*") 'Upgrade starts with the actual older desktop executable'
        'Existing user file from the previous release.' | Set-Content -LiteralPath (Join-Path $taskInstall 'upgrade-user-note.txt')
    }
    $taskCode=Invoke-InstallerProcess $taskInstaller ($taskBaseArguments+@('/TASKS=""',('/DIR="'+$taskInstall+'"'),('/LOG="'+(Join-Path $taskEvidence 'install.log')+'"')))
    Assert-InstallerCheck ($taskCode -eq 0) "Per-user installation succeeds (exit $taskCode)"
    $taskInstalledExecutableVersion=(Get-Item -LiteralPath (Join-Path $taskInstall 'MorseCore.Desktop.exe')).VersionInfo.ProductVersion
    Assert-InstallerCheck ($taskInstalledExecutableVersion -like "$taskInstallerVersion*") 'Installed executable matches the new Setup version'
    if($PreviousInstallerPath) {
        Assert-InstallerCheck (Test-Path -LiteralPath (Join-Path $taskInstall 'upgrade-user-note.txt')) 'Upgrade preserves files created before the new release'
    }
    foreach($taskFile in @('MorseCore.Desktop.exe','MorseCore.Native.dll','coreclr.dll','Microsoft.UI.Xaml.dll','LICENSE','NOTICE','licenses/dependencies.json','third_party/speech/ema-lightning/LICENSE','third_party/speech/antalia-mini/LICENSE','third_party/speech/antalia-mini/NOTICE','third_party/installer/Inno-Setup-LICENSE.txt','third_party/installer/Python-LICENSE.txt','scripts/setup-speech.py','scripts/setup-speech-windows.ps1','setup-speech.cmd','unins000.exe')) {
        Assert-InstallerCheck (Test-Path -LiteralPath (Join-Path $taskInstall $taskFile) -PathType Leaf) "Installed file: $taskFile"
    }
    foreach($taskFile in @('LICENSE','NOTICE')) {
        $taskExpected=(Get-Content -LiteralPath (Join-Path $taskRoot $taskFile) -Raw).Replace("`r`n","`n")
        $taskActual=(Get-Content -LiteralPath (Join-Path $taskInstall $taskFile) -Raw).Replace("`r`n","`n")
        Assert-InstallerCheck ($taskExpected -ceq $taskActual) "Project attribution text is preserved: $taskFile"
    }
    foreach($taskFile in @('third_party/speech/antalia-mini/NOTICE','third_party/installer/Inno-Setup-LICENSE.txt','third_party/installer/Python-LICENSE.txt')) {
        Assert-InstallerCheck ((Get-FileHash -LiteralPath (Join-Path $taskInstall $taskFile)).Hash -eq (Get-FileHash -LiteralPath (Join-Path $taskRoot $taskFile)).Hash) "Original attribution is preserved: $taskFile"
    }
    $taskEntry=$taskRegistry.OpenSubKey($taskRegistryPath)
    try {
        $taskVersion=[string]$taskEntry.GetValue('DisplayVersion')
        Assert-InstallerCheck ($taskEntry.GetValue('InstallLocation').TrimEnd('\') -eq $taskInstall) 'Per-user uninstall entry points to the Unicode/spaced installation path'
        Assert-InstallerCheck ($taskEntry.GetValue('DisplayName') -eq 'MorseCore') 'Windows Apps lists MorseCore'
    }
    finally { $taskEntry.Dispose() }
    $taskShortcut=Join-Path $taskShortcutDirectory 'MorseCore.lnk'
    Assert-InstallerCheck (Test-Path -LiteralPath $taskShortcut) 'Start menu application shortcut exists'
    $taskShell=New-Object -ComObject WScript.Shell
    $taskLink=$taskShell.CreateShortcut($taskShortcut)
    Assert-InstallerCheck ($taskLink.TargetPath -eq (Join-Path $taskInstall 'MorseCore.Desktop.exe')) 'Start menu shortcut opens the installed executable'
    Assert-InstallerCheck ($taskLink.WorkingDirectory -eq $taskInstall) 'Shortcut working directory matches the installation'
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskLink)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskShell)
    $taskSpeechLauncher=Get-Content -LiteralPath (Join-Path $taskInstall 'setup-speech.cmd') -Raw
    Assert-InstallerCheck ($taskSpeechLauncher.Contains('-SpeechHome "%LOCALAPPDATA%\MorseCore\Speech"')) 'Installed speech setup uses persistent per-user model storage'
    Assert-InstallerCheck (!(Test-Path -LiteralPath (Join-Path $taskInstall 'speech-runtime'))) 'Models and virtual environments are not bundled'
    $taskInstallLog=Get-Content -LiteralPath (Join-Path $taskEvidence 'install.log') -Raw
    Assert-InstallerCheck ($taskInstallLog.Contains('Speech Python detected:')) 'Setup checks for compatible Python before installation'
    Assert-InstallerCheck ($taskInstallLog.Contains('Speech Python installation skipped')) 'Silent installation never installs optional Python without explicit consent'
    & python -X utf8 (Join-Path $taskRoot 'tests/native_abi_tests.py') (Join-Path $taskInstall 'MorseCore.Native.dll') *> (Join-Path $taskEvidence 'native-abi.log')
    Assert-InstallerCheck ($LASTEXITCODE -eq 0) 'Installed native DLL passes the foreign-ABI regression suite'
    & python -X utf8 (Join-Path $taskRoot 'tests/profile_tests.py') (Join-Path $taskInstall 'MorseCore.Native.dll') *> (Join-Path $taskEvidence 'profiles.log')
    Assert-InstallerCheck ($LASTEXITCODE -eq 0) 'Installed native DLL passes Russian/Arabic profile checks'
    if(!$SkipLaunch) {
        $taskPreferences=Join-Path $taskEvidence 'preferences'
        New-Item -ItemType Directory -Path $taskPreferences | Out-Null
        '{"CheckUpdates":false,"Language":"tr"}' | Set-Content -LiteralPath (Join-Path $taskPreferences 'settings.json') -Encoding utf8NoBOM
        $taskPreviousHome=$env:MORSECORE_DESKTOP_HOME
        $taskPreviousDiagnostics=$env:MORSECORE_DIAGNOSTICS_DIR
        try {
            $env:MORSECORE_DESKTOP_HOME=$taskPreferences
            $env:MORSECORE_DIAGNOSTICS_DIR=$taskEvidence
            $taskApplication=Start-Process -FilePath (Join-Path $taskInstall 'MorseCore.Desktop.exe') -WorkingDirectory $taskEvidence -WindowStyle Hidden -PassThru
        }
        finally { $env:MORSECORE_DESKTOP_HOME=$taskPreviousHome; $env:MORSECORE_DIAGNOSTICS_DIR=$taskPreviousDiagnostics }
        $taskLaunchDeadline=[DateTime]::UtcNow.AddSeconds(20)
        do {
            if($taskApplication.WaitForExit(250)) { throw "Installed desktop exited during launch: $($taskApplication.ExitCode)" }
            $taskApplication.Refresh()
        } while($taskApplication.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $taskLaunchDeadline)
        Assert-InstallerCheck ($taskApplication.MainWindowHandle -ne 0) 'Installed release opens its window from a different working directory'
        Assert-InstallerCheck (!$taskApplication.WaitForExit(2500) -and !(Test-Path -LiteralPath (Join-Path $taskEvidence 'crash.txt'))) 'Installed release remains running without a startup crash'
        Assert-InstallerCheck ($taskApplication.CloseMainWindow()) 'Installed application accepts a normal window close'
        Assert-InstallerCheck ($taskApplication.WaitForExit(15000) -and $taskApplication.ExitCode -eq 0) 'Installed application closes cleanly'
        $taskApplication=$null
    }
    $taskUserFile=Join-Path $taskInstall 'user-note.txt'
    'User files must survive reinstall and uninstall.' | Set-Content -LiteralPath $taskUserFile
    $taskCode=Invoke-InstallerProcess $taskInstaller ($taskBaseArguments+@('/TASKS=""',('/LOG="'+(Join-Path $taskEvidence 'reinstall.log')+'"')))
    Assert-InstallerCheck ($taskCode -eq 0) 'Reinstall uses the existing application location'
    Assert-InstallerCheck (Test-Path -LiteralPath $taskUserFile) 'Reinstall preserves files created by the user'
    try {
        $taskParsedVersion=[Version]$taskVersion
        $taskNewerVersions=@(
            ('{0}.0.0' -f ($taskParsedVersion.Major+1)),
            ('{0}.{1}.0' -f $taskParsedVersion.Major,($taskParsedVersion.Minor+1)),
            ('{0}.{1}.{2}' -f $taskParsedVersion.Major,$taskParsedVersion.Minor,($taskParsedVersion.Build+1)),
            '65535.0.0'
        )
        foreach($taskNewerVersion in $taskNewerVersions) {
            $taskEntry=$taskRegistry.OpenSubKey($taskRegistryPath,$true)
            try { $taskEntry.SetValue('DisplayVersion',$taskNewerVersion) } finally { $taskEntry.Dispose() }
            $taskCode=Invoke-InstallerProcess $taskInstaller ($taskBaseArguments+@('/NOCANCEL',('/LOG="'+(Join-Path $taskEvidence "downgrade-$taskNewerVersion.log")+'"')))
            Assert-InstallerCheck ($taskCode -eq 1) "Older installer refuses registered version $taskNewerVersion"
        }
    }
    finally {
        $taskEntry=$taskRegistry.OpenSubKey($taskRegistryPath,$true)
        try { $taskEntry.SetValue('DisplayVersion',$taskVersion) } finally { $taskEntry.Dispose() }
    }
    $taskCode=Invoke-InstallerProcess (Join-Path $taskInstall 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $taskEvidence 'uninstall.log')+'"'))
    Assert-InstallerCheck ($taskCode -eq 0) 'Uninstall succeeds'
    $taskEntry=$taskRegistry.OpenSubKey($taskRegistryPath)
    try { Assert-InstallerCheck ($null -eq $taskEntry) 'Uninstall removes the Windows Apps entry' } finally { if($taskEntry) { $taskEntry.Dispose() } }
    Assert-InstallerCheck (!(Test-Path -LiteralPath (Join-Path $taskInstall 'MorseCore.Desktop.exe'))) 'Uninstall removes installed application binaries'
    Assert-InstallerCheck (!(Test-Path -LiteralPath $taskShortcut)) 'Uninstall removes the Start menu shortcut'
    Assert-InstallerCheck (Test-Path -LiteralPath $taskUserFile) 'Uninstall preserves user-created files'
    if($PreviousInstallerPath) {
        Assert-InstallerCheck (Test-Path -LiteralPath (Join-Path $taskInstall 'upgrade-user-note.txt')) 'Uninstall after upgrade preserves files from the previous release'
    }
}
catch { $taskFailure=$_.Exception.Message; throw }
finally {
    if($taskApplication -and !$taskApplication.HasExited) { Stop-Process -Id $taskApplication.Id }
    $taskRemainingEntry=$taskRegistry.OpenSubKey($taskRegistryPath)
    $taskNeedsCleanup=$null -ne $taskRemainingEntry
    if($taskRemainingEntry) { $taskRemainingEntry.Dispose() }
    if($taskNeedsCleanup -and (Test-Path -LiteralPath (Join-Path $taskInstall 'unins000.exe'))) {
        $taskCleanupCode=Invoke-InstallerProcess (Join-Path $taskInstall 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
        if($taskCleanupCode -ne 0) { Write-Warning "Test installation cleanup failed: $taskCleanupCode" }
    }
    $taskRegistry.Dispose()
    @{ Passed=$null -eq $taskFailure; Failure=$taskFailure; Launched=!$SkipLaunch; Checks=$taskChecks.ToArray(); Installer=$taskInstaller; PreviousInstaller=$PreviousInstallerPath; Evidence=$taskEvidence } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskEvidence 'installer-checks.json') -Encoding utf8NoBOM
}
Write-Output "$($taskChecks.Count) installer checks passed. Evidence: $taskEvidence"
