; Lantern Installer — Inno Setup 6
#define AppName "Lantern"
#define AppVersion "10.1"
#define AppPublisher "krinix1337"
#define AppURL "https://github.com/krinix1337/lantern-zapret"
#define AppExeName "Lantern.exe"
#define SrcDir ".."

[Setup]
; №4: AppId должен совпадать со значением во всех опубликованных релизах
; (9.0/9.1) и с [assembly: Guid] в _app/AssemblyInfo.cs. Inno опознаёт продукт
; по AppId: при другом GUID автообновление ставит вторую копию рядом.
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
CloseApplications=yes
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

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Stop and delete the zapret service and WinDivert drivers if they were installed
Filename: "{sys}\net.exe"; Parameters: "stop zapret"; Flags: runhidden
; №93: ручной запуск winws службой не управляется. Без снятия процесса
; {app}\zapret\bin не удаляется — winws.exe и WinDivert64.sys залочены.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM winws.exe /T"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete zapret"; Flags: runhidden
Filename: "{sys}\net.exe"; Parameters: "stop WinDivert"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete WinDivert"; Flags: runhidden
Filename: "{sys}\net.exe"; Parameters: "stop WinDivert14"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete WinDivert14"; Flags: runhidden
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM TgWsProxy_windows.exe /T"; Flags: runhidden
; Remove scheduled autostart task if registered
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /F /TN ""LanternAutostart"""; Flags: runhidden

[UninstallDelete]
; Движок и бинарники — удаляем всегда
Type: filesandordirs; Name: "{app}\zapret\bin"
Type: filesandordirs; Name: "{app}\zapret\service.bat"
Type: files; Name: "{app}\zapret\general*.bat"
; №89: {app}\zapret\lists (включая *-user.txt и ipset-all.txt.backup) и
; {app}\zapret\utils\gui-config.ini намеренно НЕ удаляются: это данные
; пользователя. Их сносит [Code] только по явному ответу «Да».
Type: files; Name: "{app}\gui-config.ini"
; %LOCALAPPDATA%\Lantern (крэш-логи и сохранённые журналы из «Журнал →
; Сохранить») удаляется в [Code] только по явному ответу «Да» — раньше
; сносилось безусловно, хотя это тоже данные пользователя.


[Code]
var
  RemoveUserData: Boolean;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  // В тихом режиме (/SILENT, /VERYSILENT) диалог не показываем и данные
  // сохраняем: молча сносить пользовательские списки нельзя.
  RemoveUserData := False;
  if not UninstallSilent then
    RemoveUserData := (MsgBox('Lantern остановит процессы zapret (winws, TgWsProxy) и удалит службу и драйвер WinDivert, если они установлены.' + #13#10 + #13#10 +
                              'Удалить также пользовательские данные — списки доменов, ipset, gui-config.ini и сохранённые журналы?' + #13#10 +
                              '«Нет» сохранит их для повторной установки.',
                              mbConfirmation, MB_YESNO) = IDYES);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // №88: автозапуск штатно реализован задачей планировщика LanternAutostart
    // (снимается в [UninstallRun]). Запись в Run могла остаться от версий до
    // перехода на планировщик — подчищаем её здесь вместо мёртвой секции [Registry].
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Lantern');
    if RemoveUserData then
    begin
      DelTree(ExpandConstant('{app}\zapret'), True, True, True);
      // Сохранённые журналы и крэш-логи — тоже данные пользователя: при
      // ответе «Нет» (и при тихом удалении) они остаются на месте.
      DelTree(ExpandConstant('{localappdata}\Lantern'), True, True, True);
    end;
  end;
end;
