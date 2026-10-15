; Isolated wizard using the same prerequisite code as the production Setup.
[Setup]
AppId=MorseCore.PrerequisiteQA.{#QaId}
AppName=MorseCore Prerequisite QA
AppVersion=1.0.0
DefaultDirName={#QaDirectory}\app
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallFilesDir={app}
OutputDir={#QaDirectory}
OutputBaseFilename=prerequisite-test
DisableProgramGroupPage=yes
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

#include "..\..\packaging\windows\PrerequisiteMessages.iss"
#include "..\..\packaging\windows\PythonPrerequisite.iss"

[Files]
Source: "..\..\LICENSE"; DestDir: "{app}"

[Code]
procedure InitializeWizard();
begin InitializePythonPrerequisite(); end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin Result := InstallPythonPrerequisite(NeedsRestart); end;
