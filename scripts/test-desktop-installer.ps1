#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [switch]$SkipLaunch
)
$ErrorActionPreference='Stop'
if(!$IsWindows) { throw 'Installer verification requires Windows.' }
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskInstaller=(Resolve-Path -LiteralPath $InstallerPath).Path
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
    $taskCode=Invoke-InstallerProcess $taskInstaller ($taskBaseArguments+@('/TASKS=""',('/DIR="'+$taskInstall+'"'),('/LOG="'+(Join-Path $taskEvidence 'install.log')+'"')))
    Assert-InstallerCheck ($taskCode -eq 0) "Per-user installation succeeds (exit $taskCode)"
    foreach($taskFile in @('MorseCore.Desktop.exe','MorseCore.Native.dll','coreclr.dll','Microsoft.UI.Xaml.dll','LICENSE','NOTICE','licenses/dependencies.json','third_party/speech/ema-lightning/LICENSE','third_party/speech/antalia-mini/LICENSE','third_party/speech/antalia-mini/NOTICE','third_party/installer/Inno-Setup-LICENSE.txt','scripts/setup-speech.py','setup-speech.cmd','unins000.exe')) {
        Assert-InstallerCheck (Test-Path -LiteralPath (Join-Path $taskInstall $taskFile) -PathType Leaf) "Installed file: $taskFile"
    }
    foreach($taskFile in @('LICENSE','NOTICE','third_party/speech/antalia-mini/NOTICE','third_party/installer/Inno-Setup-LICENSE.txt')) {
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
    Assert-InstallerCheck ($taskSpeechLauncher.Contains('--home "%LOCALAPPDATA%\MorseCore\Speech"')) 'Installed speech setup uses persistent per-user model storage'
    Assert-InstallerCheck (!(Test-Path -LiteralPath (Join-Path $taskInstall 'speech-runtime'))) 'Models and virtual environments are not bundled'
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
    @{ Passed=$null -eq $taskFailure; Failure=$taskFailure; Launched=!$SkipLaunch; Checks=$taskChecks.ToArray(); Installer=$taskInstaller; Evidence=$taskEvidence } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskEvidence 'installer-checks.json') -Encoding utf8NoBOM
}
Write-Output "$($taskChecks.Count) installer checks passed. Evidence: $taskEvidence"
