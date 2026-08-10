# generate-interop.ps1 — regenerate the XDevkit COM interop assembly from YOUR OWN installed
# Xbox 360 XDK. No Microsoft binary is redistributed by this repo: this script builds
# extension\Xbox360Package\libs\Interop.XDevkit.dll locally from the XDK's XDevkit.idl.
#
#   midl  %XEDK%\include\win32\XDevkit.idl  ->  XDevkit.tlb
#   tlbimp XDevkit.tlb                      ->  Interop.XDevkit.dll
#
# Requires: the Xbox 360 XDK, a VS2022/VS2026 C++ toolset (for midl + its preprocessor),
# and the .NET Framework tools (TlbImp.exe, ships with the Windows SDK).
[CmdletBinding()]
param([string]$Xedk)
$ErrorActionPreference = "Stop"
if (-not $Xedk) { $Xedk = & "$PSScriptRoot\Find-Xdk.ps1" }

$idl = Join-Path $Xedk "include\win32\XDevkit.idl"
if (-not (Test-Path $idl)) { Write-Error "XDevkit.idl not found under '$Xedk'. Install the Xbox 360 XDK or pass -Xedk <path>."; exit 1 }

# Locate a VS install with the C++ tools (provides vcvars64 -> midl + preprocessor + Windows SDK includes).
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = if (Test-Path $vswhere) {
    & $vswhere -all -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
               -sort -property installationPath 2>$null | Select-Object -First 1
}
if (-not $vs) { Write-Error "No Visual Studio C++ toolset found (needed for midl). Install the 'Desktop development with C++' workload."; exit 1 }
$vcvars = "$vs\VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path $vcvars)) { Write-Error "vcvars64.bat not found at $vcvars"; exit 1 }

# Locate TlbImp.exe (prefer the newest NETFX tools version, e.g. 4.8 over 4.0 — a plain
# string sort would wrongly rank "v7.0A" above "v10.0A", so sort by the parsed NETFX version).
$tlbimp = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows" -Recurse -Filter TlbImp.exe -ErrorAction SilentlyContinue |
          Where-Object { $_.FullName -notmatch '\\x64\\' } |
          Sort-Object { if ($_.FullName -match 'NETFX (\d+)\.(\d+)') { [version]"$($Matches[1]).$($Matches[2])" } else { [version]'0.0' } } -Descending |
          Select-Object -First 1
if (-not $tlbimp) { Write-Error "TlbImp.exe not found (install the .NET Framework SDK / Windows SDK)."; exit 1 }

$outDir = Join-Path $PSScriptRoot "extension\Xbox360Package\libs"
New-Item -ItemType Directory -Force $outDir | Out-Null
$tlb    = Join-Path $env:TEMP "XDevkit.tlb"
$interop= Join-Path $outDir "Interop.XDevkit.dll"

Write-Host "==> midl: XDevkit.idl -> XDevkit.tlb" -ForegroundColor Cyan
$inc = Join-Path $Xedk "include\win32"
# Run midl in a scratch dir: besides the .tlb, midl emits *_i.c/_p.c/dlldata.c/.h stubs into the
# working directory, and we don't want those littering the repo.
$work = Join-Path $env:TEMP "xdk-interop-gen"
New-Item -ItemType Directory -Force $work | Out-Null
$midlCmd = "call `"$vcvars`" >nul 2>&1 && cd /d `"$work`" && midl /nologo /I `"$inc`" /tlb `"$tlb`" `"$idl`""
$midlOut = & cmd /c "$midlCmd 2>&1"
if (-not (Test-Path $tlb)) { $midlOut | Write-Host; Write-Error "midl failed to produce XDevkit.tlb"; exit 1 }

Write-Host "==> tlbimp: XDevkit.tlb -> Interop.XDevkit.dll" -ForegroundColor Cyan
& $tlbimp.FullName $tlb /out:$interop /namespace:XDevkit /silent
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $interop)) { Write-Error "tlbimp failed"; exit 1 }
Remove-Item $tlb -ErrorAction SilentlyContinue

Write-Host ("OK -> {0} ({1:N0} bytes)" -f $interop, (Get-Item $interop).Length) -ForegroundColor Green
