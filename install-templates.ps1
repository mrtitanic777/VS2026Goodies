# install-templates.ps1 — package the Xbox 360 project templates and install them into
# Visual Studio's user template folder so they appear in "Create a new project".
# No elevation needed (writes under your Documents folder).
param(
    [string]$VsDocs = "$env:USERPROFILE\Documents\Visual Studio 18"
)
$ErrorActionPreference = "Stop"
$dst = "$VsDocs\Templates\ProjectTemplates"
New-Item -ItemType Directory -Force $dst | Out-Null

$templates = Get-ChildItem "$PSScriptRoot\templates" -Directory
foreach ($t in $templates) {
    $zip = Join-Path $dst "$($t.Name).zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path "$($t.FullName)\*" -DestinationPath $zip -Force
    Write-Host "installed template -> $zip" -ForegroundColor Green
}
Write-Host ""
Write-Host "Restart Visual Studio 2026, then Create a new project and search 'Xbox 360'." -ForegroundColor Cyan
Write-Host "(The 'Xbox 360' platform must also be installed via install.ps1 for the project to load.)" -ForegroundColor DarkGray
