# install-platform.ps1 (bundled in the VSIX) — copies the Xbox 360 platform into every
# installed VS's VC toolset tree. Runs elevated. Self-contained; $PSScriptRoot is the
# bundled Platform\ folder with "Xbox 360\" beside it.
#
# Robust by design for non-technical end users:
#  - logs everything to %TEMP%\xbox360-platform-install.log
#  - verifies each copy (Platform.targets present afterward)
#  - treats "no VS toolsets found" and "zero installs" as FAILURES (non-zero exit)
#  - exit codes: 0 = success, 2 = bundle missing, 3 = no VS/toolsets, 4 = verify failed
$ErrorActionPreference = "Stop"
$log = Join-Path $env:TEMP "xbox360-platform-install.log"
function Log($m) { $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $m; Write-Host $line; Add-Content -Path $log -Value $line }
"=== Xbox 360 platform install $(Get-Date -Format o) ===" | Set-Content $log

try {
    $src = Join-Path $PSScriptRoot "Xbox 360"
    if (-not (Test-Path $src)) { Log "ERROR: bundled platform not found: $src"; exit 2 }
    Log "source: $src"

    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $installs = if (Test-Path $vswhere) { & $vswhere -all -prerelease -products * -format value -property installationPath } else { @() }
    Log ("VS instances found: {0}" -f (($installs | Measure-Object).Count))

    $count = 0; $failed = 0
    foreach ($vs in $installs) {
        $vc = "$vs\MSBuild\Microsoft\VC"
        if (-not (Test-Path $vc)) { continue }
        foreach ($ts in (Get-ChildItem $vc -Directory -Filter "v1*")) {
            $platforms = "$($ts.FullName)\Platforms"
            if (-not (Test-Path $platforms)) { continue }
            $dest = "$platforms\Xbox 360"
            $backup = "$platforms\Xbox 360.backup"
            try {
                # preserve a pre-existing (e.g. genuine Microsoft) platform once; overwrite our own updates
                if ((Test-Path $dest) -and -not (Test-Path $backup)) { Rename-Item $dest "Xbox 360.backup" -Force }
                elseif (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
                Copy-Item $src $dest -Recurse -Force
                if (Test-Path (Join-Path $dest "Platform.targets")) { Log "OK  -> $dest"; $count++ }
                else { Log "VERIFY FAILED -> $dest (Platform.targets missing after copy)"; $failed++ }
            } catch { Log "ERROR copying to ${dest}: $($_.Exception.Message)"; $failed++ }
        }
    }

    if ($count -eq 0) {
        Log "ERROR: installed into 0 toolset trees. Is Visual Studio 2022/2026 with the C++ workload installed?"
        exit 3
    }
    if ($failed -gt 0) { Log "WARNING: $failed target(s) failed to verify, but $count succeeded." ; exit 4 }
    Log "SUCCESS: installed into $count toolset tree(s)."
    exit 0
}
catch {
    Log "FATAL: $($_.Exception.Message)"
    exit 1
}
