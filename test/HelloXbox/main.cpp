// Minimal Xbox 360 title — smoke test for the VS 2026 build integration.
#include <xtl.h>

void __cdecl main()
{
    // Prove the XDK headers, CRT, and kernel imports link through MSBuild.
    DWORD dwTitleId = XGetGameRegion();
    OutputDebugStringA("HelloXbox: built via Visual Studio 2026 MSBuild integration.\n");
    (void)dwTitleId;
}
