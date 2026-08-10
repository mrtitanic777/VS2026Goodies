# build-vsix.ps1 — package the project templates into the VSIX and build it.
# Produces extension\Xbox360Package\bin\Release\Xbox360Package.vsix.
$ErrorActionPreference = "Stop"
$root     = Split-Path $PSScriptRoot -Parent
$pkgDir   = "$PSScriptRoot\Xbox360Package"
$tplOut   = "$pkgDir\ProjectTemplates"
# Discover MSBuild from any VS2022/VS2026 install (vswhere) rather than a hard-coded edition/path.
$vswhere  = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsInstall = if (Test-Path $vswhere) {
    & $vswhere -all -prerelease -products * -requires Microsoft.Component.MSBuild `
               -sort -property installationPath 2>$null | Select-Object -First 1
}
if (-not $vsInstall) { throw "No Visual Studio install with MSBuild found (via vswhere)." }
$msbuild  = "$vsInstall\MSBuild\Current\Bin\amd64\MSBuild.exe"

# 1. (Re)zip each project template into ProjectTemplates\<Name>.zip
New-Item -ItemType Directory -Force $tplOut | Out-Null
foreach ($t in Get-ChildItem "$root\templates" -Directory) {
    $zip = "$tplOut\$($t.Name).zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path "$($t.FullName)\*" -DestinationPath $zip -Force
    Write-Host "packed template: $($t.Name).zip" -ForegroundColor DarkGray
}

# 1b. Stage the platform (+ modernized build DLL) into PlatformPayload\Xbox 360
$payload = "$pkgDir\PlatformPayload\Xbox 360"
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory -Force $payload | Out-Null
Copy-Item "$root\platform\Xbox 360\*" $payload -Recurse -Force
# Stamp the platform with the VSIX version so the extension can detect a STALE installed
# platform and auto-update it (bumping the manifest version now propagates to end users).
$manifestVer = ([regex]'Identity Id="[^"]+" Version="([0-9.]+)"').Match((Get-Content "$pkgDir\source.extension.vsixmanifest" -Raw)).Groups[1].Value
Set-Content -Path "$payload\platform.version" -Value $manifestVer -NoNewline
Write-Host "staged platform payload (version $manifestVer)" -ForegroundColor DarkGray

# 1c. NOTE: the raw-XBDM AD7 engine (debug-engine/) is intentionally NOT built or shipped any more.
#     The XDevkit path (XdkHost) superseded it and is hardware-proven; shipping+registering an AD7
#     engine we don't drive would advertise a debugger to VS that can't service it. Source kept in
#     the repo for future source-level-breakpoint work.
$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"; if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
$env:DOTNET_ROLL_FORWARD = "Major"

# 1d. Build + stage the 32-bit XDevkit host. MUST be x86: XDevkit's typelib describes 32-bit struct
#     layouts, so XBOX_EVENT_INFO/XBOX_PROCESS_INFO access-violate in a 64-bit process (verified on
#     real hardware) — VS is x64, so all XDevkit COM runs out-of-process in this host.
& $dotnet build "$PSScriptRoot\XdkHost\Xbox360.XdkHost.csproj" -c Release -v quiet --nologo
if ($LASTEXITCODE -ne 0) { throw "XdkHost build failed" }
$hostStage = "$pkgDir\XdkHostPayload"
if (Test-Path $hostStage) { Remove-Item $hostStage -Recurse -Force }
New-Item -ItemType Directory -Force $hostStage | Out-Null
$hostBin = "$PSScriptRoot\XdkHost\bin\Release\net472"
Copy-Item "$hostBin\Xbox360.XdkHost.exe" $hostStage -Force
Copy-Item "$hostBin\Interop.XDevkit.dll" $hostStage -Force
Copy-Item "$hostBin\Xbox360.XdkHost.exe.config" $hostStage -Force -ErrorAction SilentlyContinue
Write-Host "staged 32-bit XDevkit host (Xbox360.XdkHost.exe + interop)" -ForegroundColor DarkGray

# 2. Build the VSIX
& $msbuild "$pkgDir\Xbox360Package.csproj" /t:Restore /v:quiet /nologo
& $msbuild "$pkgDir\Xbox360Package.csproj" /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "VSIX build failed" }

$vsix = "$pkgDir\bin\Release\Xbox360Package.vsix"
if (Test-Path $vsix) { Write-Host ("VSIX -> {0} ({1:N0} bytes)" -f $vsix, (Get-Item $vsix).Length) -ForegroundColor Green }
else { throw "VSIX not produced" }
