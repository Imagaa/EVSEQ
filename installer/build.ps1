# Test, publish self-contained, and build the installer. Run from anywhere.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot

dotnet test "$root" -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }

dotnet publish "$root\src\AudioPlayer.App" -c Release -r win-x64 --self-contained true -o "$root\artifacts\publish"
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "ISCC.exe not found. Install: winget install --id JRSoftware.InnoSetup -e" }

& $iscc "$PSScriptRoot\AudioPlayer.iss"
if ($LASTEXITCODE) { exit $LASTEXITCODE }
