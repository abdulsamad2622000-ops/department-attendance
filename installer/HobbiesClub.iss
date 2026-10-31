#define AppName "Departments Attendance System"
#define AppVersion "1.0.0"
#define AppExe "HobbiesClub.App.exe"

[Setup]
AppId={{19AA0F17-3F68-4F49-AF88-3F367E74D9C2}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=NovaStackHub
DefaultDirName={autopf}\DepartmentAttendance
DefaultGroupName=Departments Attendance System
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=DepartmentAttendance-Setup-{#AppVersion}
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
Name: "{commonappdata}\DepartmentAttendance"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
Source: "..\publish\HobbiesClub\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Departments Attendance System"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall Departments Attendance System"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Departments Attendance System"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{commonstartup}\Departments Attendance System"; Filename: "{app}\{#AppExe}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Open Departments Attendance System now"; Flags: nowait postinstall skipifsilent
