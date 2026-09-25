#define AppName "Event Sequencer"
#define AppShortName "EVSEQ"
#define AppVersion "0.2.0"
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
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} ({#AppShortName})
; App icon: add SetupIconFile=EVSEQ.ico once the logo is ready.
; Code signing (F8): enable once a certificate is available:
; SignTool=signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f "cert.pfx" /p "$PASSWORD" $f
; SignedUninstaller=yes

[Tasks]
Name: "desktopicon"; Description: "Buat ikon di desktop"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[InstallDelete]
; executable of the earlier "Audio Player" builds, replaced by EVSEQ.exe
Type: files; Name: "{app}\AudioPlayer.*"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Jalankan {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; autosave, session lock, recent list and panel layout; the user's .approj project files are NOT touched
Type: filesandordirs; Name: "{localappdata}\EVSEQ"
Type: filesandordirs; Name: "{localappdata}\AudioPlayer"
