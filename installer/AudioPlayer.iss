#define AppName "Audio Player"
#define AppVersion "0.1.0"
#define AppExe "AudioPlayer.exe"

[Setup]
AppId={{8F3C2A51-6B7D-4E2A-9C1F-3D5E7A9B0C12}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=AudioPlayer-Setup-{#AppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
; Code signing (F8) — enable once a certificate is available:
; SignTool=signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f "cert.pfx" /p "$PASSWORD" $f
; SignedUninstaller=yes

[Tasks]
Name: "desktopicon"; Description: "Buat ikon di desktop"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Jalankan {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; autosave & session lock; the user's .approj project files are NOT touched
Type: filesandordirs; Name: "{localappdata}\AudioPlayer"
