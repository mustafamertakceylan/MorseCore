; Build through scripts/package-desktop-installer.ps1.
#if VER < EncodeVer(7, 1, 0)
  #error Inno Setup 7.1 or later is required.
#endif
#ifndef PayloadDir
  #error PayloadDir is required.
#endif
#ifndef PackageVersion
  #error PackageVersion is required.
#endif
#ifndef OutputDir
  #error OutputDir is required.
#endif
#ifndef PrerequisitesDir
  #error PrerequisitesDir is required.
#endif
#define InstallerId "io.github.mustafamertakceylan.morsecore.desktop"

[Setup]
AppId={#InstallerId}
AppName=MorseCore
AppVersion={#PackageVersion}
AppPublisher=Mustafa Mert Akceylan
AppPublisherURL=https://github.com/mustafamertakceylan/MorseCore
AppSupportURL=https://github.com/mustafamertakceylan/MorseCore/issues
AppUpdatesURL=https://github.com/mustafamertakceylan/MorseCore/releases/latest
AppCopyright=Copyright (C) 2026 Mustafa Mert Akceylan
DefaultDirName={localappdata}\Programs\MorseCore
DefaultGroupName=MorseCore
DisableProgramGroupPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=MorseCore-{#PackageVersion}-Windows-x64-Setup
Compression=lzma2
SolidCompression=yes
SetupIconFile={#PayloadDir}\icon.ico
UninstallDisplayIcon={app}\MorseCore.Desktop.exe
LicenseFile={#PayloadDir}\LICENSE
WizardStyle=modern dynamic
WizardBackColor=#FAF8F2
WizardBackColorDynamicDark=#5E6A42
WizardSmallImageFile=..\..\apps\MorseCore.Desktop\MorseCore.Desktop\Assets\Brand\morsecore.png
DisableWelcomePage=no
DisableReadyPage=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=MorseCore
VersionInfoVersion={#PackageVersion}.0
VersionInfoDescription=MorseCore Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[CustomMessages]
english.SpeechSetup=Set up offline voices
turkish.SpeechSetup=Çevrimdışı sesleri kur
german.SpeechSetup=Offline-Stimmen einrichten
russian.SpeechSetup=Установить офлайн-голоса
arabic.SpeechSetup=إعداد الأصوات دون اتصال
english.NewerVersion=A newer MorseCore version is already installed. Setup will not replace it.
turkish.NewerVersion=Daha yeni bir MorseCore sürümü kurulu. Kurulum bu sürümün üzerine yazmayacak.
german.NewerVersion=Eine neuere MorseCore-Version ist bereits installiert. Sie wird nicht ersetzt.
russian.NewerVersion=Уже установлена более новая версия MorseCore. Она не будет заменена.
arabic.NewerVersion=تم تثبيت إصدار أحدث من MorseCore بالفعل. لن يتم استبداله.
#include "PrerequisiteMessages.iss"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "setup-speech.cmd,speech-runtime\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "setup-speech.cmd"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\MorseCore"; Filename: "{app}\MorseCore.Desktop.exe"; WorkingDir: "{app}"; AppUserModelID: "{#InstallerId}"
Name: "{group}\{cm:SpeechSetup}"; Filename: "{app}\setup-speech.cmd"; WorkingDir: "{app}"; IconFilename: "{app}\MorseCore.Desktop.exe"
Name: "{autodesktop}\MorseCore"; Filename: "{app}\MorseCore.Desktop.exe"; WorkingDir: "{app}"; AppUserModelID: "{#InstallerId}"; Tasks: desktopicon

[Run]
Filename: "{app}\MorseCore.Desktop.exe"; Description: "{cm:LaunchProgram,MorseCore}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\setup-speech.cmd"; Description: "{cm:DownloadVoices}"; Flags: shellexec postinstall skipifsilent unchecked; Check: HasSpeechPython

#include "PythonPrerequisite.iss"

[Code]
procedure InitializeWizard();
begin InitializePythonPrerequisite(); end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin Result := InstallPythonPrerequisite(NeedsRestart); end;

function CompareReleaseVersions(const A, B: Int64): Integer;
begin
  { Packed versions contain unsigned words; major versions >= 32768 set the sign bit. }
  if (A < 0) and (B >= 0) then
    Result := 1
  else if (A >= 0) and (B < 0) then
    Result := -1
  else
    Result := ComparePackedVersion(A, B);
end;

function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
  InstalledPacked, PackagePacked: Int64;
begin
  Result := True;
  if RegQueryStringValue(HKCU64,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#InstallerId}_is1',
    'DisplayVersion', InstalledVersion) then
  begin
    Log('Existing MorseCore version: ' + InstalledVersion);
    if StrToVersion(InstalledVersion, InstalledPacked) and
       StrToVersion('{#PackageVersion}', PackagePacked) then
    begin
      Log(Format('MorseCore version comparison: %d', [CompareReleaseVersions(InstalledPacked, PackagePacked)]));
      if CompareReleaseVersions(InstalledPacked, PackagePacked) > 0 then
      begin
        SuppressibleMsgBox(CustomMessage('NewerVersion'), mbError, MB_OK, IDOK);
        Result := False;
      end;
    end;
  end;
end;
