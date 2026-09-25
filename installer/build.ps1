# Test, publish self-contained, and build the installer. Run from anywhere.
#   .\installer\build.ps1                 # version from the project files
#   .\installer\build.ps1 -Version 0.2.1  # what the release workflow does for tag v0.2.1
param([string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$publish = Join-Path $root 'artifacts\publish'

dotnet test "$root" -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }

# Start from an empty folder so files from older builds never end up in the installer.
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
[string[]]$versionArgs = if ($Version) { @("-p:Version=$Version") } else { @() }
# ReadyToRun: precompiled code, so the first start after a reboot does not wait for the JIT
dotnet publish "$root\src\AudioPlayer.App" -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o $publish @versionArgs
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "ISCC.exe not found. Install: winget install --id JRSoftware.InnoSetup -e" }

[string[]]$isccArgs = if ($Version) { @("/DAppVersion=$Version") } else { @() }
& $iscc @isccArgs "$PSScriptRoot\EVSEQ.iss"
if ($LASTEXITCODE) { exit $LASTEXITCODE }
