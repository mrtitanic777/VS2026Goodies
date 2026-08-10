using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;
using MessageBox = System.Windows.Forms.MessageBox;
using WinForms = System.Windows.Forms;

namespace Xbox360Package
{
    /// <summary>The three Tools-menu commands: Sample Browser, Deploy &amp; Run, Set Default Console.</summary>
    internal static class Xbox360Commands
    {
        private static readonly Guid CmdSet = new Guid("D1CE0001-3606-4360-8360-ABCDEF012345");
        private const int CmdidSampleBrowser = 0x0100;
        private const int CmdidDeployRun = 0x0101;
        private const int CmdidSetConsole = 0x0102;
        private const int CmdidInstallPlatform = 0x0103;
        private const int CmdidDebugOutput = 0x0104;
        private const int CmdidDebugOutputStop = 0x0105;
        private const int CmdidDeployDebug = 0x0106;
        private const int CmdidDebugContinue = 0x0107;
        private const int CmdidStopDebug = 0x0108;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (await package.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService mcs)
            {
                mcs.AddCommand(new MenuCommand((s, e) => OnSampleBrowser(), new CommandID(CmdSet, CmdidSampleBrowser)));
                mcs.AddCommand(new MenuCommand((s, e) => OnDeployRun(), new CommandID(CmdSet, CmdidDeployRun)));
                mcs.AddCommand(new MenuCommand((s, e) => OnSetConsole(), new CommandID(CmdSet, CmdidSetConsole)));
                mcs.AddCommand(new MenuCommand((s, e) => OnInstallPlatform(), new CommandID(CmdSet, CmdidInstallPlatform)));
                mcs.AddCommand(new MenuCommand((s, e) => OnConnectDebugOutput(), new CommandID(CmdSet, CmdidDebugOutput)));
                mcs.AddCommand(new MenuCommand((s, e) => OnDisconnectDebugOutput(), new CommandID(CmdSet, CmdidDebugOutputStop)));
                mcs.AddCommand(new MenuCommand((s, e) => OnDeployDebug(), new CommandID(CmdSet, CmdidDeployDebug)));
                mcs.AddCommand(new MenuCommand((s, e) => OnDebugContinue(), new CommandID(CmdSet, CmdidDebugContinue)));
                mcs.AddCommand(new MenuCommand((s, e) => OnStopDebug(), new CommandID(CmdSet, CmdidStopDebug)));
            }

            // First-run self-configuration: if the Xbox 360 platform isn't yet installed into
            // this VS's build system, offer to install it now (one UAC prompt). Once installed,
            // this check passes silently on every subsequent launch.
            await EnsurePlatformInstalledAsync(package);
        }

