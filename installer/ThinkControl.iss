#ifndef AppVersion
  #define AppVersion "0.1.0-alpha.2"
#endif

#ifndef NumericVersion
  #define NumericVersion "0.1.0.0"
#endif

#ifndef PayloadFile
  #define PayloadFile "ThinkControl-Payload-0.1.0-alpha.2.zip"
#endif

#ifndef PayloadUrl
  #define PayloadUrl "https://github.com/Hugowhitee/ThinkControl/releases/download/v0.1.0-alpha.2/ThinkControl-Payload-0.1.0-alpha.2.zip"
#endif

#ifndef PayloadSha256
  #define PayloadSha256 "UNSET"
#endif

#define AppName "ThinkControl"
#define UiExeName "ThinkControl.UI.exe"
#define ServiceExeName "ThinkControl.Service.exe"
#define ServiceName "ThinkControlService"
#define AppPublisher "ThinkControl Project"
#define AppUrl "https://github.com/Hugowhitee/ThinkControl"

#define DotNetDesktopVersion "10.0.10"
#define DotNetDesktopFile "windowsdesktop-runtime-10.0.10-win-x64.exe"
#define DotNetDesktopUrl "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x64.exe"
#define DotNetDesktopSha256 "E82FC901C8F52D716293B2BC0830CE0DD254A06268C457A19E8FC503560A84D1"

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
VersionInfoDescription=Lenovo hardware companion
VersionInfoProductName={#AppName}
VersionInfoVersion={#NumericVersion}
VersionInfoProductVersion={#NumericVersion}
DefaultDirName={autopf}\ThinkControl
DefaultGroupName=ThinkControl
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
SetupIconFile=..\assets\brand\v3\windows\ThinkControl_setup.ico
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
UninstallDisplayIcon={app}\ThinkControl.ico
CloseApplications=yes
CloseApplicationsFilter={#UiExeName}
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "..\assets\brand\v3\windows\ThinkControl.ico"; DestDir: "{app}"; DestName: "ThinkControl.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\ThinkControl"; Filename: "{app}\ui\{#UiExeName}"; IconFilename: "{app}\ThinkControl.ico"
Name: "{autodesktop}\ThinkControl"; Filename: "{app}\ui\{#UiExeName}"; IconFilename: "{app}\ThinkControl.ico"; Tasks: desktopicon

[Run]
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\service\{#ServiceExeName}"" start= auto DisplayName= ""ThinkControl Hardware Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""Verified ThinkControl hardware access service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "config {#ServiceName} binPath= ""{app}\service\{#ServiceExeName}"" start= auto DisplayName= ""ThinkControl Hardware Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated
Filename: "{app}\ui\{#UiExeName}"; Description: "Launch ThinkControl"; Flags: nowait postinstall skipifsilent runasoriginaluser
Filename: "{app}\ui\{#UiExeName}"; Flags: nowait skipifnotsilent runasoriginaluser; Check: ShouldRelaunchAfterSilentUpdate

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopThinkControlService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteThinkControlService"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\ui"
Type: filesandordirs; Name: "{app}\service"

[Code]
var
  PayloadPath: String;
  ExistingInstall: Boolean;

function IsUpdateParameter(): Boolean;
begin
  Result := CompareText(ExpandConstant('{param:UPDATE|0}'), '1') = 0;
end;

function ShouldRelaunchAfterSilentUpdate(): Boolean;
begin
  Result := WizardSilent and IsUpdateParameter() and
    (CompareText(ExpandConstant('{param:RELAUNCH|0}'), '1') = 0);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := ExistingInstall and (PageID = wpSelectTasks);
end;

procedure InitializeWizard();
begin
  ExistingInstall := FileExists(ExpandConstant('{autopf}\ThinkControl\ui\{#UiExeName}'));
  if ExistingInstall or IsUpdateParameter() then
  begin
    WizardForm.Caption := 'Update ThinkControl';
    WizardForm.NextButton.Caption := 'Update';
  end;
end;

function HasDotNetDesktop10(): Boolean;
var
  FindRec: TFindRec;
  SearchPath: String;
begin
  Result := False;
  SearchPath := ExpandConstant('{autopf}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*');
  if FindFirst(SearchPath, FindRec) then
  begin
    try
      repeat
        if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
           (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function InstallDotNetDesktop(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  InstallerPath: String;
begin
  Result := '';
  if HasDotNetDesktop10() then
  begin
    Log('.NET Desktop Runtime 10 is already installed.');
    Exit;
  end;

  try
    Log('Downloading verified Microsoft .NET Desktop Runtime {#DotNetDesktopVersion} x64.');
    DownloadTemporaryFile(
      '{#DotNetDesktopUrl}',
      '{#DotNetDesktopFile}',
      '{#DotNetDesktopSha256}',
      nil);
  except
    Result := 'ThinkControl could not download the required Microsoft .NET 10 Desktop Runtime: ' + GetExceptionMessage;
    Exit;
  end;

  InstallerPath := ExpandConstant('{tmp}\{#DotNetDesktopFile}');
  if not Exec(InstallerPath, '/install /quiet /norestart', '', SW_SHOW,
      ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'The Microsoft .NET 10 Desktop Runtime installer could not be started.';
    Exit;
  end;

  if ResultCode = 3010 then
    NeedsRestart := True
  else if ResultCode <> 0 then
  begin
    Result := Format('Microsoft .NET Desktop Runtime setup returned exit code %d.', [ResultCode]);
    Exit;
  end;

  if not HasDotNetDesktop10() then
    Result := 'Microsoft .NET 10 Desktop Runtime did not become available after setup.';
end;

function AcquirePayload(): String;
var
  LocalPayload: String;
  ActualHash: String;
begin
  Result := '';
  PayloadPath := '';
  LocalPayload := ExpandConstant('{param:PAYLOAD|}');

  if LocalPayload <> '' then
  begin
    if not FileExists(LocalPayload) then
    begin
      Result := 'The local ThinkControl payload specified for validation does not exist.';
      Exit;
    end;

    if CompareText('{#PayloadSha256}', 'UNSET') = 0 then
    begin
      Result := 'ThinkControl setup was built without a payload checksum.';
      Exit;
    end;

    ActualHash := GetSHA256OfFile(LocalPayload);
    if CompareText(ActualHash, '{#PayloadSha256}') <> 0 then
    begin
      Result := 'The local ThinkControl payload failed SHA-256 verification.';
      Exit;
    end;

    PayloadPath := LocalPayload;
    Log('Using SHA-256 verified local ThinkControl payload for package validation.');
    Exit;
  end;

  if CompareText('{#PayloadSha256}', 'UNSET') = 0 then
  begin
    Result := 'ThinkControl setup was built without a payload checksum.';
    Exit;
  end;

  try
    Log('Downloading SHA-256 pinned ThinkControl payload from GitHub Releases.');
    DownloadTemporaryFile(
      '{#PayloadUrl}',
      '{#PayloadFile}',
      '{#PayloadSha256}',
      nil);
    PayloadPath := ExpandConstant('{tmp}\{#PayloadFile}');
  except
    Result := 'ThinkControl could not download its application payload from GitHub Releases: ' + GetExceptionMessage;
  end;
end;

function ExtractPayload(): String;
var
  ResultCode: Integer;
  TarPath: String;
  Params: String;
  AppDir: String;
begin
  Result := '';
  AppDir := ExpandConstant('{app}');
  TarPath := ExpandConstant('{sys}\tar.exe');

  if (PayloadPath = '') or not FileExists(PayloadPath) then
  begin
    Result := 'The verified ThinkControl payload is unavailable.';
    Exit;
  end;

  if not FileExists(TarPath) then
  begin
    Result := 'Windows tar.exe is unavailable; ThinkControl cannot extract its verified payload.';
    Exit;
  end;

  ForceDirectories(AppDir);
  DelTree(ExpandConstant('{app}\ui'), True, True, True);
  DelTree(ExpandConstant('{app}\service'), True, True, True);

  Params := '-xf "' + PayloadPath + '" -C "' + AppDir + '"';
  if not Exec(TarPath, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Windows could not start the ThinkControl payload extractor.';
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    Result := Format('ThinkControl payload extraction returned exit code %d.', [ResultCode]);
    Exit;
  end;

  if not FileExists(ExpandConstant('{app}\ui\{#UiExeName}')) or
     not FileExists(ExpandConstant('{app}\service\{#ServiceExeName}')) then
  begin
    Result := 'ThinkControl payload extraction completed but the required UI/service executables are missing.';
  end;
end;

procedure CloseRunningThinkControl();
var
  ResultCode: Integer;
begin
  { Inno Setup's Restart Manager normally closes the app. Explicit taskkill is a
    final update-mode guard so stale tray-only instances cannot keep payload DLLs
    locked and turn an update into a file-in-use error. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#UiExeName} /T /F', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
  Sleep(450);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  NeedsRestart := False;

  Result := InstallDotNetDesktop(NeedsRestart);
  if Result <> '' then
    Exit;

  CloseRunningThinkControl();

  { Stop the existing service before replacing its payload. Normal controller
    disposal returns any verified manual fan ownership to Lenovo Auto. }
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
  Sleep(1200);

  Result := AcquirePayload();
  if Result <> '' then
    Exit;

  Result := ExtractPayload();
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    Exec(ExpandConstant('{sys}\sc.exe'),
      'failure {#ServiceName} reset= 86400 actions= restart/5000',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
