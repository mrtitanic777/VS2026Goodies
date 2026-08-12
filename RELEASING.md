# Cutting a release

The published release asset must contain **no Microsoft SDK material**. A normally
built `.vsix` *does* embed Microsoft's files (the XDevkit interop, the IDE property
pages, and the template's TriangleHLSL starter), so it must never be attached to a
release as-is. Instead we ship a **stripped** `.vsix` plus an `install.ps1` that
regenerates those pieces from the end user's own XDK at install time.

## Steps

1. **Bump the version** in
   [`extension/Xbox360Package/source.extension.vsixmanifest`](extension/Xbox360Package/source.extension.vsixmanifest)
   (the `Identity … Version="X.Y.Z"` attribute). The release asset name and the
   `platform.version` stamp both derive from it.

2. **Materialize the Microsoft-owned bits locally** (needs your own installed XDK):

   ```powershell
   .\setup.ps1
   ```

3. **Build the full VSIX** (compiles the C# + packages):

   ```powershell
   .\extension\build-vsix.ps1
   ```

4. **Produce the clean release asset** — strips the 3 Microsoft-owned parts from the
   VSIX and zips it with `install.ps1` + `Find-Xdk.ps1` + `README.txt`:

   ```powershell
   .\release\build-release.ps1
   ```

   Output: `release/Xbox360-VS-Extension-vX.Y.Z.zip`. The script fails if any
   Microsoft-owned entry survives the strip, so a bad asset can't be produced.

5. **Sanity-check** (optional): confirm the asset carries nothing Microsoft-owned,
   and that the installer reproduces a complete `.vsix`:

   ```powershell
   .\release\install.ps1 -NoInstall   # from an unzipped copy of the asset
   ```

6. **Publish the GitHub release**, attaching the asset:

   ```powershell
   gh release create vX.Y.Z release/Xbox360-VS-Extension-vX.Y.Z.zip `
     --title "Xbox 360 Extensions for VS 2022/2026 — vX.Y.Z" `
     --notes "..."
   ```

## What the download contains (and does not)

- **Ships:** the pre-built *original* code (`Xbox360Package.dll`, `Xbox360.XdkHost.exe`,
  the platform `props`/`targets`, the template wrapper) inside a `.vsix` with the
  Microsoft parts removed, plus `install.ps1` and `Find-Xdk.ps1`.
- **Never ships:** `Interop.XDevkit.dll`, the `1033/*.xml` property pages, or the
  template's `TEST.cpp`. `install.ps1` regenerates these from the user's own XDK and
  injects them into the `.vsix` before launching the Visual Studio installer.

See [NOTICE](NOTICE) for the licensing rationale.
