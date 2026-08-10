# setup.ps1 — one-shot local setup. Run this once after cloning, before building.
# It materializes the Microsoft-owned pieces that this repo does NOT ship, from YOUR OWN
# installed Xbox 360 XDK / SDK:
#   1. extension\Xbox360Package\libs\Interop.XDevkit.dll   (generate-interop.ps1)
#   2. platform\Xbox 360\1033\*.xml  property pages         (fetch-property-pages.ps1)
#   3. templates\Xbox360Application\TEST.cpp starter         (fetch-template.ps1)
# All outputs are git-ignored, so no Microsoft material is ever committed.
[CmdletBinding()]
param([string]$Xedk)
$ErrorActionPreference = "Stop"
Write-Host "== Xbox 360 VS extension — local setup ==" -ForegroundColor Cyan
# Resolve the XDK once (bulletproof locator) and pass it to each step.
if (-not $Xedk) { $Xedk = & "$PSScriptRoot\Find-Xdk.ps1" }
Write-Host "Using XDK: $Xedk" -ForegroundColor DarkGray
& "$PSScriptRoot\generate-interop.ps1"     -Xedk $Xedk
& "$PSScriptRoot\fetch-property-pages.ps1"
& "$PSScriptRoot\fetch-template.ps1"        -Xedk $Xedk
Write-Host ""
Write-Host "Setup complete. You can now build:  .\extension\build-vsix.ps1" -ForegroundColor Green
