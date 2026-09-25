#define AppName "Event Sequencer"
#define AppShortName "EVSEQ"
; build.ps1 -Version passes /DAppVersion=x.y.z; this default is used for plain local builds
#ifndef AppVersion
  #define AppVersion "0.3.1"
#endif
#define AppExe "EVSEQ.exe"

[Setup]
; Same AppId as the earlier "Audio Player" builds, so installing EVSEQ upgrades them in place.
AppId={{8F3C2A51-6B7D-4E2A-9C1F-3D5E7A9B0C12}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} ({#AppShortName}) {#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=EVSEQ-Setup-{#AppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
; Tell Explorer about the .approj association so icons/double-click work right away
ChangesAssociations=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} ({#AppShortName})
; Brand: logo mark as the setup/uninstall icon; the wizard is light, so the full logo reads well there
SetupIconFile=..\assets\brand\evseq.ico
WizardImageFile=wizard-large.png
WizardSmallImageFile=wizard-small.png
; Code signing (F8): enable once a certificate is available:
; SignTool=signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f "cert.pfx" /p "$PASSWORD" $f
; SignedUninstaller=yes

[Tasks]
Name: "desktopicon"; Description: "Buat ikon di desktop"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; DestName: "THIRD-PARTY-NOTICES.txt"; Flags: ignoreversion

[Registry]
; .approj opens in EVSEQ (double-click in Explorer); removed again on uninstall
Root: HKA; Subkey: "Software\Classes\.approj"; ValueType: string; ValueName: ""; ValueData: "EVSEQ.Project"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.approj\OpenWithProgids"; ValueType: string; ValueName: "EVSEQ.Project"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\EVSEQ.Project"; ValueType: string; ValueName: ""; ValueData: "Project EVSEQ"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\EVSEQ.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\EVSEQ.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""

[InstallDelete]
; executable of the earlier "Audio Player" builds, replaced by EVSEQ.exe
Type: files; Name: "{app}\AudioPlayer.*"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Jalankan {#AppName}"; Flags: nowait postinstall skipifsilent
; In-app "Pasang sekarang" runs this installer with /SILENT /RELAUNCH=1: reopen EVSEQ when done
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser; Check: RelaunchAfterUpdate

[Code]
{ Upgrade handling: an existing install (same AppId) turns this into a short update:
  no license/folder/start-menu/task pages, a clear "update from -> to" message, refuse downgrades,
  and wait for a running EVSEQ to close. }
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3C2A51-6B7D-4E2A-9C1F-3D5E7A9B0C12}_is1';
  AppMutexName = 'AudioPlayer.SingleInstance';  { held by EVSEQ.exe while it runs (name kept from before the rename) }

var
  PreviousVersion: String;

function GetPreviousVersion(): String;
begin
  Result := '';
  if not RegQueryStringValue(HKLM64, UninstallKey, 'DisplayVersion', Result) then
    if not RegQueryStringValue(HKLM32, UninstallKey, 'DisplayVersion', Result) then
      RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', Result);
end;

function IsUpgrade(): Boolean;
begin
  Result := PreviousVersion <> '';
end;

function InitializeSetup(): Boolean;
var
  Installed, Offered: Int64;
  Tries: Integer;
begin
  Result := True;
  PreviousVersion := GetPreviousVersion();

  { Never replace a newer install with an older one. }
  if IsUpgrade() and StrToVersion(PreviousVersion, Installed) and StrToVersion('{#AppVersion}', Offered)
     and (ComparePackedVersion(Installed, Offered) > 0) then
  begin
    SuppressibleMsgBox('Versi EVSEQ yang terpasang (' + PreviousVersion + ') lebih baru dari installer ini ({#AppVersion}). Instalasi dibatalkan.',
      mbError, MB_OK, IDOK);
    Result := False;
    Exit;
  end;

  { EVSEQ must be closed. When started from the app it is just exiting, so wait a moment. }
  Tries := 0;
  while CheckForMutexes(AppMutexName) do
  begin
    if WizardSilent() then
    begin
      if Tries >= 60 then begin Result := False; Exit; end;
      Sleep(250);
      Tries := Tries + 1;
    end
    else if MsgBox('EVSEQ masih berjalan. Simpan project dan tutup EVSEQ, lalu klik OK untuk melanjutkan.',
                   mbInformation, MB_OKCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

procedure InitializeWizard();
begin
  if IsUpgrade() then
    WizardForm.Caption := 'Update {#AppName} ' + PreviousVersion + ' -> {#AppVersion}';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  { Settings chosen at first install (folder, Start Menu, desktop icon) are kept on update. }
  Result := IsUpgrade() and ((PageID = wpLicense) or (PageID = wpSelectDir)
    or (PageID = wpSelectProgramGroup) or (PageID = wpSelectTasks));
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpReady) and IsUpgrade() then
    WizardForm.ReadyLabel.Caption := 'EVSEQ akan diperbarui dari versi ' + PreviousVersion + ' ke {#AppVersion}.' + #13#10 + #13#10 +
      'Project, daftar project terakhir, layout panel, dan pengaturan tidak berubah.';
end;

function RelaunchAfterUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:relaunch|0}') = '1';
end;

[UninstallDelete]
; autosave, session lock, recent list and panel layout; the user's .approj project files are NOT touched
Type: filesandordirs; Name: "{localappdata}\EVSEQ"
Type: filesandordirs; Name: "{localappdata}\AudioPlayer"
