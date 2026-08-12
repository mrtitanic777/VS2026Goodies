# install.ps1 — set up the Xbox 360 Extensions for Visual Studio 2022/2026 WITHOUT compiling.
#
# This download contains only original, pre-built code and ships NO Microsoft SDK files.
# It completes the extension on YOUR machine, from YOUR OWN installed Xbox 360 XDK:
#   * XDevkit interop  (midl + tlbimp on your XDK's XDevkit.idl)
#   * IDE property pages (copied from the Xbox 360 platform your XDK's VS integration installed)
#   * the project template's starter source (your XDK's TriangleHLSL sample)
# ...then injects them into the .vsix and launches the Visual Studio installer.
#
# Requirements: Visual Studio 2022/2026 with "Desktop development with C++" (for midl + tlbimp),
# your own Xbox 360 XDK (+ its VS MSBuild integration for the property pages).
#
#   .\install.ps1              # build the completed .vsix and launch VSIXInstaller
#   .\install.ps1 -NoInstall   # just produce the completed .vsix, don't launch the installer
#   .\install.ps1 -Xedk D:\XDK # explicit XDK path
[CmdletBinding()]
param([string]$Xedk, [switch]$NoInstall)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
Add-Type -AssemblyName System.IO.Compression.FileSystem
Write-Host "== Xbox 360 VS extension — no-compile install ==" -ForegroundColor Cyan

# --- locate the XDK (bulletproof: env / known roots / bounded search) ---
if (-not $Xedk) { $Xedk = & "$here\Find-Xdk.ps1" }
Write-Host "Using XDK: $Xedk" -ForegroundColor DarkGray

$work = Join-Path $env:TEMP "x360-vsix-install"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force $work | Out-Null

# --- 1. XDevkit interop: midl XDevkit.idl -> tlbimp -> Interop.XDevkit.dll ---
$idl = Join-Path $Xedk 'include\win32\XDevkit.idl'
if (-not (Test-Path $idl)) { Write-Error "XDevkit.idl not found under '$Xedk'."; exit 1 }
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = if (Test-Path $vswhere) {
    & $vswhere -all -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
               -sort -property installationPath 2>$null | Select-Object -First 1
}
if (-not $vs) { Write-Error "No Visual Studio C++ toolset found (needed for midl)."; exit 1 }
$vcvars = "$vs\VC\Auxiliary\Build\vcvars64.bat"
$tlbimp = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows" -Recurse -Filter TlbImp.exe -ErrorAction SilentlyContinue |
          Where-Object { $_.FullName -notmatch '\\x64\\' } |
          Sort-Object { if ($_.FullName -match 'NETFX (\d+)\.(\d+)') { [version]"$($Matches[1]).$($Matches[2])" } else { [version]'0.0' } } -Descending |
          Select-Object -First 1
if (-not $tlbimp) { Write-Error "TlbImp.exe not found (install the .NET Framework SDK / Windows SDK)."; exit 1 }
Write-Host "==> generating XDevkit interop from your XDK ..." -ForegroundColor Cyan
$tlb = Join-Path $work 'XDevkit.tlb'
$interop = Join-Path $work 'Interop.XDevkit.dll'
$inc = Join-Path $Xedk 'include\win32'
& cmd /c "call `"$vcvars`" >nul 2>&1 && cd /d `"$work`" && midl /nologo /I `"$inc`" /tlb `"$tlb`" `"$idl`" 2>&1" | Out-Null
if (-not (Test-Path $tlb)) { Write-Error "midl failed to produce the type library."; exit 1 }
& $tlbimp.FullName $tlb /out:$interop /namespace:XDevkit /silent
if (-not (Test-Path $interop)) { Write-Error "tlbimp failed to produce Interop.XDevkit.dll."; exit 1 }

# --- 2. IDE property pages from your installed v4.0 Xbox 360 platform ---
$ppSrc = "${env:ProgramFiles(x86)}\MSBuild\Microsoft.Cpp\v4.0\Platforms\Xbox 360\1033"
if (-not (Test-Path $ppSrc)) { Write-Error "Xbox 360 property pages not found at:`n  $ppSrc`nInstall the XDK's Visual Studio MSBuild integration."; exit 1 }
$pp = @(Get-ChildItem $ppSrc -Filter *.xml -File)
Write-Host "==> collected $($pp.Count) property-page schemas from your XDK platform" -ForegroundColor Cyan

# --- 3. template starter from your XDK's TriangleHLSL sample ---
$tri = Join-Path $Xedk 'Source\Samples\Graphics\TriangleHLSL\TriangleHLSL.cpp'
if (-not (Test-Path $tri)) { Write-Error "TriangleHLSL sample not found at:`n  $tri"; exit 1 }
$testCpp = Join-Path $work 'TEST.cpp'
Set-Content -Path $testCpp -Value ("#include `"stdafx.h`"`r`n" + (Get-Content $tri -Raw)) -NoNewline

# --- 4. inject everything into a working copy of the clean .vsix ---
Write-Host "==> assembling the completed .vsix ..." -ForegroundColor Cyan
$vsix = Join-Path $work 'Xbox360Package.vsix'
Copy-Item "$here\Xbox360Package.clean.vsix" $vsix -Force
$zip = [System.IO.Compression.ZipFile]::Open($vsix, 'Update')
try {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $interop, 'XdkHostPayload/Interop.XDevkit.dll') | Out-Null
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $testCpp, 'ProjectTemplates/Xbox360Application/TEST.cpp') | Out-Null
    foreach ($x in $pp) {
        # OPC part names encode the space in "Xbox 360" as %20 — match the .vsix exactly.
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $x.FullName, "Platform/Xbox%20360/1033/$($x.Name)") | Out-Null
    }
} finally { $zip.Dispose() }

# --- 5. install ---
$out = Join-Path $here 'Xbox360Package.vsix'
Copy-Item $vsix $out -Force
Write-Host "OK -> completed extension: $out" -ForegroundColor Green
if ($NoInstall) { Write-Host "Double-click it to install, or re-run without -NoInstall." -ForegroundColor DarkGray; return }

$installer = Get-ChildItem "$vs\Common7\IDE\VSIXInstaller.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($installer) { Write-Host "==> launching VSIXInstaller ..." -ForegroundColor Cyan; & $installer.FullName $out }
else { Write-Host "VSIXInstaller not found; double-click $out to install." -ForegroundColor Yellow }
