# fetch-property-pages.ps1 — populate platform\Xbox 360\1033\ with the VS property-page
# schemas (cl.xml, link.xml, imagexex.xml, ...) from YOUR OWN installed Xbox 360 SDK.
#
# These .xml files are Microsoft's and are therefore NOT committed to this repo (they are
# git-ignored). They are copied here, on your machine, from the Xbox 360 MSBuild platform
# that the XDK's Visual Studio integration installed:
#
#   %ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\Platforms\Xbox 360\1033\
#
# The .props/.targets in platform\Xbox 360\ are original and drive the build; these .xml
# files only provide the IDE's project-property-page UI.
[CmdletBinding()]
param(
    [string]$Source = "${env:ProgramFiles(x86)}\MSBuild\Microsoft.Cpp\v4.0\Platforms\Xbox 360\1033"
)
$ErrorActionPreference = "Stop"

if (-not (Test-Path $Source)) {
    Write-Error @"
Could not find the Xbox 360 property pages at:
  $Source

These come from the Xbox 360 XDK's Visual Studio (2010-era) MSBuild integration. Install the
XDK's VS integration, or pass -Source <path to an 'Xbox 360\1033' folder> that contains
cl.xml / link.xml / imagexex.xml.
"@
    exit 1
}

$dest = Join-Path $PSScriptRoot "platform\Xbox 360\1033"
New-Item -ItemType Directory -Force $dest | Out-Null

$xml = Get-ChildItem $Source -Filter *.xml -File
if (-not $xml) { Write-Error "No .xml property pages found in $Source"; exit 1 }
foreach ($f in $xml) { Copy-Item $f.FullName (Join-Path $dest $f.Name) -Force }

Write-Host ("OK -> copied {0} property-page schema(s) into platform\Xbox 360\1033\" -f $xml.Count) -ForegroundColor Green
Write-Host "(these are Microsoft's and remain git-ignored — never committed)" -ForegroundColor DarkGray
