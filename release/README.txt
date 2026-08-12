Xbox 360 Extensions for Visual Studio 2022 / 2026 — no-compile install
=====================================================================

This package installs the extension WITHOUT building any source code.

It ships only original, pre-built code and contains NO Microsoft SDK files.
The few Microsoft-owned pieces the extension needs are generated on YOUR machine,
from YOUR OWN installed Xbox 360 XDK, and injected into the .vsix at install time:
  * the XDevkit interop  (built from your XDK's XDevkit.idl)
  * the IDE property pages (from the Xbox 360 platform your XDK's VS integration installed)
  * the project template's starter source (your XDK's TriangleHLSL sample)

Requirements
------------
  * Visual Studio 2022 or 2026 with "Desktop development with C++"
    (provides midl + TlbImp, needed to build the interop)
  * Your own Xbox 360 XDK, plus its Visual Studio MSBuild integration
    (installs the Xbox 360 property pages)

Install
-------
  1. Unzip this folder anywhere.
  2. In PowerShell, from the unzipped folder, run:

         .\install.ps1

     It locates your XDK, completes the .vsix, and launches the VS installer.
     Use  .\install.ps1 -NoInstall  to just produce the .vsix without installing,
     or   .\install.ps1 -Xedk <path>  if your XDK is somewhere unusual.

This is an independent, unofficial tool; it is not affiliated with or endorsed by
Microsoft. "Xbox 360" is a trademark of Microsoft Corporation. You must supply your
own legally licensed Xbox 360 XDK. See the project repository's NOTICE.
