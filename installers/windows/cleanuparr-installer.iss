#define MyAppName "Cleanuparr"
#define MyAppVersion GetEnv("APP_VERSION")
#define MyAppPublisher "Cleanuparr Team"
#define MyAppURL "https://github.com/Cleanuparr/Cleanuparr"
#define MyAppExeName "Cleanuparr.exe"
#define MyServiceName "Cleanuparr"

[Setup]
AppId={{E8B2C9D4-6F87-4E42-B5C3-29E121D4BDFF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=.\installer
OutputBaseFilename=Cleanuparr_Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=no
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=Logo\favicon.ico
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1
Name: "installservice"; Description: "Install as Windows Service (Recommended)"; GroupDescription: "Service Installation"; Flags: checkedonce

[Files]
Source: "dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Logo\favicon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{app}\config"; Permissions: everyone-full

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\favicon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\favicon.ico"; Tasks: desktopicon

[Run]
; Create service only if it doesn't exist (fresh install)
Filename: "{sys}\sc.exe"; Parameters: "create ""{#MyServiceName}"" binPath= ""\""{app}\{#MyAppExeName}\"""" DisplayName= ""{#MyAppName}"" start= auto"; Tasks: installservice; Flags: runhidden; Check: not ServiceExists('{#MyServiceName}')
Filename: "{sys}\sc.exe"; Parameters: "description ""{#MyServiceName}"" ""Cleanuparr download management service"""; Tasks: installservice; Flags: runhidden; Check: not ServiceExists('{#MyServiceName}')

; Start service (both fresh install and update)
Filename: "{sys}\sc.exe"; Parameters: "start ""{#MyServiceName}"""; Tasks: installservice; Flags: runhidden

; Open web interface
Filename: "http://localhost:11011"; Description: "Open Cleanuparr Web Interface"; Flags: postinstall shellexec nowait; Check: IsTaskSelected('installservice')

; Run directly (if not installed as service)
Filename: "{app}\{#MyAppExeName}"; Description: "Run {#MyAppName} Application"; Flags: nowait postinstall skipifsilent; Check: not IsTaskSelected('installservice')

[Code]
procedure LogInstaller(const Msg: string);
var
  LogDir, LogFile, Line: string;
begin
  LogDir := ExpandConstant('{app}\config\logs');
  ForceDirectories(LogDir);
  LogFile := LogDir + '\cleanuparr-installer.log';
  Line := '[' + GetDateTimeString('yyyy/mm/dd hh:nn:ss', '-', ':') + '] ' + Msg + #13#10;
  SaveStringToFile(LogFile, Line, True);
end;

function ServiceExists(ServiceName: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query "' + ServiceName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function IsServiceRunning(ServiceName: string): Boolean;
var
  ResultCode: Integer;
  TempFile: string;
  StatusOutput: AnsiString;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\service_status.txt');
  
  // Use PowerShell to get service status
  if Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), 
          '-Command "try { (Get-Service -Name ''' + ServiceName + ''' -ErrorAction Stop).Status } catch { ''NotFound'' }" > "' + TempFile + '"', 
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
  begin
    if LoadStringFromFile(TempFile, StatusOutput) then
    begin
      Result := (Pos('Running', StatusOutput) > 0);
    end;
    DeleteFile(TempFile);
  end;
end;

function WaitForServiceStop(ServiceName: string; TimeoutSeconds: Integer): Boolean;
var
  Counter: Integer;
begin
  Result := True;
  Counter := 0;
  
  while Counter < TimeoutSeconds do
  begin
    if not IsServiceRunning(ServiceName) then
      Exit;
    Sleep(1000);
    Counter := Counter + 1;
  end;
  
  Result := False;
end;

procedure StopAndDeleteService(const Name: string);
var
  ResultCode, i: Integer;
begin
  if not ServiceExists(Name) then
  begin
    LogInstaller('Service ' + Name + ' not present, nothing to remove');
    Exit;
  end;

  if IsServiceRunning(Name) then
  begin
    LogInstaller('Stopping service ' + Name);
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop "' + Name + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if not WaitForServiceStop(Name, 30) then
      LogInstaller('WARNING: service did not report STOPPED within 30s');
  end;

  LogInstaller('Force-killing ' + ExpandConstant('{#MyAppExeName}'));
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /t /im "' + ExpandConstant('{#MyAppExeName}') + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  for i := 1 to 10 do
  begin
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete "' + Name + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if not ServiceExists(Name) then
    begin
      LogInstaller('Service ' + Name + ' deleted (attempt ' + IntToStr(i) + ')');
      Exit;
    end;
    Sleep(1000);
  end;
  LogInstaller('WARNING: service ' + Name + ' still present after 10 delete attempts');
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  
  // If service exists and is running, stop it for the update
  if ServiceExists('{#MyServiceName}') and IsServiceRunning('{#MyServiceName}') then
  begin
    if MsgBox('Cleanuparr service is currently running and needs to be stopped for the installation. Continue?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      Exec(ExpandConstant('{sys}\sc.exe'), 'stop "{#MyServiceName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      
      if not WaitForServiceStop('{#MyServiceName}', 30) then
      begin
        MsgBox('Warning: Service took longer than expected to stop. Installation will continue but you may need to restart the service manually.', 
               mbInformation, MB_OK);
      end;
    end
    else
    begin
      Result := False;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;

  if ServiceExists('{#MyServiceName}') then
  begin
    if MsgBox('Cleanuparr service will be stopped and removed. Continue with uninstallation?',
              mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := False;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    LogInstaller('=== Uninstall started ===');
    StopAndDeleteService('{#MyServiceName}');
  end;
end;