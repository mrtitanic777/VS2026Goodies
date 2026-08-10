# Find-Xdk.ps1 — locate the installed Xbox 360 XDK, wherever it is. Writes the resolved root
# path to stdout, or errors out. Other setup scripts call this so none of them hard-code a path.
#
#   $xedk = & "$PSScriptRoot\Find-Xdk.ps1"          # auto-detect
#   $xedk = & "$PSScriptRoot\Find-Xdk.ps1" -Path D:\XDK   # explicit override
#
# Resolution order: -Path, %XEDK%, common install roots on every fixed drive, then a bounded
# recursive scan of each drive's Program Files trees for the SDK folder.
[CmdletBinding()]
param([string]$Path)
$ErrorActionPreference = "Stop"

# A directory is a valid XDK root if it has the packaging tool and the XDevkit IDL.
function Test-XdkRoot([string]$p) {
    if ([string]::IsNullOrWhiteSpace($p)) { return $false }
    return (Test-Path (Join-Path $p 'bin\win32\imagexex.exe')) -and
           (Test-Path (Join-Path $p 'include\win32\XDevkit.idl'))
}

# 1) explicit path, 2) environment variable, 3) known roots on every fixed drive.
$drives = @(Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue |
            Where-Object { $_.Root -match '^[A-Za-z]:\\$' } | Select-Object -Expand Root)
$known = @()
if ($Path)     { $known += $Path }
if ($env:XEDK) { $known += $env:XEDK }
foreach ($d in $drives) {
    $known += (Join-Path $d 'Program Files (x86)\Microsoft Xbox 360 SDK')
    $known += (Join-Path $d 'Program Files\Microsoft Xbox 360 SDK')
    $known += (Join-Path $d 'Microsoft Xbox 360 SDK')
    $known += (Join-Path $d 'XDK')
}
foreach ($k in $known) { if (Test-XdkRoot $k) { Write-Output ($k.TrimEnd('\')); exit 0 } }

# 4) bounded scan: look for a folder literally named "Microsoft Xbox 360 SDK" a few levels deep
#    under each drive's Program Files trees (covers non-standard but sane install locations).
foreach ($d in $drives) {
    foreach ($base in @('Program Files (x86)', 'Program Files', '')) {
        $root = if ($base) { Join-Path $d $base } else { $d }
        if (-not (Test-Path $root)) { continue }
        $hit = Get-ChildItem -Path $root -Directory -Filter 'Microsoft Xbox 360 SDK' `
                             -Recurse -Depth 3 -ErrorAction SilentlyContinue |
               Where-Object { Test-XdkRoot $_.FullName } | Select-Object -First 1
        if ($hit) { Write-Output ($hit.FullName.TrimEnd('\')); exit 0 }
    }
}

Write-Error @"
Could not locate the Xbox 360 XDK on this machine.
Fix one of:
  * set the XEDK environment variable to the SDK root, or
  * install the Xbox 360 XDK to the default location, or
  * pass -Path <XDK root> to setup.ps1.
A valid root contains bin\win32\imagexex.exe and include\win32\XDevkit.idl.
"@
exit 1
