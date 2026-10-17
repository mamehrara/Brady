; Brady's installer (Inno Setup 6). Build it with build/build-installer.ps1, which publishes the
; app and passes AppVersion (from Directory.Build.props) and PublishDir.
;
; Per-user, no admin prompt: installs to %LocalAppData%\Programs\Brady.
; "Start with Windows" uses the same HKCU Run value as Brady's Settings, so they never disagree.

#ifndef AppVersion
  #error Pass /DAppVersion=x.y.z (build/build-installer.ps1 does this).
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif

#define AppName "Brady"
#define AppExe "Brady.exe"
; Must match SingleInstance.MutexName and AppIdentity in the app.
#define AppMutexName "Brady-7c1d2a54-5f0e-4f4b-9a63-2e8f1b6d0c41"
#define AppUserModelId "mamehrara.Brady"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
#define UninstallKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\{AEAB95B4-8658-4D0B-AF0A-5BEC8EDFFD65}_is1"

[Setup]
AppId={{AEAB95B4-8658-4D0B-AF0A-5BEC8EDFFD65}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=mamehrara
AppPublisherURL=https://github.com/mamehrara/Brady
AppSupportURL=https://github.com/mamehrara/Brady/issues
AppUpdatesURL=https://github.com/mamehrara/Brady/releases
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Brady setup
DefaultDirName={localappdata}\Programs\{#AppName}
DisableWelcomePage=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts
OutputBaseFilename=Brady-Setup-{#AppVersion}
SetupIconFile=..\design\icons\brady-app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardImageFile=installer\wizard-large-164x314.bmp,installer\wizard-large-205x392.bmp,installer\wizard-large-246x471.bmp,installer\wizard-large-328x628.bmp
WizardSmallImageFile=installer\wizard-small-55x55.bmp,installer\wizard-small-69x69.bmp,installer\wizard-small-82x82.bmp,installer\wizard-small-110x110.bmp
Compression=lzma2/ultra64
SolidCompression=yes
; No AppMutex: Inno checks it before any [Code] runs, so it can only refuse to start (and a silent
; upgrade would fail). Setup instead asks Brady to quit through its own quit event; see QuitBrady.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This installs [name/ver] on your computer.%n%nBrady is a three-toed sloth who hangs from the corner of your screen and gently reminds you to sit tall, walk, rest your eyes, stretch and drink water.%n%nIt installs just for you, with no admin rights needed.
FinishedLabel=Brady is installed. Look for him in the notification area, by the clock.

[Tasks]
Name: "startup"; Description: "Start Brady when Windows starts"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; AppUserModelID: "{#AppUserModelId}"; Comment: "Gentle break reminders"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; AppUserModelID: "{#AppUserModelId}"; Comment: "Gentle break reminders"; Tasks: desktopicon

[Registry]
; The startup entry, exactly as Brady's Settings writes it.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" --background"; Tasks: startup
; Uninstall removes it whether the installer or Settings created it.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: none; ValueName: "{#AppName}"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch Brady now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Brady\logs"
Type: dirifempty; Name: "{localappdata}\Brady"

[Code]
const
  EVENT_MODIFY_STATE = $0002;

var
  IsUpgrade: Boolean;
  HadStartupEntry: Boolean;
  WasRunning: Boolean;

function OpenEvent(DesiredAccess: DWORD; InheritHandle: BOOL; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Event: THandle): BOOL;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

function BradyIsRunning(): Boolean;
begin
  Result := CheckForMutexes('{#AppMutexName}');
end;

{ Asks a running Brady to exit cleanly, exactly like "Brady.exe --quit": it sets Brady's named
  quit event (any Brady, installed or not). Then waits up to 10 seconds for it to go. }
function QuitBrady(): Boolean;
var
  Event: THandle;
  Waited: Integer;
begin
  if BradyIsRunning() then
  begin
    Event := OpenEvent(EVENT_MODIFY_STATE, False, 'Local\{#AppMutexName}-quit');
    if Event <> 0 then
    begin
      SetEvent(Event);
      CloseHandle(Event);
    end;
    Waited := 0;
    while BradyIsRunning() and (Waited < 10000) do
    begin
      Sleep(200);
      Waited := Waited + 200;
    end;
  end;
  Result := not BradyIsRunning();
end;

function InitializeSetup(): Boolean;
begin
  IsUpgrade := RegValueExists(HKCU, '{#UninstallKey}', 'UninstallString');
  HadStartupEntry := RegValueExists(HKCU, '{#RunKey}', '{#AppName}');
  WasRunning := BradyIsRunning();
  Result := True;
end;

{ On an upgrade the startup checkbox shows the current choice (which Settings may have changed). }
procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpSelectTasks) and IsUpgrade then
  begin
    if HadStartupEntry then
      WizardSelectTasks('startup')
    else
      WizardSelectTasks('!startup');
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if BradyIsRunning() then WasRunning := True;
  if QuitBrady() then
    Result := ''
  else
    Result := 'Brady is still running. Quit it from its tray menu, then try again.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  KeepStartup: Boolean;
  Code: Integer;
begin
  if CurStep <> ssPostInstall then Exit;

  { A silent upgrade keeps whatever startup choice was in place; otherwise the task decides. }
  if IsUpgrade and WizardSilent() then
    KeepStartup := HadStartupEntry
  else
    KeepStartup := WizardIsTaskSelected('startup');
  if KeepStartup then
    RegWriteStringValue(HKCU, '{#RunKey}', '{#AppName}', '"' + ExpandConstant('{app}\{#AppExe}') + '" --background')
  else
    RegDeleteValue(HKCU, '{#RunKey}', '{#AppName}');

  { Relaunch a Brady that was running before a silent upgrade; the wizard has its own checkbox. }
  if WasRunning and WizardSilent() then
    Exec(ExpandConstant('{app}\{#AppExe}'), '--background', '', SW_SHOWNORMAL, ewNoWait, Code);
end;

function InitializeUninstall(): Boolean;
begin
  QuitBrady();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Settings: String;
begin
  if CurUninstallStep <> usPostUninstall then Exit;
  Settings := ExpandConstant('{userappdata}\Brady');
  if DirExists(Settings) then
    if SuppressibleMsgBox('Also delete your Brady settings?' + #13#10#13#10 +
        'Keep them if you might install Brady again.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
      DelTree(Settings, True, True, True);
end;
