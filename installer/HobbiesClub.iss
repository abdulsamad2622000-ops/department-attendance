#define AppName "Fleet UMS Hobby Club Attendance"
#define AppVersion "1.8.5"
#define AppExe "HobbiesClub.App.exe"

[Setup]
AppId={{8F3C2A51-7B4E-4D2A-9C61-3E5B7A9D1F20}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=NovaStackHub
DefaultDirName={autopf}\HobbiesClub
DefaultGroupName=Fleet UMS Hobby Club
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=HobbiesClub-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start automatically when Windows starts (kiosk PC)"; GroupDescription: "Kiosk:"; Flags: unchecked

[Dirs]
; Data folder: every Windows user can read/write the database and backups. Never removed on uninstall.
Name: "{commonappdata}\HobbiesClub"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
Source: "..\publish\HobbiesClub\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Fleet UMS Hobby Club"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall Fleet UMS Hobby Club"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Fleet UMS Hobby Club"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{commonstartup}\Fleet UMS Hobby Club"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Open Fleet UMS Hobby Club now"; Flags: nowait postinstall skipifsilent














