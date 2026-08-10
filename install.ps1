# install.ps1 — install (or remove) the Xbox 360 platform into EVERY installed
# Visual Studio (2017+) on this machine, across all their VC toolset trees.
# Discovers installs via vswhere, so it is not locked to VS2026.
#
# Must run ELEVATED (writes under Program Files). From an elevated PowerShell:
#   .\install.ps1            # install into all discovered VS installs / toolsets
#   .\install.ps1 -Uninstall # remove, restoring any backups
#   .\install.ps1 -Toolsets v170,v180   # limit to specific VCTargets versions
[CmdletBinding()]
param(
    [switch]$Uninstall,
    [string[]]$Toolsets                       # empty => every v1xx tree found
)
$ErrorActionPreference = "Stop"

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
         ).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $admin) { Write-Error "Run this as Administrator (it writes under Program Files)."; exit 1 }

$platSrc = "$PSScriptRoot\platform\Xbox 360"
if (-not (Test-Path $platSrc)) { Write-Error "Platform source not found: $platSrc"; exit 1 }

# Discover all VS installs (2017+) via vswhere.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$installPaths = @()
if (Test-Path $vswhere) {
    $installPaths = & $vswhere -all -prerelease -products * -format value -property installationPath 2>$null
}
if (-not $installPaths) { Write-Error "No Visual Studio installations found via vswhere."; exit 1 }

$installedCount = 0
foreach ($vs in $installPaths) {
    $vcRoot = "$vs\MSBuild\Microsoft\VC"
    if (-not (Test-Path $vcRoot)) { continue }
    Write-Host "VS: $vs" -ForegroundColor Cyan

    $tsDirs = Get-ChildItem $vcRoot -Directory -Filter "v1*" | Select-Object -Expand Name
    if ($Toolsets) { $tsDirs = $tsDirs | Where-Object { $Toolsets -contains $_ } }

    foreach ($ts in $tsDirs) {
        $platformsDir = "$vcRoot\$ts\Platforms"
        if (-not (Test-Path $platformsDir)) { continue }
        $dest   = "$platformsDir\Xbox 360"
        $backup = "$platformsDir\Xbox 360.backup"

        if ($Uninstall) {
            if (Test-Path $dest)   { Remove-Item $dest -Recurse -Force; Write-Host "  [$ts] removed" -ForegroundColor Yellow }
            if (Test-Path $backup) { Rename-Item $backup "Xbox 360"; Write-Host "  [$ts] restored backup" -ForegroundColor Yellow }
            continue
        }

        if ((Test-Path $dest) -and -not (Test-Path $backup)) { Rename-Item $dest "Xbox 360.backup" }
        elseif (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
        Copy-Item $platSrc $dest -Recurse -Force
        Write-Host "  [$ts] installed -> $dest" -ForegroundColor Green
        $installedCount++
    }
}

Write-Host ""
if ($Uninstall) { Write-Host "Uninstall complete." -ForegroundColor Cyan }
else { Write-Host "Installed into $installedCount toolset tree(s). Restart Visual Studio to pick it up." -ForegroundColor Cyan }
