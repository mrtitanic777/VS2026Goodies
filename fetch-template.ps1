# fetch-template.ps1 — populate the project template's starter source from YOUR OWN XDK.
# The Xbox 360 Application template renders the XDK's TriangleHLSL sample; that sample is
# Microsoft's and is NOT committed here. This copies it in locally (git-ignored) so the
# template is complete when you build the VSIX.
[CmdletBinding()]
param([string]$Xedk)
$ErrorActionPreference = "Stop"
if (-not $Xedk) { $Xedk = & "$PSScriptRoot\Find-Xdk.ps1" }

$sample = Join-Path $Xedk 'Source\Samples\Graphics\TriangleHLSL\TriangleHLSL.cpp'
if (-not (Test-Path $sample)) {
    Write-Error "TriangleHLSL sample not found in your XDK at:`n  $sample`nInstall the XDK samples, or pass -Xedk <XDK root>."
    exit 1
}

$dest = Join-Path $PSScriptRoot 'templates\Xbox360Application\TEST.cpp'
# The template project compiles with a precompiled header (stdafx.h), so make it the first
# include of the pulled source. (The original repo file lives nowhere — this is generated.)
$body = Get-Content $sample -Raw
Set-Content -Path $dest -Value ("#include `"stdafx.h`"`r`n" + $body) -NoNewline

Write-Host "OK -> template starter source pulled from your XDK's TriangleHLSL sample" -ForegroundColor Green
Write-Host "(Microsoft-owned; git-ignored, never committed)" -ForegroundColor DarkGray
