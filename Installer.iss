[Setup]
AppId={code:InstallIdentity}
AppName=BenchMark X
AppVersion=1.7.0
AppPublisher=BenchMark X
DefaultDirName={autopf}\BenchMarkX
UsePreviousAppDir=no
DisableDirPage=yes
DefaultGroupName=BenchMark X
UsePreviousGroup=no
UsePreviousLanguage=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=artifacts
OutputBaseFilename=BenchMark-X-1.7.0-Precision-Windows-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=assets\BenchMarkX.ico
UninstallDisplayIcon={app}\BenchMarkX.exe
CloseApplications=yes

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked


[Files]
Source: "release-benchmarkx\dependencies\PawnIO_setup.exe"; Flags: dontcopy
Source: "release-benchmarkx\DriverPrerequisiteProbe.exe"; Flags: dontcopy
Source: "release-benchmarkx\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\Overclock.exe"
Type: files; Name: "{app}\Overclock.exe.config"
Type: files; Name: "{autodesktop}\Overclock Native.lnk"; Check: not IsValidation
Type: files; Name: "{userprograms}\Overclock Native\Overclock Native.lnk"; Check: not IsValidation

[Icons]
Name: "{group}\BenchMark X"; Filename: "{app}\BenchMarkX.exe"; Check: not IsValidation
Name: "{group}\User guide"; Filename: "{app}\README.md"; Check: not IsValidation
Name: "{autodesktop}\BenchMark X"; Filename: "{app}\BenchMarkX.exe"; Tasks: desktopicon; Check: not IsValidation

[Run]

Filename: "{app}\BenchMarkX.exe"; Description: "Launch BenchMark X"; Flags: nowait postinstall skipifsilent runascurrentuser; Check: CanLaunchApp

[Code]
var
  DriverReady: Boolean;
  DriverRestartRequired: Boolean;

function IsValidation(): Boolean;
begin Result := ExpandConstant('{param:VALIDATION|0}') = '1'; end;

function InstallIdentity(Param: String): String;
begin Result := '{4A289D5E-F694-4C02-BA1B-395BC779D5BD}'; if IsValidation then Result := Result + '-Validation'; end;

function ProbeDriver(var Reason: String): Boolean;
var ExitCode: Integer; StatusFile: String; Text: AnsiString;
begin
  StatusFile := ExpandConstant('{tmp}\pawnio-status.txt');
  DeleteFile(StatusFile);
  Result := Exec(ExpandConstant('{tmp}\DriverPrerequisiteProbe.exe'), '--check "' + StatusFile + '"', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if not Result then begin Reason := 'The hardware access check could not start (Windows error ' + IntToStr(ExitCode) + ').'; Exit; end;
  Result := ExitCode = 0;
  if LoadStringFromFile(StatusFile, Text) then Reason := String(Text) else Reason := 'Hardware check exit code: ' + IntToStr(ExitCode);
  Log(Reason);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer; Reason: String;
begin
  Result := '';
  if DriverReady or DriverRestartRequired then Exit;
  ExtractTemporaryFile('DriverPrerequisiteProbe.exe');
  if ProbeDriver(Reason) then begin DriverReady := True; Exit; end;
  WizardForm.PreparingLabel.Caption := 'Configuring required hardware access...';
  ExtractTemporaryFile('PawnIO_setup.exe');
  if CompareText(GetSHA256OfFile(ExpandConstant('{tmp}\PawnIO_setup.exe')), '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032') <> 0 then begin
    Result := 'The bundled PawnIO installer failed its integrity check. Download a fresh BenchMark X installer.'; Exit;
  end;
  Log('Installing required signed PawnIO 2.2.0 dependency automatically.');
  if not Exec(ExpandConstant('{tmp}\PawnIO_setup.exe'), '-install -silent', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
    Result := 'PawnIO could not be started (Windows error ' + IntToStr(ExitCode) + '). Setup cannot continue.'; Exit;
  end;
  Log('PawnIO installer exit code: ' + IntToStr(ExitCode));
  if ExitCode = 3010 then begin
    DriverRestartRequired := True;
    Log('Windows restart required for hardware access. Application launch is deferred.');
    Exit;
  end;
  if ExitCode <> 0 then begin
    Result := 'Required hardware access could not be installed (PawnIO exit code ' + IntToStr(ExitCode) + '). Setup cannot continue. See the setup log for details.'; Exit;
  end;
  if not ProbeDriver(Reason) then begin
    Result := 'PawnIO was installed but is not ready: ' + Reason + #13#10 + 'Restart Windows and run setup again. Setup has not completed.'; Exit;
  end;
  DriverReady := True;
end;

function NeedRestart(): Boolean;
begin Result := DriverRestartRequired; end;

function CanLaunchApp(): Boolean;
begin Result := DriverReady and not DriverRestartRequired; end;

function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then MsgBox('.NET Framework 4.8 or later is required. Install it from Microsoft before continuing.', mbError, MB_OK);
end;