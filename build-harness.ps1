# build-harness.ps1 — admin-free iteration loop for the Xbox 360 VS2026 platform.
# Copies VS2026's v170 VCTargets into a writable dir, drops our platform in, and
# builds the test project against it by overriding $(VCTargetsPath). No elevation,
# no touching Program Files.
[CmdletBinding()]
param(
    [string]$Project = "$PSScriptRoot\test\HelloXbox\HelloXbox.vcxproj",
    [string]$Toolset = "v170",              # which VCTargets tree to test against (v150..v180)
    [string]$Configuration = "Debug",
    [string]$Verbosity = "minimal",
    [switch]$Detailed
)
$ErrorActionPreference = "Stop"
if ($Detailed) { $Verbosity = "detailed" }

# Discover a Visual Studio install that has the requested VC toolset (vswhere, not a hard-coded
# edition/path) so this works on VS2022/VS2026 Community/Professional/Enterprise alike.
$vswhere  = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = $null
if (Test-Path $vswhere) {
    # Newest first; pick the first install that actually carries the requested VC toolset.
    $vs = & $vswhere -all -prerelease -products * -requires Microsoft.Component.MSBuild `
                     -sort -property installationPath 2>$null |
          Where-Object { Test-Path "$_\MSBuild\Microsoft\VC\$Toolset" } |
          Select-Object -First 1
}
if (-not $vs) { Write-Error "No Visual Studio install with toolset $Toolset found (via vswhere)."; exit 2 }
$srcVCT   = "$vs\MSBuild\Microsoft\VC\$Toolset"
$msbuild  = "$vs\MSBuild\Current\Bin\amd64\MSBuild.exe"
$harness  = "$PSScriptRoot\test-harness\VCTargets-$Toolset"
$platSrc  = "$PSScriptRoot\platform\Xbox 360"
if (-not (Test-Path $srcVCT)) { Write-Error "Toolset $Toolset not found at $srcVCT"; exit 2 }
Write-Host "==> Toolset: $Toolset   Config: $Configuration" -ForegroundColor Cyan

Write-Host "==> Refreshing writable VCTargets harness ..." -ForegroundColor Cyan
if (Test-Path $harness) { Remove-Item $harness -Recurse -Force }
New-Item -ItemType Directory -Path $harness -Force | Out-Null
Copy-Item "$srcVCT\*" $harness -Recurse -Force

Write-Host "==> Installing platform 'Xbox 360' into harness ..." -ForegroundColor Cyan
$destPlat = "$harness\Platforms\Xbox 360"
if (Test-Path $destPlat) { Remove-Item $destPlat -Recurse -Force }
Copy-Item $platSrc $destPlat -Recurse -Force

Write-Host "==> Building $Project" -ForegroundColor Cyan
& $msbuild $Project `
    /p:Configuration=$Configuration `
    /p:Platform="Xbox 360" `
    "/p:VCTargetsPath=$harness\" `
    /t:Rebuild `
    /v:$Verbosity `
    /nologo
$code = $LASTEXITCODE
Write-Host ""
Write-Host "==> MSBuild exit code: $code" -ForegroundColor (($code -eq 0) ? "Green" : "Red")
exit $code
