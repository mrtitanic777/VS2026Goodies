# sign-vsix.ps1 — sign the built VSIX with a code-signing certificate.
# Uses Microsoft's 'sign' CLI (dotnet tool). Works with a cert in the Windows store
# (by SHA-1 thumbprint) OR a PFX file. Swap the self-signed test cert for a real
# CA/EV cert (or Azure Trusted Signing) for distribution — see README.
[CmdletBinding(DefaultParameterSetName = 'Store')]
param(
    [string]$VsixPath = "$PSScriptRoot\Xbox360Package\bin\Release\Xbox360Package.vsix",

    # --- Option A: a cert already in Cert:\CurrentUser\My (default: the self-signed test cert) ---
    [Parameter(ParameterSetName = 'Store')]
    [string]$Thumbprint = "D529123ADFB97269E7A15B974B1A7E06F0965147",

    # --- Option B: a PFX file (real distribution cert) ---
    [Parameter(ParameterSetName = 'Pfx', Mandatory = $true)]
    [string]$PfxPath,
    [Parameter(ParameterSetName = 'Pfx', Mandatory = $true)]
    [string]$PfxPassword,

    # RFC 3161 timestamp server — timestamping keeps the signature valid after the cert expires.
    [string]$TimestampUrl = "http://timestamp.acs.microsoft.com/",
    [string]$Description   = "Xbox 360 Extensions for Visual Studio"
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $VsixPath)) { throw "VSIX not found: $VsixPath (run build-vsix.ps1 first)" }

# 1. Ensure the 'sign' tool is available (installs into %USERPROFILE%\.dotnet\tools).
$sign = "$env:USERPROFILE\.dotnet\tools\sign.exe"
if (-not (Test-Path $sign)) {
    Write-Host "Installing 'sign' tool ..." -ForegroundColor Cyan
    $env:DOTNET_ROLL_FORWARD = "Major"
    & "$env:USERPROFILE\.dotnet\dotnet.exe" tool install --global sign --prerelease
    if (-not (Test-Path $sign)) { throw "'sign' tool install failed" }
}

# 2. Resolve the certificate's SHA-256 fingerprint (what 'sign' requires) + build args.
$signArgs = @("code", "certificate-store", (Split-Path $VsixPath -Leaf))
if ($PSCmdlet.ParameterSetName -eq 'Pfx') {
    $c = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $PfxPassword)
    $signArgs += @("-cfp", $c.GetCertHashString('SHA256'), "-cf", $PfxPath, "-p", $PfxPassword)
} else {
    $c = Get-Item "Cert:\CurrentUser\My\$Thumbprint"
    $signArgs += @("-cfp", $c.GetCertHashString('SHA256'))
}
$signArgs += @("-t", $TimestampUrl, "-d", $Description, "-b", (Split-Path $VsixPath -Parent), "-v", "Information")

Write-Host "Signing $VsixPath" -ForegroundColor Cyan
Write-Host "  with cert: $($c.Subject)  (expires $($c.NotAfter.ToShortDateString()))" -ForegroundColor DarkGray
& $sign @signArgs
if ($LASTEXITCODE -ne 0) { throw "signing failed ($LASTEXITCODE)" }
Write-Host "Signed OK -> $VsixPath" -ForegroundColor Green
