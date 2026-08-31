# Xbox 360 XDK → Visual Studio 2026 Integration

Goal: Full Xbox 360 title development inside Visual Studio 2026 (VS "18") — build,
IntelliSense, deploy, run, and (stretch) live debugging — targeting a physical dev kit.

## Confirmed environment (2026-07-07)

| Fact | Value |
|------|-------|
| XDK version | `21256` (`_XDK_VER`), one of the last Xbox 360 XDKs |
| XDK root (`$XEDK`) | `C:\Program Files (x86)\Microsoft Xbox 360 SDK` |
| Compiler toolchain | `bin/win32/cl.exe`, `link.exe`, `lib.exe` (Xenon PPC MSVC) |
| Packaging / deploy | `bin/win32/imagexex.exe`, `xbcp.exe`, `make100.exe` |
| Headers / libs | `include/xbox`, `lib/xbox` |
| Visual Studio | VS 18 Community **18.6.0** at `C:\Program Files\Microsoft Visual Studio\18\Community` |
| Target toolset dir | `...\MSBuild\Microsoft\VC\v170\Platforms\` (v180 also present) |
| Hardware | Physical dev kit available (XBDM reachable — to be verified) |

## The key asset

The **original VS2010-era Xbox 360 MSBuild platform** survives intact at:
`C:\Program Files (x86)\MSBuild\Microsoft.Cpp\v4.0\Platforms\Xbox 360\` (26 files).
Copied to `reference/Xbox 360-v4.0-original/`. These files ALREADY use the
`$(VCTargetsPath)\Platforms\Xbox 360\...` layout that modern VS uses — so the port is
"relocate + adapt", not "invent".

Platform string is `Xbox 360` (with the space). Sample projects import only the STOCK
`Microsoft.Cpp.{Default.props,props,targets}`; all Xbox specifics come from the platform dir.

Canonical build settings (from `Source/Samples/.../AdvancedLighting2010.vcxproj`):
- ConfigurationType `Application`; defines `_XBOX;XBOX`; no exceptions; no RTTI; `/MP`.
- Configs: Debug / Release / Release_LTCG / Profile / Profile_FastCap / CodeAnalysis.
- Debug libs: `d3d9d d3dx9d xgraphicsd xapilibd xaudiod2 x3daudiod xmcored xboxkrnl xbdm`.
- Build chain: cl → link → **imagexex** (make the `.xex`) → **Deploy** (`xe:\`/`devkit:\`).
- `DebuggerFlavor = Xbox360Debugger`, remote root `devkit:\$(SolutionName)`.

## Phases

- [x] **Phase 0 — Recon & workspace.** Version, toolchain, VS, reference platform located. DONE.
- [~] **Phase 1 — Build integration (the port).**
      - [x] Ported platform to modern `v170` layout (`Platform.*.props/targets` + `Xenon` toolset),
            driving XDK tools through VS2026's stock CL/Link tasks + Exec-based imagexex.
      - [x] `HelloXbox` compiles → links → packages to a valid **XEX2** via `MSBuild.exe` CLI
            (see `build-harness.ps1`). Key fixes: default ClCompile metadata; `LinkCompiled` via
            `Microsoft.Cpp.Common.props`; `NonCoreWin=true` to drop Win32 libs; manifests off;
            suppress ASLR/DEP/SAFESEH switches for `/SUBSYSTEM:XBOX`; match debug CRT libs.
      - [ ] Install into VS2026 proper (`install.ps1`, elevated) and confirm the platform builds
            **from the IDE**, appears in Configuration Manager, and IntelliSense resolves `xtl.h`.
      - [ ] Polish: silence benign `D9002` modern-switch cl warnings; trace stray `pwsh.exe`
            message (environmental, non-fatal); wire real debug info once base is stable.
- [~] **Phase 2 — Templates & IDE UX.**
      - [x] Project template `templates/Xbox360Application/` (`.vstemplate` + templatized vcxproj +
            main.cpp). Instantiated + verified: builds valid XEX2 in Debug AND Release. `install-templates.ps1`
            packages it into `Documents\Visual Studio 18\Templates\ProjectTemplates`.
      - [x] Fixed two platform bugs found via the template: modern `/DEBUG:FULL` and (from
            PlatformTarget=x86) `/LTCG:incremental` both rejected by the XDK linker → remapped to
            plain `/DEBUG` and `/LTCG` in Platform.targets.
      - [ ] Add item templates (Add > New Item); verify property pages in the real IDE; add an
            "Xbox 360 (D3D)" starter and maybe an "Empty Project".

  Note — how VS2010's XDK set up the selector (learned from the install): a VSPackage
  "Xbox 360 Extensions for Visual Studio" registered project flavor GUID
  `{7C153452-B4EB-470e-9AD4-1419B778989C}` + the AD7 COM debug engine `X360DebugEnginev100.dll`
  (+UI) + `X360EmulationManagerV100.dll` + the build DLL; plus `SampleBrowser.exe` as the main
  "start from sample" entry. All `v100` DLLs are VS2010-only and don't load in VS2026, so we use
  modern `.vstemplate` files instead (a better New-Project experience than the original had).

- [ ] **RGH/JTAG (homebrew) deploy support.** For consoles the user owns, running homebrew.
      Transport: XBDM over network if DashLaunch's `xbdm` plugin is enabled (our deploy-run.ps1
      works as-is); otherwise FTP (Aurora/FSD/F3) — add an `-Mode FTP` path. Launch: xbreboot
      (XBDM) or dashboard/launcher (FTP). OPEN QUESTION to verify on hardware: whether XDK-built
      (devkit-signed) `.xex` run unmodified on RGH/JTAG, or need xextool-style handling.
- [ ] **Phase 3 — Deploy & Run.** One-click deploy (xbcp/XBDM) + launch to the dev kit from VS;
      set default console name; capture console debug output into VS.
- [~] **Multi-toolset support (not locked to VS2026).** Platform rides the stable `CppCommon`
      contract → same files work across `v150`–`v180`. Proven: `HelloXbox` builds a valid XEX2
      under v150/v160/v170/v180. `install.ps1` now discovers all VS installs via `vswhere` and
      installs into every VC toolset tree. Remaining: verify in each IDE.

- [~] **Build the real XDK samples (not hand-written scaffolding).**
      - [x] ATG framework static lib (`Common/AtgFramework2010.vcxproj`, 50 cpp) → `AtgFramework.lib`.
      - [x] `System/Gamepad` (Application) builds end-to-end → valid **7.7MB XEX2**: ATG project
            reference, `fxc` shaders, `bundler` resources, mesh copy all work. Use `setup-samples.ps1`
            to fetch XDK content into `test/` (gitignored). Only Debug config tested so far.
      - [ ] Verify the other five configs (Release/Release_LTCG/Profile/Profile_FastCap/CodeAnalysis)
            and a couple more samples (graphics-heavy) to shake out remaining metadata gaps.

- [~] **Reverse-engineer & modernize the XDK build DLL (`Microsoft.Xna.Xbox360.Build.dll`).**
      Scoped: only 6 classes. `CL`/`Link` subclass the *stock* MSVC `CPPTasks.CL`/`Link` and add
      Xbox switches; `ImageXex`(47 methods)/`Deploy`/`DeployToHardDrive` derive from
      `XboxToolTask : ToolTask`. Plan: decompile (ILSpy) → retarget base refs from VS2010
      `CPPTasks` v4.0.0.0 to current `Microsoft.Build.CppTasks.Common` → recompile **AnyCPU** so it
      loads in the 64-bit IDE. Payoff: file-tracked incremental builds, first-class Xbox cl
      switches, full `.xex` option surface, integrated deploy. Bounded, not a black-box rewrite.
      DONE: ported to `dll-modernization/src/` → `Microsoft.Xbox360.Build.dll` (net472, **AnyCPU**,
      references current CppTasks.Common v17.14). Only 2 API-drift fixes needed (logPrivate→Log;
      drop ToolSwitch.MultiValues). Verified it LOADS + RUNS in 64-bit MSBuild: HelloXbox and full
      Gamepad build to valid XEX2 through the modernized CL/Link/ImageXex tasks (wired via UsingTask
      in Platform.targets). Rebuild with `dll-modernization/build-dll.ps1`.
      Remaining polish: add the Xbox cl/link/imagexex properties as first-class property-page
      metadata (`cl.xml`/`link.xml`/`imagexex.xml`); wire DeployToHardDrive into a Deploy target;
      confirm incremental no-op rebuilds.

- [~] **"Xbox 360 Extensions for VS 2026" VSIX** — modern re-creation of the VS2010 VSPackage.
      Design in `extension/DESIGN.md`; manifest scaffolded in `extension/Xbox360Package/`. Scoped:
      VSSDK workload installed → buildable; templates ship as a native VSIX asset; SampleBrowser.exe
      is native x86 (runs under WOW64) → wire as a Tools-menu command; Deploy & Run command wraps
      xbcp/xbreboot (devkit + RGH/JTAG). The native x86 AD7 engine (`X360DebugEnginev100.dll`) is NOT
      reusable (native, no source, won't load in 64-bit VS) — live debugging needs a from-scratch
      managed AD7/XBDM engine. `xbdm.dll` exports the client API (`DmConnectDebugger`, `DmSetBreakpoint`,
      `DmGetThreadList`, `DmReboot`, ...); the XBDM wire protocol (TCP:730) can be driven in managed code.
      - [x] VSIX BUILDS (legacy VSSDK csproj + Microsoft.VSSDK.BuildTools 18.5 + VisualStudio.SDK,
            built with VS2026 MSBuild). `extension/build-vsix.ps1` → `Xbox360Package.vsix` (59KB).
      - [x] Commands: Sample Browser (launch exe), Deploy & Run (xbcp/xbreboot + .xex picker),
            Set Default Console (registry), Install/Update Platform (elevated, vswhere → all toolsets).
      - [x] Bundled the platform + modernized build DLL + property pages + elevated installer INTO
            the VSIX. One .vsix delivers templates + commands + platform. Verified 29 entries.
      - [ ] Not yet tested by installing into a live VS2026 (needs user). Add FTP deploy for RGH/JTAG.

- [~] **Phase 4 — Debugging (R&D).** A NEW managed AD7/XBDM engine (`debug-engine/`), structurally
      COMPLETE and compiling (~1300 LOC, AnyCPU, net472):
      - [x] `Xbox360.Xbdm`: transport (TCP:730 framing) + debug commands (threads/modules/memory/
            breakpoints/continue, grounded in xbdm.dll formats) + async notification listener. All
            mock-tested (transport, commands, notifications PASS).
      - [x] `Xbox360.DebugEngine`: AD7 engine core + launch handshake + event dispatch; 11 AD7 events;
            Thread/Process/Program + enumerators; breakpoints -> XBDM BREAK; port supplier; memory ->
            GETMEM/SETMEM; stack frame; notification->AD7-event translation; registration pkgdef.
      - [ ] Symbol reader (source line <-> PPC address, PC from GETCONTEXT) — the hard remaining piece.
      - [ ] Full PPC stack walking; locals/watch (need symbols); wire real console connect in launch/attach.
      - [ ] Bundle engine into the VSIX + register; F5-validate on a real console. AD7/Concord debug engine speaking XBDM
      (port 730) for breakpoints/stepping/watch. The old `Xbox360Debugger` VSPackage targets the
      VS2010 debugger arch and will NOT load in VS2026. Fallback: standalone xbWatson crash dumps
      + output capture if a full engine proves infeasible.

## Dev/test workflow

Develop the ported platform under `platform/Xbox 360/` in this repo. Installing into
`C:\Program Files\...\VS\18\...` needs elevation → use an `install.ps1` (admin) that copies
`platform/Xbox 360/` into `v170\Platforms\`. For fast iteration, test via `msbuild.exe` with an
override before touching Program Files. Keep everything in git.

## Open risks / unknowns

1. Modern `Microsoft.Cpp.Default.props` resolves `$(VCTargetsPath)` from the toolset — getting a
   fully custom platform recognized (dropdown + load) may need an AdditionalVCTargetsPath / a
   platform-toolset shim. Primary Phase 1 engineering risk.
2. Property-page rule `.xml` schema evolved since VS2010 — rules may need updating to load.
3. XDK `cl.exe` is old-MSVC; confirm it runs standalone under current OS with correct env
   (`INCLUDE`/`LIB`/`PATH` = `$(XEDK)\include\xbox`, `$(XEDK)\lib\xbox`, `$(XEDK)\bin\win32`).
4. Phase 4 debugger is genuine R&D; treat as separable from a working Phases 1–3 product.