        /// <summary>
        /// Returns the VC toolset root (…\MSBuild\Microsoft\VC) of the running VS instance,
        /// or null if it can't be determined. Read-only; needs no elevation.
        /// </summary>
        private static async System.Threading.Tasks.Task<string> GetVcTargetsRootAsync(AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!(await package.GetServiceAsync(typeof(SVsShell)) is IVsShell shell)) return null;
            if (shell.GetProperty((int)__VSSPROPID.VSSPROPID_InstallDirectory, out var raw) != 0) return null;
            var ide = raw as string;            // …\Common7\IDE\
            if (string.IsNullOrEmpty(ide)) return null;
            var vc = Path.GetFullPath(Path.Combine(ide, @"..\..\MSBuild\Microsoft\VC"));
            return Directory.Exists(vc) ? vc : null;
        }

        private static string ExtensionDir => Path.GetDirectoryName(typeof(Xbox360ExtensionsPackage).Assembly.Location);
        private static string LogPath => Path.Combine(Path.GetTempPath(), "xbox360-platform-install.log");

        /// <summary>The platform version this extension bundles (from the payload's stamp).</summary>
        private static string BundledPlatformVersion()
        {
            try { var f = Path.Combine(ExtensionDir, "Platform", "Xbox 360", "platform.version");
                  return File.Exists(f) ? File.ReadAllText(f).Trim() : ""; }
            catch { return ""; }
        }

        /// <summary>Highest Xbox 360 platform version installed under this VS, or null if none is.</summary>
        private static string InstalledPlatformVersion(string vcRoot)
        {
            string best = null;
            foreach (var ts in Directory.GetDirectories(vcRoot, "v1*"))
            {
                var dir = Path.Combine(ts, "Platforms", "Xbox 360");
                if (!File.Exists(Path.Combine(dir, "Platform.targets"))) continue;
                var vf = Path.Combine(dir, "platform.version");
                var v = File.Exists(vf) ? File.ReadAllText(vf).Trim() : "0";   // present but unstamped => treat as old
                if (best == null || CompareVersions(v, best) > 0) best = v;
            }
            return best;   // null => not installed in any toolset
        }

        private static int CompareVersions(string a, string b)
        {
            return (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb))
                ? va.CompareTo(vb) : string.CompareOrdinal(a ?? "", b ?? "");
        }

        /// <summary>Is the Xbox 360 XDK present? (needed to BUILD; the platform installs regardless.)</summary>
        private static bool XdkInstalled()
        {
            var xedk = Environment.GetEnvironmentVariable("XEDK");
            if (!string.IsNullOrEmpty(xedk) && Directory.Exists(Path.Combine(xedk, "bin", "win32"))) return true;
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                using (var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var k = bk.OpenSubKey(@"SOFTWARE\Microsoft\Xbox\2.0\SDK"))
                    if (k?.GetValue("InstallPath") is string p && Directory.Exists(p)) return true;
            return false;
        }

        /// <summary>
        /// First-run / self-healing setup: installs the platform if it's missing, or auto-updates it
        /// if this extension bundles a newer version than what's installed. Fully automatic bar one UAC.
        /// </summary>
        private static async Task EnsurePlatformInstalledAsync(AsyncPackage package)
        {
            try
            {
                var vcRoot = await GetVcTargetsRootAsync(package);
                if (vcRoot == null) return;   // can't locate VC -> don't nag

                var installed = InstalledPlatformVersion(vcRoot);   // null if none
                var bundled = BundledPlatformVersion();
                bool needInstall = installed == null;
                bool needUpdate = installed != null && bundled.Length > 0 && CompareVersions(installed, bundled) < 0;
                if (!needInstall && !needUpdate) return;   // up to date -> silent

                await package.JoinableTaskFactory.SwitchToMainThreadAsync();
                string xdkNote = XdkInstalled() ? "" :
                    "\n\nNote: the Xbox 360 XDK (21256) wasn't detected. You'll need it installed to BUILD titles — " +
                    "everything will work once it's present.";
                string msg = needUpdate
                    ? $"An updated Xbox 360 build platform (v{bundled}) is available.\n\nUpdate now? " +
                      "This needs a one-time administrator approval (one UAC prompt)." + xdkNote
                    : "Finishing setup: the Xbox 360 build platform needs to be added to Visual Studio's C++ build " +
                      "system.\n\nSet it up now? This needs a one-time administrator approval (one UAC prompt) and " +
                      "takes a few seconds." + xdkNote;

                if (MessageBox.Show(msg, "Xbox 360 Extensions",
                        WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Information) != WinForms.DialogResult.Yes)
                    return;

                int code = await Task.Run(() => RunPlatformInstaller());   // off the UI thread so VS doesn't freeze
                await package.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (code == 0)
                {
                    if (MessageBox.Show(
                            "Xbox 360 setup complete. Visual Studio needs to restart to load the platform.\n\nRestart now?",
                            "Xbox 360 Extensions", WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Information)
                        == WinForms.DialogResult.Yes)
                        await RestartVsAsync(package);
                }
                else
                {
                    MessageBox.Show(
                        "Setup didn't complete:\n" + InstallerMessage(code) +
                        "\n\nIt will try again next time you open Visual Studio, or retry any time from " +
                        "Tools → Install/Update Xbox 360 Platform.\n\nLog: " + LogPath,
                        "Xbox 360 Extensions", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                }
            }
            catch { /* first-run convenience only — never block VS startup on it */ }
        }

        /// <summary>Tools-menu "Install/Update Xbox 360 Platform" — force a (re)install.</summary>
        private static void OnInstallPlatform()
        {
            int code = RunPlatformInstaller();
            if (code == 0)
                MessageBox.Show("Xbox 360 platform installed/updated. Restart Visual Studio to load it.",
                    "Xbox 360 Extensions", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            else
                MessageBox.Show("Platform install didn't complete:\n" + InstallerMessage(code) + "\n\nLog: " + LogPath,
                    "Xbox 360 Extensions", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
        }

        /// <summary>Runs the bundled elevated installer. Returns its exit code (0 = success);
        /// -1 if UAC was declined / it couldn't start, -2 if the script is missing.</summary>
        private static int RunPlatformInstaller()
        {
            var script = Path.Combine(ExtensionDir, "Platform", "install-platform.ps1");
            if (!File.Exists(script)) return -2;
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"")
                { UseShellExecute = true, Verb = "runas" };   // elevate: VCTargets is under Program Files
                using (var p = Process.Start(psi)) { p.WaitForExit(); return p.ExitCode; }
            }
            catch { return -1; }   // UAC declined or couldn't start
        }

        private static string InstallerMessage(int code)
        {
            switch (code)
            {
                case -2: return "The platform installer was missing from the extension package.";
                case -1: return "Administrator approval was declined, or the installer couldn't start.";
                case 2:  return "The platform files were missing from the extension package.";
                case 3:  return "No Visual Studio C++ build tools were found. Install the \"Desktop development with C++\" workload, then retry.";
                case 4:  return "The platform files couldn't be verified after copying (antivirus or permissions?).";
                default: return "The installer reported an error (code " + code + ").";
            }
        }

        private static async Task RestartVsAsync(AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (await package.GetServiceAsync(typeof(SVsShell)) is IVsShell4 shell4)
                shell4.Restart((uint)__VSRESTARTTYPE.RESTART_Normal);
        }

        private static string Xedk =>
            Environment.GetEnvironmentVariable("XEDK") ?? @"C:\Program Files (x86)\Microsoft Xbox 360 SDK";

        private static string DefaultConsole()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\XenonSDK"))
                return key?.GetValue("XboxName") as string ?? string.Empty;
        }

        private static void OnSampleBrowser()
        {
            var exe = Path.Combine(Xedk, @"Source\Tools\SampleBrowser\SampleBrowser.exe");
            if (!File.Exists(exe)) { MessageBox.Show("Sample Browser not found:\n" + exe); return; }
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
            }
            catch (Exception ex) { MessageBox.Show("Failed to launch Sample Browser:\n" + ex.Message); }
        }

        private static void OnSetConsole()
        {
            var name = ConsolePrompt.Show("Set Default Xbox 360 Console", "Console name or IP address:", DefaultConsole());
            if (string.IsNullOrEmpty(name)) return;
            using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\XenonSDK"))
                key.SetValue("XboxName", name);
            MessageBox.Show("Default Xbox 360 console set to: " + name);
        }

        private static void OnDeployRun()
        {
            var console = ConsolePrompt.Show("Deploy && Run to Xbox 360", "Target console name or IP:", DefaultConsole());
            if (string.IsNullOrEmpty(console)) return;

            string xex;
            using (var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Select the .xex to deploy",
                Filter = "Xbox 360 Executable (*.xex)|*.xex"
            })
            {
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                xex = ofd.FileName;
            }

            var localDir = Path.GetDirectoryName(xex);
            var xexName = Path.GetFileName(xex);
            var remoteRoot = "xe:\\" + Path.GetFileNameWithoutExtension(xex);
            var bin = Path.Combine(Xedk, @"bin\win32");

            try
            {
                RunTool(bin, "xbmkdir.exe", $"/X:{console} {remoteRoot}");
                foreach (var f in Directory.GetFiles(localDir, "*", SearchOption.AllDirectories))
                {
                    var rel = f.Substring(localDir.Length).TrimStart('\\');
                    var remote = remoteRoot + "\\" + rel;
                    var remoteDir = remote.Substring(0, remote.LastIndexOf('\\'));
                    RunTool(bin, "xbmkdir.exe", $"/X:{console} \"{remoteDir}\"");
                    RunTool(bin, "xbcp.exe", $"/X:{console} /Y \"{f}\" \"{remote}\"");
                }
                RunTool(bin, "xbreboot.exe", $"/X:{console} \"{remoteRoot}\\{xexName}\"");
                MessageBox.Show($"Deployed and launched {xexName} on {console}.");
            }
            catch (Exception ex) { MessageBox.Show("Deploy failed:\n" + ex.Message); }
        }

        private static void RunTool(string binDir, string tool, string args)
        {
            var psi = new ProcessStartInfo(Path.Combine(binDir, tool), args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi)) { p.WaitForExit(); }
        }

        // ---- Debug output (xbWatson-style live console spew, via XDevkit) ------------------------

        private static readonly Guid DebugOutputPaneGuid = new Guid("D1CE0002-3606-4360-8360-ABCDEF012345");
        private static IVsOutputWindowPane _debugPane;
        // All XDevkit COM runs in the out-of-process 32-bit host (VS is x64; XDevkit's structs
        // access-violate in a 64-bit process). This client drives it.
        private static XdkHostClient _host;

        /// <summary>Get (creating once) the "Xbox 360 Debug Output" Output-window pane. Main thread only.</summary>
        private static IVsOutputWindowPane GetDebugPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_debugPane != null) return _debugPane;
            if (Package.GetGlobalService(typeof(SVsOutputWindow)) is IVsOutputWindow ow)
            {
                Guid g = DebugOutputPaneGuid;
                ow.CreatePane(ref g, "Xbox 360 Debug Output", 1, 1);
                ow.GetPane(ref g, out _debugPane);
            }
            return _debugPane;
        }

        /// <summary>Append a line to the pane. Thread-safe (called from the host's stdout reader).</summary>
        private static void WriteDebugLine(string line)
        {
            _debugPane?.OutputStringThreadSafe(line + Environment.NewLine);
        }

        private static XdkHostClient Host()
        {
            if (_host == null) _host = new XdkHostClient(WriteDebugLine, s => WriteDebugLine("[" + s + "]"));
            return _host;
        }

        private static void OnConnectDebugOutput()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var console = ConsolePrompt.Show("Xbox 360 Debug Output", "Console name or IP:", DefaultConsole());
            if (string.IsNullOrEmpty(console)) return;
            GetDebugPane()?.Activate();       // create+show the pane before the host starts logging
            Host().Connect(console);
        }

        private static void OnDisconnectDebugOutput() => Host().StopSession();

        // ---- Deploy & Debug (F5): deploy → attach → break, run in the 32-bit XDevkit host ---------

        private static void OnDeployDebug()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var console = ConsolePrompt.Show("Deploy && Debug on Xbox 360", "Console name or IP:", DefaultConsole());
            if (string.IsNullOrEmpty(console)) return;

            string xex;
            using (var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Select the .xex to deploy and debug",
                Filter = "Xbox 360 Executable (*.xex)|*.xex"
            })
            {
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                xex = ofd.FileName;
            }

            GetDebugPane()?.Activate();
            Host().DeployAndDebug(console, xex);
        }

        private static void OnDebugContinue() => Host().Continue();
        private static void OnStopDebug() => Host().StopSession();
    }
}
