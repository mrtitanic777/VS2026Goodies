# build-release.ps1 — produce the downloadable "no-compile" release asset (a .zip).
#
# The asset contains only original, pre-built code: a .vsix with the three Microsoft-owned
# parts STRIPPED OUT (interop, property pages, template starter), plus install.ps1 +
# Find-Xdk.ps1, which materialize those from the end user's own XDK at install time.
#
# Run order for cutting a release (needs your own XDK present):
#   .\setup.ps1                 # pull MS bits locally so the full VSIX can be built
#   .\extension\build-vsix.ps1  # compile + package the full VSIX
#   .\release\build-release.ps1 # strip MS bits -> clean .vsix -> zip the release
$ErrorActionPreference = "Stop"
$root  = Split-Path $PSScriptRoot -Parent
$here  = $PSScriptRoot
Add-Type -AssemblyName System.IO.Compression.FileSystem

$fullVsix = "$root\extension\Xbox360Package\bin\Release\Xbox360Package.vsix"
if (-not (Test-Path $fullVsix)) { Write-Error "Build the VSIX first: .\extension\build-vsix.ps1"; exit 1 }

# Version from the manifest (for the asset name).
$ver = ([regex]'Identity Id="[^"]+" Version="([0-9.]+)"').Match(
        (Get-Content "$root\extension\Xbox360Package\source.extension.vsixmanifest" -Raw)).Groups[1].Value
if (-not $ver) { $ver = "0.0.0" }

# 1. Clean .vsix = full .vsix minus the three Microsoft-owned parts.
$clean = "$here\Xbox360Package.clean.vsix"
Copy-Item $fullVsix $clean -Force
$zip = [System.IO.Compression.ZipFile]::Open($clean, 'Update')
$rm = @($zip.Entries | Where-Object {
        $_.FullName -match '/1033/' -or
        $_.FullName -match 'Interop\.XDevkit\.dll$' -or
        $_.FullName -match 'Xbox360Application/TEST\.cpp$' })
foreach ($e in $rm) { $e.Delete() }
$zip.Dispose()
Write-Host "clean .vsix: stripped $($rm.Count) Microsoft-owned entries" -ForegroundColor DarkGray

# 1b. Verify the clean .vsix really carries no Microsoft-owned parts.
$zc = [System.IO.Compression.ZipFile]::OpenRead($clean)
$leak = @($zc.Entries | Where-Object { $_.FullName -match '1033|Interop\.XDevkit|TEST\.cpp' })
$zc.Dispose()
if ($leak) { Write-Error ("clean .vsix still contains: " + ($leak.FullName -join ', ')); exit 1 }

# 2. Stage the release payload.
$stage = "$here\_stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item $clean            "$stage\Xbox360Package.clean.vsix" -Force
Copy-Item "$here\install.ps1" "$stage\install.ps1" -Force
Copy-Item "$root\Find-Xdk.ps1" "$stage\Find-Xdk.ps1" -Force
Copy-Item "$here\README.txt"  "$stage\README.txt" -Force

# 3. Zip it.
$asset = "$here\Xbox360-VS-Extension-v$ver.zip"
if (Test-Path $asset) { Remove-Item $asset -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $asset)
Remove-Item $stage -Recurse -Force
Write-Host ("release asset -> {0} ({1:N0} bytes)" -f $asset, (Get-Item $asset).Length) -ForegroundColor Green
