using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Xbox360Package
{
    /// <summary>
    /// "Xbox 360 Extensions for Visual Studio 2026" — the command package. Adds a Tools
    /// submenu with Sample Browser, Deploy &amp; Run, and Set Default Console commands.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // Auto-load (in the background) once the shell is up, so the first-run platform-install
    // check actually runs — otherwise the package only loads when a user clicks one of its
    // Tools commands, and someone who just creates+builds an Xbox 360 project never triggers
    // it, so the platform is never installed into VC ("OutputPath is not set" at build).
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class Xbox360ExtensionsPackage : AsyncPackage
    {
        public const string PackageGuidString = "9C1A0E10-3606-4360-8360-ABCDEF012345";

        protected override async Task InitializeAsync(CancellationToken cancellationToken,
                                                      IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await Xbox360Commands.InitializeAsync(this);
        }
    }
}
