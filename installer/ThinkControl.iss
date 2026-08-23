#ifndef AppVersion
  #define AppVersion "0.1.0-alpha.1"
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

#define DotNetDesktopVersion "10.0.10"
#define DotNetDesktopFile "windowsdesktop-runtime-10.0.10-win-x64.exe"
#define DotNetDesktopUrl "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x64.exe"
#define DotNetDesktopSha256 "E82FC901C8F52D716293B2BC0830CE0DD254A06268C457A19E8FC503560A84D1"

#define PawnIoVersion "2.2.0"
#define PawnIoFile "PawnIO_setup.exe"
#define PawnIoUrl "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe"
#define PawnIoSha256 "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032"

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
SetupIconFile=..\src\ThinkControl.UI\Assets\ThinkControl.ico
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
Name: "hardwareaccess"; Description: "Install X9 hardware access (PawnIO {#PawnIoVersion})"; GroupDescription: "ThinkPad X9 hardware:"; Flags: checkedonce; Check: IsVerifiedX9

[Files]
Source: "{#UiSourceDir}\*"; DestDir: "{app}\ui"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ServiceSourceDir}\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ThinkControl"; Filename: "{app}\ui\{#UiExeName}"; IconFilename: "{app}\ui\{#UiExeName}"
Name: "{autodesktop}\ThinkControl"; Filename: "{app}\ui\{#UiExeName}"; IconFilename: "{app}\ui\{#UiExeName}"; Tasks: desktopicon

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
var
  PawnIoWarning: String;

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

function ReadBiosIdentity(): String;
var
  Value: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM, 'HARDWARE\DESCRIPTION\System\BIOS', 'SystemSKU', Value) then
    Result := Result + ' ' + Value;
  if RegQueryStringValue(HKLM, 'HARDWARE\DESCRIPTION\System\BIOS', 'SystemProductName', Value) then
    Result := Result + ' ' + Value;
  if RegQueryStringValue(HKLM, 'HARDWARE\DESCRIPTION\System\BIOS', 'SystemFamily', Value) then
    Result := Result + ' ' + Value;
  Result := Uppercase(Result);
end;

function IsVerifiedX9(): Boolean;
var
  Identity: String;
begin
  Identity := ReadBiosIdentity();
  Result := (Pos('21Q6', Identity) > 0) or (Pos('21Q7', Identity) > 0);
end;

function PawnIoInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query PawnIO', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
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

procedure InstallPawnIoIfNeeded(var NeedsRestart: Boolean);
var
  ResultCode: Integer;
  InstallerPath: String;
begin
  PawnIoWarning := '';

  if not IsVerifiedX9() then
    Exit;
  if not WizardIsTaskSelected('hardwareaccess') then
    Exit;
  if PawnIoInstalled() then
  begin
    Log('PawnIO is already installed.');
    Exit;
  end;

  try
    Log('Downloading verified PawnIO {#PawnIoVersion} for ThinkPad X9 EC access.');
    DownloadTemporaryFile(
      '{#PawnIoUrl}',
      '{#PawnIoFile}',
      '{#PawnIoSha256}',
      nil);
  except
    PawnIoWarning := 'Hardware access could not be downloaded. ThinkControl will still install, but X9 fan RPM/control may remain unavailable.';
    Log(PawnIoWarning + ' ' + GetExceptionMessage);
    Exit;
  end;

  InstallerPath := ExpandConstant('{tmp}\{#PawnIoFile}');
  if not Exec(InstallerPath, '-install -silent', '', SW_SHOW,
      ewWaitUntilTerminated, ResultCode) then
  begin
    PawnIoWarning := 'PawnIO setup could not be started. ThinkControl will continue with Windows and Lenovo providers only.';
    Log(PawnIoWarning);
    Exit;
  end;

  if ResultCode = 3010 then
    NeedsRestart := True
  else if ResultCode <> 0 then
  begin
    PawnIoWarning := Format('PawnIO setup returned exit code %d. ThinkControl will install, but X9 EC fan access may remain unavailable.', [ResultCode]);
    Log(PawnIoWarning);
    Exit;
  end;

  if not PawnIoInstalled() then
  begin
    PawnIoWarning := 'PawnIO was installed but its service is not ready yet. A Windows restart may be required before X9 fan access appears.';
    Log(PawnIoWarning);
  end;
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

  { Stopping the service disposes the active Lenovo hardware controller. Its
    safety invariant returns any verified X9 manual fan level to Lenovo Auto
    before process exit. }
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);

  { Give Service Control Manager a short window to release the old service EXE
    before Inno replaces the payload during an upgrade. }
  Sleep(1200);

  InstallPawnIoIfNeeded(NeedsRestart);
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

    if (PawnIoWarning <> '') and (not WizardSilent) then
      MsgBox(PawnIoWarning, mbInformation, MB_OK);
  end;
end;
