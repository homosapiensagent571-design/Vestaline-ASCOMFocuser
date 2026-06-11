#define MyAppName "VestalFocuser"
#define MyAppVersion "0.6.10"
#define MyAppPublisher "Vestaline"
#define DriverProgID "ASCOM.Autofocus.Focuser"
#define DriverDescription "VestalFocuser beta 0.6.10"
#define SourceRoot ".."

[Setup]
AppId={{B7D5A3C1-E8F2-4A90-BCED-F123456789AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=VestalFocuser-{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UninstallDisplayIcon={app}\VestalFocuser.exe
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Driver DLL and .NET dependencies
Source: "{#SourceRoot}\ASCOM_Driver\bin\Debug\net48\ASCOM.Autofocus.Focuser.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\ASCOM_Driver\bin\Debug\net48\ASCOM.DeviceInterfaces.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\ASCOM_Driver\bin\Debug\net48\ASCOM.Exceptions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\ASCOM_Driver\bin\Debug\net48\ASCOM.Utilities.dll"; DestDir: "{app}"; Flags: ignoreversion
; Companion WinForms app
Source: "{#SourceRoot}\FocuserApp\bin\VestalFocuser.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\VestalFocuser Control"; Filename: "{app}\VestalFocuser.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall VestalFocuser"; Filename: "{uninstallexe}"

[Run]
; Register COM 32-bit (also triggers [ComRegisterFunction] which writes ASCOM Profile)
Filename: "{win}\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"; \
    Parameters: "/codebase ""{app}\ASCOM.Autofocus.Focuser.dll"""; \
    StatusMsg: "Registering COM (32-bit)..."; Flags: runhidden
; Register COM 64-bit
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; \
    Parameters: "/codebase ""{app}\ASCOM.Autofocus.Focuser.dll"""; \
    StatusMsg: "Registering COM (64-bit)..."; Flags: runhidden; Check: IsWin64

[UninstallRun]
; Unregister COM 32-bit (also triggers [ComUnregisterFunction] which cleans ASCOM Profile)
Filename: "{win}\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"; \
    Parameters: "/unregister ""{app}\ASCOM.Autofocus.Focuser.dll"""; \
    StatusMsg: "Unregistering COM (32-bit)..."; Flags: runhidden; RunOnceId: "UnregCOM32"
; Unregister COM 64-bit
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; \
    Parameters: "/unregister ""{app}\ASCOM.Autofocus.Focuser.dll"""; \
    StatusMsg: "Unregistering COM (64-bit)..."; Flags: runhidden; Check: IsWin64; RunOnceId: "UnregCOM64"

[Code]
function InitializeSetup: Boolean;
var
  RegResult: Boolean;
begin
  Result := True;

  // Check .NET Framework 4.8
  RegResult := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\.NET Framework\v4.0.30319');
  if not RegResult then
    RegResult := RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\.NET Framework\v4.0.30319');
  if not RegResult then
  begin
    MsgBox('.NET Framework 4.8 is required but was not detected.' + #13#13 +
      'Please install .NET Framework 4.8 (https://dotnet.microsoft.com/download/dotnet-framework/net48) and re-run this setup.',
      mbCriticalError, MB_OK);
    Result := False;
    Exit;
  end;

  // Check ASCOM Platform 7+
  RegResult := RegKeyExists(HKLM, 'SOFTWARE\ASCOM\Platform');
  if not RegResult then
    RegResult := RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\ASCOM\Platform');
  if not RegResult then
  begin
    MsgBox('ASCOM Platform 7+ is required but was not detected.' + #13#13 +
      'Please install ASCOM Platform (https://github.com/ASCOMInitiative/ASCOMPlatform/releases) and re-run this setup.',
      mbCriticalError, MB_OK);
    Result := False;
    Exit;
  end;
end;
