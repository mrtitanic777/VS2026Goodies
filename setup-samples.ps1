# setup-samples.ps1 — copy the XDK sample projects + shared media into test\ so the
# real-sample builds work without committing 600MB of XDK content to the repo.
param([string]$Xedk)
if (-not $Xedk) { $Xedk = & "$PSScriptRoot\Find-Xdk.ps1" }
$src = "$Xedk\Source\Samples"
$dst = "$PSScriptRoot\test"
foreach ($p in @("Common","Media","System\Gamepad")) {
    $s = "$src\$p"; $d = "$dst\$p"
    if (-not (Test-Path $s)) { Write-Warning "missing: $s"; continue }
    New-Item -ItemType Directory -Force (Split-Path $d) | Out-Null
    Copy-Item $s $d -Recurse -Force
    Write-Host "copied $p" -ForegroundColor Green
}
