#ifndef AppVersion
  #define AppVersion "0.1.0-dev"
#endif

#ifndef NumericVersion
  #define NumericVersion "0.1.0.0"
#endif

#ifndef UiSourceDir
  #define UiSourceDir "..\artifacts\ui"
#endif

#ifndef ServiceSourceDir
  #define ServiceSourceDir "..\artifacts\service"
#endif

#define AppName "ThinkControl"
#define UiExeName "ThinkControl.UI.exe"
#define ServiceExeName "ThinkControl.Service.exe"
#define ServiceName "ThinkControlService"
#define AppPublisher "ThinkControl Project"
#define AppUrl "https://github.com/Hugowhitee/ThinkControl"

[Setup]
AppId={{5E69D050-3273-4CC7-9160-9148D839AB29}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=ThinkPad hardware companion
VersionInfoProductName={#AppName}
VersionInfoVersion={#NumericVersion}
VersionInfoProductVersion={#NumericVersion}
DefaultDirName={autopf}\ThinkControl
DefaultGroupName=ThinkControl
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=ThinkControl-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern dynamic windows11
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
UninstallDisplayName=ThinkControl
UninstallDisplayIcon={app}\ui\{#UiExeName}
CloseApplications=yes
CloseApplicationsFilter={#UiExeName}
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#UiSourceDir}\*"; DestDir: "{app}\ui"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ServiceSourceDir}\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ThinkControl"; Filename: "{app}\ui\{#UiExeName}"
Name: "{autodesktop}\ThinkControl"; Filename: "{app}\ui\{#UiExeName}"; Tasks: desktopicon

[Run]
; `sc create` is harmless on upgrades: it returns ERROR_SERVICE_EXISTS and the
; following `sc config` updates the existing registration to the new path.
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\service\{#ServiceExeName}"" start= auto DisplayName= ""ThinkControl Hardware Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""Verified ThinkControl hardware access service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "config {#ServiceName} binPath= ""{app}\service\{#ServiceExeName}"" start= auto DisplayName= ""ThinkControl Hardware Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated
Filename: "{app}\ui\{#UiExeName}"; Description: "Launch ThinkControl"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopThinkControlService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteThinkControlService"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  NeedsRestart := False;

  { Stopping the service disposes X9HardwareController. Its safety invariant
    returns any active manual fan level to Lenovo BIOS/Auto before process exit. }
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);

  { Give Service Control Manager a short window to release the old service EXE
    before Inno replaces the payload during an upgrade. }
  Sleep(1200);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    { Automatic recovery is intentionally conservative: restart the service after
      ordinary failures, but do not create an endless restart loop. }
    Exec(ExpandConstant('{sys}\sc.exe'),
      'failure {#ServiceName} reset= 86400 actions= restart/5000',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
