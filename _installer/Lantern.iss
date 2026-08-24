; Lantern Installer — Inno Setup 6
#define AppName "Lantern"
#define AppVersion "6.1"
#define AppPublisher "krinix1337"
#define AppURL "https://github.com/krinix1337/lantern-zapret"
#define AppExeName "Lantern.exe"
#define SrcDir "C:\project\zapret\zapret-discord-youtube-1.9.9d"

[Setup]
AppId={{A7F3B2C1-4D5E-6F7A-8B9C-0D1E2F3A4B5C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#SrcDir}\_installer
OutputBaseFilename=Lantern-Setup
SetupIconFile={#SrcDir}\_app\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersion}
LicenseFile={#SrcDir}\LICENSE.txt

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Only the app — zapret components are downloaded in-app
Source: "{#SrcDir}\Lantern.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\assets\peter-songs\*"; DestDir: "{app}\assets\peter-songs"; Flags: ignoreversion createallsubdirs recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{app}\zapret"
Type: files; Name: "{app}\gui-config.ini"
