; Shared by the production installer and the isolated prerequisite test fixture.
#ifndef PythonRegistryRoot
  #define PythonRegistryRoot "Software\Python\PythonCore"
#endif
#ifndef PythonDefaultTarget
  #define PythonDefaultTarget "{localappdata}\Programs\Python\Python313"
#endif
#ifndef PythonLogPath
  #define PythonLogPath "{localappdata}\MorseCore\InstallerLogs\Python.log"
#endif

[Files]
Source: "{#PrerequisitesDir}\python-3.13.16-amd64.exe"; Flags: dontcopy noencryption

[Code]
var
  PythonPath: String;
  PrerequisitePage: TInputOptionWizardPage;

function GetBinaryType(const FileName: String; var BinaryType: Cardinal): Boolean;
  external 'GetBinaryTypeW@kernel32.dll stdcall';

function CompatiblePython(const FileName: String): Boolean;
var
  BinaryType, MS, LS: Cardinal;
  Code: Integer;
begin
  Result := False;
  if not FileExists(FileName) then Exit;
  if not GetBinaryType(FileName, BinaryType) or (BinaryType <> 6) then Exit;
  if not GetVersionNumbers(FileName, MS, LS) then Exit;
  if (MS shr 16 <> 3) or ((MS and $FFFF) < 11) or ((MS and $FFFF) > 13) then Exit;
  if Exec(FileName, '-I -S -c "import sys, struct, venv, ensurepip, sysconfig; sys.exit(0 if (3,11) <= sys.version_info[:2] < (3,14) and struct.calcsize(''P'') == 8 and not sysconfig.get_config_var(''Py_GIL_DISABLED'') else 1)"',
    '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := Code = 0;
end;

function RegisteredPython(const Root: Integer; const Tag: String): String;
var
  Key, Candidate: String;
begin
  Result := '';
  Key := '{#PythonRegistryRoot}\' + Tag + '\InstallPath';
  if not RegQueryStringValue(Root, Key, 'ExecutablePath', Candidate) then
  begin
    if not RegQueryStringValue(Root, Key, '', Candidate) then Exit;
    Candidate := AddBackslash(Candidate) + 'python.exe';
  end;
  if CompatiblePython(Candidate) then Result := Candidate;
end;

function FindSpeechPython(): String;
var
  Minor: Integer;
  Candidate, Paths: String;
  Separator: Integer;
begin
  Result := '';
  for Minor := 13 downto 11 do
  begin
    Result := RegisteredPython(HKCU64, '3.' + IntToStr(Minor));
    if Result = '' then Result := RegisteredPython(HKLM64, '3.' + IntToStr(Minor));
    if Result = '' then Result := RegisteredPython(HKCU64, '3.' + IntToStr(Minor) + '-64');
    if Result = '' then Result := RegisteredPython(HKLM64, '3.' + IntToStr(Minor) + '-64');
    if Result <> '' then Exit;
  end;
#ifndef PythonSkipUserPaths
  Candidate := ExpandConstant('{localappdata}\MorseCore\Speech\venv\Scripts\python.exe');
  if CompatiblePython(Candidate) then begin Result := Candidate; Exit; end;
  Paths := GetEnv('PATH') + ';';
  while Paths <> '' do
  begin
    Separator := Pos(';', Paths);
    Candidate := RemoveQuotes(Copy(Paths, 1, Separator - 1));
    Delete(Paths, 1, Separator);
    if (Candidate <> '') and (Pos('WindowsApps', Candidate) = 0) then
      if CompatiblePython(AddBackslash(Candidate) + 'python.exe') then
      begin Result := AddBackslash(Candidate) + 'python.exe'; Exit; end;
  end;
#endif
end;

procedure InitializePythonPrerequisite();
var
  Detail: String;
begin
  PythonPath := FindSpeechPython();
  if PythonPath <> '' then Detail := CustomMessage('PythonFound') + #13#10 + PythonPath
  else Detail := CustomMessage('PythonMissing');
  Log('Speech Python detected: ' + PythonPath);
  PrerequisitePage := CreateInputOptionPage(wpSelectDir, CustomMessage('Prerequisites'),
    CustomMessage('BundledRuntimes'), Detail + #13#10#13#10 + CustomMessage('PythonOptional'), False, False);
  PrerequisitePage.Add(CustomMessage('InstallPython'));
  PrerequisitePage.Values[0] := (PythonPath = '') and (ExpandConstant('{param:INSTALLPYTHON|0}') = '1');
  PrerequisitePage.CheckListBox.ItemEnabled[0] := PythonPath = '';
end;

function InstallPythonPrerequisite(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Installer: String;
begin
  Result := '';
  if (PythonPath <> '') or not PrerequisitePage.Values[0] then
  begin Log('Speech Python installation skipped (present or not selected).'); Exit; end;
  Log('Speech Python installation explicitly selected.');
  WizardForm.StatusLabel.Caption := CustomMessage('InstallingPython');
  ExtractTemporaryFile('python-3.13.16-amd64.exe');
  Installer := ExpandConstant('{tmp}\python-3.13.16-amd64.exe');
  if CompareText(GetSHA256OfFile(Installer), 'fb4f9f5d438b2396da0086dc70b935c530cb578e37adc6d354f7ad2037fee83b') <> 0 then
  begin Result := CustomMessage('PythonFailed'); Exit; end;
  ForceDirectories(ExtractFileDir(ExpandConstant('{#PythonLogPath}')));
  if not Exec(Installer, '/quiet /norestart InstallAllUsers=0 Include_launcher=0 Include_test=0 Include_doc=0 Include_tcltk=0 Include_pip=1 Include_dev=1 Include_exe=1 Include_lib=1 PrependPath=0 AppendPath=0 AssociateFiles=0 Shortcuts=0 TargetDir="' + ExpandConstant('{#PythonDefaultTarget}') + '" /log "' + ExpandConstant('{#PythonLogPath}') + '"',
    '', SW_HIDE, ewWaitUntilTerminated, Code) then
  begin Result := CustomMessage('PythonFailed'); Exit; end;
  Log(Format('Python installer exit code: %d', [Code]));
  if (Code <> 0) and (Code <> 3010) then begin Result := CustomMessage('PythonFailed'); Exit; end;
  NeedsRestart := Code = 3010;
  PythonPath := FindSpeechPython();
  if (PythonPath = '') and CompatiblePython(ExpandConstant('{#PythonDefaultTarget}\python.exe')) then
    PythonPath := ExpandConstant('{#PythonDefaultTarget}\python.exe');
  if PythonPath = '' then Result := CustomMessage('PythonFailed');
end;

function HasSpeechPython(): Boolean;
begin Result := PythonPath <> ''; end;
