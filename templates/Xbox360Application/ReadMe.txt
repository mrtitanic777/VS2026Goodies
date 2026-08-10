Xbox 360 Application
====================

A starter Xbox 360 project for the "Xbox 360" MSBuild platform. Building it
compiles your source with the Xenon (PPC) toolset and packages the result into a
bootable .xex.

Files
-----
  <project>.vcxproj   The project. Configured for the "Xbox 360" platform with the
                      Debug / Release / Release_LTCG / Profile / Profile_FastCap
                      configurations.
  stdafx.h/.cpp       Precompiled header (pulls in <xtl.h> and <xboxmath.h>).
  <project>.cpp       Your entry point. The starter renders a spinning triangle
                      with inline HLSL shaders (the XDK's TriangleHLSL sample,
                      materialized locally from your own installed XDK).

Build & deploy
--------------
  Build in Visual Studio, or from the repo:  .\build-harness.ps1
  Deploy + launch to a console:              .\deploy-run.ps1 -Console <name-or-ip>

Note: the starter .cpp is Microsoft sample code and is provided by your own XDK
(it is not distributed with this template). See the repository's NOTICE.
