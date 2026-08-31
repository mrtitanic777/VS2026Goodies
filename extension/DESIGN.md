# Xbox 360 Extensions for Visual Studio 2026 (VSIX)

A modern re-creation of the VS2010 "Xbox 360 Extensions for Visual Studio" package,
delivered as a single VSIX. Replaces the manual install.ps1 / install-templates.ps1 steps.

## What VS2010 shipped (and our modern equivalent)

| VS2010 component | Nature | VS2026 approach |
|---|---|---|
| Project flavor `{7C153452-...}` | native VSPackage | MSBuild platform (done) + optional light package for deploy/F5 |
| `X360DebugEnginev100.dll` (+UI) | **native x86 COM AD7 engine** | **rewrite** — new managed AD7/XBDM engine (separate track) |
| `X360EmulationManagerV100.dll` | native | deploy plumbing via MSBuild Deploy target + xbecopy (defer) |
| `Microsoft.Xna.Xbox360.Build.dll` | .NET FX task DLL | ✅ modernized AnyCPU (done) |
| `SampleBrowser.exe` | native x86 app | runs under WOW64 → Tools-menu command that launches it |
| New Project experience | Sample Browser | native `.vstemplate` (done) → shipped as VSIX ProjectTemplate assets |

## VSIX assets (this package)

1. **ProjectTemplate** — `templates/Xbox360Application` (native VSIX asset; no more manual zip install).
2. **VsPackage** — a small managed VSPackage providing menu commands:
   - `Tools > Xbox 360 > Sample Browser` → launches `$(XEDK)\Source\Tools\SampleBrowser\SampleBrowser.exe`.
   - `Tools > Xbox 360 > Deploy & Run…` → runs the deploy-run flow (xbcp/xbreboot) for the active project.
   - `Tools > Xbox 360 > Set Default Console…` → writes `HKCU\...\XenonSDK\XboxName`.
3. **Platform delivery** — install the ported `Xbox 360` platform + modernized build DLL into the
   VC toolset tree. (VSIX can't drop into VCTargetsPath directly; use a VSIX install action or keep
   install.ps1 invoked once. Investigate the per-user VC platform-extension mechanism.)
4. **RGH/JTAG deploy mode** — Deploy & Run offers XBDM (devkit / DashLaunch-xbdm) or FTP (Aurora/FSD).

## Build
VSSDK workload is installed. Build via `Microsoft.VSSDK.BuildTools` (NuGet) + VS2026 MSBuild.
Target: `Microsoft.VisualStudio.Sdk` references; `source.extension.vsixmanifest` declares assets.

## Out of scope for v1
- Live F5 debugging (the AD7/XBDM engine) — tracked separately; the big R&D piece.
