#requires -Version 7.0
param([Parameter(Mandatory)][string]$PreviousInstallerPath)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskBaseline=(Resolve-Path -LiteralPath $PreviousInstallerPath).Path
$taskVersion=[Version](Get-Item -LiteralPath $taskBaseline).VersionInfo.ProductVersion
$taskId=[Guid]::NewGuid().ToString('N')
$taskEvidence=Join-Path $taskRoot "cmake-build-live-update-tests/$taskId"
$taskInstall=Join-Path $taskEvidence 'güncelleme denemesi'
$taskRegistryPath='Software\Microsoft\Windows\CurrentVersion\Uninstall\io.github.mustafamertakceylan.morsecore.desktop_is1'
$taskRegistry=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry64)
$taskEntry=$taskRegistry.OpenSubKey($taskRegistryPath)
if($taskEntry) { $taskEntry.Dispose(); $taskRegistry.Dispose(); throw 'An existing MorseCore installation must be preserved. Use a clean account.' }
New-Item -ItemType Directory -Force -Path $taskEvidence | Out-Null
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$taskChecks=[Collections.Generic.List[string]]::new(); $taskFailure=$null; $taskApp=$null; $taskSetup=$null
$taskPreviousHome=$env:MORSECORE_DESKTOP_HOME
$taskPreviousDiagnostics=$env:MORSECORE_DIAGNOSTICS_DIR
function Assert-Update([bool]$Condition,[string]$Description) { if(!$Condition) { throw $Description }; $taskChecks.Add($Description) }
function Invoke-UpdateProcess([string]$File,[string[]]$Arguments) {
    $taskProcess=Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    try { if(!$taskProcess.WaitForExit(120000)) { $taskProcess.Kill($true); throw 'Setup timed out.' }; return $taskProcess.ExitCode }
    finally { $taskProcess.Dispose() }
}
function Open-TestApp {
    $taskProcess=Start-Process -FilePath (Join-Path $taskInstall 'MorseCore.Desktop.exe') -WorkingDirectory $taskEvidence -WindowStyle Hidden -PassThru
    $taskDeadline=[DateTime]::UtcNow.AddSeconds(20)
    do { if($taskProcess.WaitForExit(150)) { throw 'Application exited on startup.' }; $taskProcess.Refresh() }
    while($taskProcess.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $taskDeadline)
    if($taskProcess.MainWindowHandle -eq 0) { throw 'Application window did not open.' }
    return $taskProcess
}
function Find-Wizard {
    # Inno runs a temporary child process; restrict UI discovery to that process tree.
    $taskProcessIds=[Collections.Generic.HashSet[int]]::new(); [void]$taskProcessIds.Add($taskSetup.ProcessId)
    $taskProcesses=Get-CimInstance Win32_Process
    for($taskDepth=0;$taskDepth -lt 3;$taskDepth++) {
        foreach($taskProcess in $taskProcesses) { if($taskProcessIds.Contains([int]$taskProcess.ParentProcessId)) { [void]$taskProcessIds.Add([int]$taskProcess.ProcessId) } }
    }
    $taskWindows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition)
    foreach($taskWindow in $taskWindows) { if($taskProcessIds.Contains($taskWindow.Current.ProcessId) -and $taskWindow.Current.Name -like '*MorseCore*') { return $taskWindow } }
    return $null
}
try {
    $env:MORSECORE_DESKTOP_HOME=Join-Path $taskEvidence 'preferences'; $env:MORSECORE_DIAGNOSTICS_DIR=$taskEvidence
    New-Item -ItemType Directory -Path $env:MORSECORE_DESKTOP_HOME | Out-Null
    '{"CheckUpdates":false,"Language":"tr","Theme":"dark"}' | Set-Content -LiteralPath (Join-Path $env:MORSECORE_DESKTOP_HOME 'settings.json') -Encoding utf8NoBOM
    Assert-Update ((Invoke-UpdateProcess $taskBaseline @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS','/TASKS=""',('/DIR="'+$taskInstall+'"'),('/GROUP="MorseCore Live QA '+$taskId+'"'))) -eq 0) 'Actual previous release installs in an isolated Unicode path'
    $taskApp=Open-TestApp
    Assert-Update (!$taskApp.WaitForExit(1500)) 'Previous release is running when the updater starts'
    'Keep this user file' | Set-Content -LiteralPath (Join-Path $taskInstall 'user-note.txt')
    $taskConsumer=Join-Path $taskRoot 'tests/dotnet/MorseCore.AppCore.Smoke/bin/Release/net10.0/MorseCore.AppCore.Smoke.dll'
    $taskHandoff=Join-Path $taskEvidence 'handoff.json'
    $taskUpdater=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$taskConsumer+'"'),'--launch-update',$taskVersion.ToString(3),('"'+(Join-Path $taskEvidence 'downloads')+'"'),('"'+$taskInstall+'"'),('"'+$taskHandoff+'"')) -WindowStyle Hidden -PassThru
    try { if(!$taskUpdater.WaitForExit(120000) -or $taskUpdater.ExitCode -ne 0) { throw 'Live updater failed.' } } finally { $taskUpdater.Dispose() }
    $taskSetup=Get-Content -LiteralPath $taskHandoff -Raw | ConvertFrom-Json
    Assert-Update ([Version]$taskSetup.Version -gt $taskVersion) 'GitHub latest stable release is newer than the actually installed version'
    Assert-Update (Test-Path -LiteralPath $taskSetup.Path) 'Production updater downloads, verifies and launches the actual release Setup'
    Assert-Update ($taskApp.CloseMainWindow() -and $taskApp.WaitForExit(15000)) 'Running application closes cleanly after handing off to Setup'
    $taskApp.Dispose(); $taskApp=$null
    $taskTrace=[Collections.Generic.List[string]]::new()
    $taskDeadline=[DateTime]::UtcNow.AddSeconds(90); $taskFinished=$false
    while([DateTime]::UtcNow -lt $taskDeadline) {
        $taskWizard=Find-Wizard
        if(!$taskWizard) { Start-Sleep -Milliseconds 250; continue }
        $taskControls=$taskWizard.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
        foreach($taskControl in $taskControls) {
            if($taskControl.Current.Name.Replace('&','') -eq 'I accept the agreement') {
                $taskPattern=$null
                if($taskControl.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$taskPattern)) { $taskPattern.Select() }
            }
            if($taskControl.Current.Name -eq 'Launch MorseCore') {
                $taskPattern=$null
                if($taskControl.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern,[ref]$taskPattern) -and $taskPattern.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) { $taskPattern.Toggle() }
            }
        }
        $taskButton=$null
        foreach($taskControl in $taskControls) {
            $taskName=$taskControl.Current.Name.Replace('&','')
            if($taskControl.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $taskControl.Current.IsEnabled -and $taskName -in @('Next','Next >','Install','Finish')) { $taskButton=$taskControl; break }
        }
        if($taskButton) {
            $taskName=$taskButton.Current.Name.Replace('&',''); $taskTrace.Add($taskName)
            ([System.Windows.Automation.InvokePattern]$taskButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
            if($taskName -eq 'Finish') { $taskFinished=$true; break }
        }
        Start-Sleep -Milliseconds 500
    }
    $taskTrace | Set-Content -LiteralPath (Join-Path $taskEvidence 'wizard-actions.txt')
    Assert-Update $taskFinished 'Updater-launched interactive Setup completes through the real wizard'
    $taskEntry=$taskRegistry.OpenSubKey($taskRegistryPath)
    try {
        Assert-Update ($taskEntry.GetValue('DisplayVersion') -eq $taskSetup.Version) 'Windows uninstall registration advances to the GitHub release version'
        Assert-Update ($taskEntry.GetValue('InstallLocation').TrimEnd('\') -eq $taskInstall) 'Update retains the original installation directory'
    } finally { $taskEntry.Dispose() }
    Assert-Update ((Get-Item -LiteralPath (Join-Path $taskInstall 'MorseCore.Desktop.exe')).VersionInfo.ProductVersion -like "$($taskSetup.Version)*") 'Installed executable advances to the downloaded release version'
    Assert-Update (Test-Path -LiteralPath (Join-Path $taskInstall 'user-note.txt')) 'Update preserves user-created files'
    Assert-Update ((Get-Content -LiteralPath (Join-Path $env:MORSECORE_DESKTOP_HOME 'settings.json') -Raw | ConvertFrom-Json).Theme -eq 'dark') 'Update preserves desktop preferences'
    $taskApp=Open-TestApp
    Assert-Update (!$taskApp.WaitForExit(1500)) 'Updated release opens successfully'
    Assert-Update ($taskApp.CloseMainWindow() -and $taskApp.WaitForExit(15000)) 'Updated release closes normally'
    $taskApp.Dispose(); $taskApp=$null
}
catch { $taskFailure=$_.Exception.Message; throw }
finally {
    if($taskApp -and !$taskApp.HasExited) { $taskApp.Kill($true) }
    if($taskSetup) {
        $taskRunning=Get-Process -Id $taskSetup.ProcessId -ErrorAction SilentlyContinue
        if($taskRunning) { $taskRunning.Kill($true); $taskRunning.WaitForExit(10000) | Out-Null; $taskRunning.Dispose() }
    }
    $taskUninstall=Join-Path $taskInstall 'unins000.exe'
    if(Test-Path -LiteralPath $taskUninstall) { [void](Invoke-UpdateProcess $taskUninstall @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')) }
    $taskRegistry.Dispose(); $env:MORSECORE_DESKTOP_HOME=$taskPreviousHome; $env:MORSECORE_DIAGNOSTICS_DIR=$taskPreviousDiagnostics
    @{ Passed=$null -eq $taskFailure; Failure=$taskFailure; Checks=$taskChecks.ToArray(); Release=$taskSetup; Evidence=$taskEvidence } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskEvidence 'live-update-checks.json') -Encoding utf8NoBOM
}
Write-Output "$($taskChecks.Count) live update checks passed. Evidence: $taskEvidence"
