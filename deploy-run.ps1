# deploy-run.ps1 — mirror a built title to an Xbox 360 devkit and launch it.
#   .\deploy-run.ps1 -Console 192.168.1.50
#   .\deploy-run.ps1 -Console MyDevkit -LocalDir test\System\Gamepad\Debug -RemoteRoot "xe:\Gamepad" -Xex Gamepad.xex
param(
    [Parameter(Mandatory=$true)] [string]$Console,
    [string]$LocalDir  = "$PSScriptRoot\test\System\Gamepad\Debug",
    [string]$RemoteRoot = "xe:\Gamepad",
    [string]$Xex        = "Gamepad.xex",
    [switch]$NoLaunch
)
$ErrorActionPreference = "Stop"
$bin      = "C:\Program Files (x86)\Microsoft Xbox 360 SDK\bin\win32"
$xbcp     = "$bin\xbcp.exe"
$xbmkdir  = "$bin\xbmkdir.exe"
$xbreboot = "$bin\xbreboot.exe"
$xbtype   = "$bin\xbconsoletype.exe"

if (-not (Test-Path $LocalDir)) { throw "Local build dir not found: $LocalDir (build it first)" }

Write-Host "==> Checking devkit '$Console' ..." -ForegroundColor Cyan
& $xbtype /X:$Console /B
if ($LASTEXITCODE -ne 0) { throw "Cannot reach devkit '$Console'. Is it powered on, networked, and named/addressed correctly?" }

$root = (Resolve-Path $LocalDir).Path.TrimEnd('\')
$files = Get-ChildItem $root -Recurse -File

# Create the remote directory tree (ignore 'already exists').
$dirs = @($RemoteRoot) + ($files | ForEach-Object {
    $rel = $_.DirectoryName.Substring($root.Length).TrimStart('\')
    if ($rel) { "$RemoteRoot\$rel" } else { $RemoteRoot }
} | Sort-Object -Unique)
foreach ($d in ($dirs | Sort-Object -Unique)) {
    & $xbmkdir /X:$Console $d 2>$null | Out-Null
}

# Copy every file to its mirrored remote path.
$n = 0
foreach ($f in $files) {
    $rel = $f.FullName.Substring($root.Length).TrimStart('\')
    $remote = "$RemoteRoot\$rel"
    & $xbcp /X:$Console /Y $f.FullName $remote
    if ($LASTEXITCODE -ne 0) { throw "xbcp failed for $($f.FullName)" }
    $n++
}
Write-Host ("==> Copied {0} files to {1} on {2}" -f $n, $RemoteRoot, $Console) -ForegroundColor Green

if (-not $NoLaunch) {
    $target = "$RemoteRoot\$Xex"
    Write-Host "==> Launching $target ..." -ForegroundColor Cyan
    & $xbreboot /X:$Console $target
    if ($LASTEXITCODE -ne 0) { throw "xbreboot failed to launch $target" }
    Write-Host "==> Launched. Watch your devkit's display." -ForegroundColor Green
}
