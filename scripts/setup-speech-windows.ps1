# Windows PowerShell 5.1 compatible; no PATH changes or Microsoft Store aliases.
param([string]$SpeechHome,[Parameter(ValueFromRemainingArguments=$true)][string[]]$SetupArguments)
$ErrorActionPreference='Stop'
function Test-SpeechPython([string]$Candidate) {
    if(!$Candidate -or !(Test-Path -LiteralPath $Candidate -PathType Leaf) -or $Candidate -like '*\WindowsApps\*') { return $false }
    $taskProbe=[Diagnostics.ProcessStartInfo]::new()
    $taskProbe.FileName=$Candidate
    $taskProbe.Arguments='-I -S -c "import sys,struct,venv,ensurepip,sysconfig; sys.exit(0 if (3,11)<=sys.version_info[:2]<(3,14) and struct.calcsize(''P'')==8 and not sysconfig.get_config_var(''Py_GIL_DISABLED'') else 1)"'
    $taskProbe.UseShellExecute=$false; $taskProbe.CreateNoWindow=$true
    $taskProbe.RedirectStandardOutput=$true; $taskProbe.RedirectStandardError=$true
    try {
        $taskProcess=[Diagnostics.Process]::Start($taskProbe)
        try {
            if(!$taskProcess.WaitForExit(10000)) { $taskProcess.Kill(); return $false }
            return $taskProcess.ExitCode -eq 0
        } finally { $taskProcess.Dispose() }
    } catch { return $false }
}
function Find-SpeechPython {
    $taskCandidates=[Collections.Generic.List[string]]::new()
    foreach($taskMinor in @(13,12,11)) {
        foreach($taskHive in @([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryHive]::LocalMachine)) {
            $taskRegistry=[Microsoft.Win32.RegistryKey]::OpenBaseKey($taskHive,[Microsoft.Win32.RegistryView]::Registry64)
            try {
                foreach($taskTag in @("3.$taskMinor","3.$taskMinor-64")) {
                    $taskKey=$taskRegistry.OpenSubKey("Software\Python\PythonCore\$taskTag\InstallPath")
                    if($taskKey) {
                        try {
                            $taskPath=[string]$taskKey.GetValue('ExecutablePath')
                            if(!$taskPath -and $taskKey.GetValue('')) { $taskPath=Join-Path ([string]$taskKey.GetValue('')) 'python.exe' }
                            if($taskPath) { $taskCandidates.Add($taskPath) }
                        } finally { $taskKey.Dispose() }
                    }
                }
            } finally { $taskRegistry.Dispose() }
        }
    }
    $taskCandidates.Add((Join-Path $env:LOCALAPPDATA 'MorseCore/Speech/venv/Scripts/python.exe'))
    foreach($taskCommand in @(Get-Command python.exe -All -ErrorAction SilentlyContinue)) { $taskCandidates.Add($taskCommand.Source) }
    foreach($taskCandidate in $taskCandidates | Select-Object -Unique) { if(Test-SpeechPython $taskCandidate) { return $taskCandidate } }
    return $null
}
# Dot sourcing exposes only the detector for tests.
if($MyInvocation.InvocationName -eq '.') { return }
$taskPython=Find-SpeechPython
if(!$taskPython) { throw 'Python 3.11-3.13 x64 with venv/pip was not found. Run MorseCore Setup and select the Python prerequisite.' }
$taskArguments=@('-X','utf8',(Join-Path $PSScriptRoot 'setup-speech.py'))
if($SpeechHome) { $taskArguments+=@('--home',$SpeechHome) }
if($SetupArguments) { $taskArguments+=$SetupArguments }
& $taskPython @taskArguments
if($LASTEXITCODE -ne 0) { throw "Speech pack setup failed ($LASTEXITCODE)." }
