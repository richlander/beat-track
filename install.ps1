# Install script for beat-track.
# Usage: irm https://raw.githubusercontent.com/richlander/beat-track/main/install.ps1 | iex
#
# Downloads a pre-built Native AOT binary from GitHub Releases
# and places it in ~/.local/bin/.
#
# Environment variables:
#   BEAT_TRACK_FEED        Override the download base URL
#   BEAT_TRACK_INSTALL_DIR Override the install directory

$ErrorActionPreference = 'Stop'

$Feed = if ($env:BEAT_TRACK_FEED) { $env:BEAT_TRACK_FEED } else { 'https://github.com/richlander/beat-track/releases/download' }
$InstallDir = if ($env:BEAT_TRACK_INSTALL_DIR) { $env:BEAT_TRACK_INSTALL_DIR } else { Join-Path $HOME '.local' 'bin' }

$Version = '0.1.0'
$Rid = 'win-x64'

$Arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
if ($Arch -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
    $Rid = 'win-arm64'
}

$Url = "$Feed/v$Version/beat-track-$Version-$Rid.zip"
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "beat-track-install-$([System.Guid]::NewGuid().ToString('N').Substring(0,8))"

Write-Host "beat-track: downloading beat-track $Version ($Rid)" -ForegroundColor Cyan

New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
$Archive = Join-Path $TempDir 'beat-track.zip'

Invoke-WebRequest -Uri $Url -OutFile $Archive -UseBasicParsing

Expand-Archive -Path $Archive -DestinationPath $TempDir -Force

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item (Join-Path $TempDir 'beat-track.exe') (Join-Path $InstallDir 'beat-track.exe') -Force

Write-Host "beat-track: installed to $InstallDir\beat-track.exe" -ForegroundColor Cyan

# Check if install dir is on PATH
$PathDirs = $env:PATH -split ';'
if ($InstallDir -notin $PathDirs) {
    Write-Host ''
    Write-Host "beat-track: Add $InstallDir to your PATH:" -ForegroundColor Yellow
    Write-Host "  [Environment]::SetEnvironmentVariable('PATH', `"$InstallDir;`$env:PATH`", 'User')" -ForegroundColor Yellow
    Write-Host ''
}

# Clean up
Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue
