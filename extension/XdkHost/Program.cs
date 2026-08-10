using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using XDevkit;

namespace Xbox360.XdkHost
{
    /// <summary>
    /// 32-bit XDevkit host process.
    ///
    /// WHY THIS EXISTS: Visual Studio 2026 is x64, but XDevkit's type library describes 32-bit struct
    /// layouts (strings + interface pointers inside XBOX_EVENT_INFO / XBOX_PROCESS_INFO). Touching any
    /// struct-returning member from a 64-bit process access-violates in the CLR's struct marshaller —
    /// verified against real hardware. Every XDK tool (xbWatson, xbMon) is x86 for this reason. So ALL
    /// XDevkit COM lives here, in a 32-bit process, and the x64 VS extension drives it over a simple
    /// line protocol on stdin/stdout.
    ///
    /// stdin  : connect{TAB}console | debug{TAB}console{TAB}xexPath | continue | break | stop | quit
    /// stdout : READY | LOG{TAB}text | STATUS{TAB}text | ERR{TAB}text
    /// </summary>
    internal static class Program
    {
        private static readonly object Sync = new object();
        private static XboxManagerClass _mgr;
        private static XboxConsole _console;
        private static IXboxDebugTarget _target;
        private static XboxEvents_OnStdNotifyEventHandler _onStdNotify;

        private static int Main()
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(stdout);

            _onStdNotify = OnStdNotify;
            try { _mgr = new XboxManagerClass(); }
            catch (Exception e) { Err("XDevkit unavailable — is the Xbox 360 XDK installed? " + e.Message); return 2; }
            Emit("READY", null);

            string line;
            while ((line = Console.In.ReadLine()) != null)
            {
                var p = line.Split('\t');
                var cmd = p[0].Trim().ToLowerInvariant();
                try
                {
                    switch (cmd)
                    {
                        case "connect":  Connect(Arg(p, 1)); break;
                        case "debug":    DeployAndDebug(Arg(p, 1), Arg(p, 2), Arg(p, 3)); break;
                        case "continue": Continue(); break;
                        case "break":    BreakNow(); break;
                        case "stop":     Detach(); break;
                        case "quit":     Detach(); return 0;
                        case "":         break;
                        default:         Err("Unknown command: " + cmd); break;
                    }
                }
                catch (Exception e) { Err(Describe(e)); }
            }
            Detach();
            return 0;
        }

        private static string Arg(string[] p, int i) => i < p.Length ? p[i] : string.Empty;

        // ---- commands ---------------------------------------------------------------------------

        /// <summary>Attach as debugger and stream the console's debug output (xbWatson-style).</summary>
        private static void Connect(string consoleName)
        {
            lock (Sync)
            {
                if (_console != null) { Log("Already connected — stop first."); return; }
                if (string.IsNullOrEmpty(consoleName)) consoleName = DefaultConsole();
                Status("Connecting");
                Log("Connecting to " + consoleName + "...");
                Attach(consoleName);
                Status("Connected");
                Log("Connected. Streaming debug output.");
            }
        }

        /// <summary>
        /// Deploy the .xex, attach, arm the title-launch break, and launch.
        /// <paramref name="remoteDrive"/> is the console drive to deploy under; default "DEVKIT:\",
        /// verified booting on RGH hardware. Note "xe:\" is the dev partition on a real devkit, but on
        /// RGH/JTAG it's a writable *virtual* mapping the console cannot BOOT from — deploy succeeds and
        /// then it silently reboots to the dash.
        /// </summary>
        private static void DeployAndDebug(string consoleName, string xexPath, string remoteDrive)
        {
            lock (Sync)
            {
                if (_console != null) { Log("A session is already active — stop first."); return; }
                if (string.IsNullOrEmpty(xexPath) || !File.Exists(xexPath)) { Err("No such .xex: " + xexPath); return; }
                if (string.IsNullOrEmpty(consoleName)) consoleName = DefaultConsole();

                Status("Connecting");
                Log("Connecting to " + consoleName + "...");
                Attach(consoleName);

                string localDir = Path.GetDirectoryName(xexPath);
                string name = Path.GetFileNameWithoutExtension(xexPath);

                // RGH/JTAG consoles CANNOT execute a Debug-compiled .xex — only Release / Release_LTCG.
                // (A Debug build deploys fine, then the console silently falls back to the dash, which
                // looks like a deploy bug but isn't.) Debug .xex needs a real devkit.
                string cfg = Path.GetFileName(localDir);
                if (string.Equals(cfg, "Debug", StringComparison.OrdinalIgnoreCase))
                    Log("WARNING: this is a Debug build. Debug .xex files only run on a real devkit — " +
                        "RGH/JTAG consoles need a Release or Release_LTCG build or the title won't start.");

                if (string.IsNullOrEmpty(remoteDrive)) remoteDrive = "DEVKIT:\\";
                if (!remoteDrive.EndsWith("\\")) remoteDrive += "\\";
                string remoteRoot = remoteDrive + name;
                Log("Deploying to " + remoteRoot + " ...");
                _madeDirs.Clear();
                EnsureRemoteDir(remoteRoot);          // SendFile will NOT create parent dirs
                int n = 0, skipped = 0; bool xexSent = false;
                string xexName = Path.GetFileName(xexPath);
                foreach (var f in Directory.GetFiles(localDir, "*", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(localDir.Length).TrimStart('\\');
                    if (!IsDeployable(rel)) { skipped++; continue; }
                    string remote = remoteRoot + "\\" + rel;
                    int slash = remote.LastIndexOf('\\');
                    if (slash > 0) EnsureRemoteDir(remote.Substring(0, slash));
                    try
                    {
                        _console.SendFile(f, remote);
                        n++;
                        if (string.Equals(rel, xexName, StringComparison.OrdinalIgnoreCase)) xexSent = true;
                    }
                    catch (Exception e) { Err("SendFile " + rel + ": " + Describe(e)); }
                }
                Log("Deployed " + n + " file(s)" + (skipped > 0 ? " (skipped " + skipped + " build intermediate(s))" : "") + ".");

                if (!xexSent) { Err("The .xex failed to deploy — not launching."); Status("Failed"); return; }

                // Break when the title starts, and on crashes. NOTE: SetInitialBreakpoint() requires an
                // already-stopped thread and throws 0x82DA0008 ("Thread not stopped") against a running
                // console — StopOn is the correct API for "break when the next title launches".
                // Non-fatal: if these fail we still want the title to launch.
                TrySet(() => _target.StopOn(XboxStopOnFlags.OnTitleLaunch, true), "StopOn(OnTitleLaunch)");
                TrySet(() => _target.StopOn(XboxStopOnFlags.OnFirstChanceException, true), "StopOn(OnFirstChanceException)");
                Log("Launching " + Path.GetFileName(xexPath) + " (will break at title launch)...");
                _console.Reboot(remoteRoot + "\\" + Path.GetFileName(xexPath), remoteRoot, "",
                                XboxRebootFlags.Title | XboxRebootFlags.Wait);
                Status("Debugging");
                Log("Launched. Use Continue to run.");
            }
        }

        private static readonly System.Collections.Generic.HashSet<string> _madeDirs =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Create a remote directory and every parent (mkdir -p). IXboxConsole.SendFile does NOT create
        /// parent directories — without this, deploying to a fresh path fails with 0x82DA000D
        /// ("Cannot create file"). MakeDirectory on an existing directory just errors; ignore that.
        /// </summary>
        private static void EnsureRemoteDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !_madeDirs.Add(dir)) return;
            var parts = dir.Split('\\');
            string cur = parts[0];                      // drive, e.g. "xe:" / "Hdd:"
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                cur += "\\" + parts[i];
                try { _console.MakeDirectory(cur); } catch { /* already exists */ }
            }
        }

        /// <summary>
        /// Should this output-dir file go to the console? The build output dir is full of intermediates
        /// (.obj/.pch/.tlog/...) the title doesn't need — sending them wastes minutes over 100Mbit. Keep
        /// the .xex and any real assets (Media\, .xpr, .xzp, ...); drop known build artifacts. Symbols
        /// (.pdb) stay on the PC — the debugger reads them locally.
        /// </summary>
        private static bool IsDeployable(string rel)
        {
            if (rel.IndexOf(".tlog\\", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (rel.EndsWith("FileListAbsolute.txt", StringComparison.OrdinalIgnoreCase)) return false;
            switch (Path.GetExtension(rel).ToLowerInvariant())
            {
                case ".obj": case ".pch": case ".idb": case ".ilk": case ".pe":
                case ".recipe": case ".log": case ".tlog": case ".lastbuildstate":
                case ".pdb":   // symbols are used PC-side, not on the console
                case ".exe":   // the raw PE that imagexex wrapped into the .xex
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Run a best-effort debug-target call; report failure but don't abort the session.</summary>
        private static void TrySet(Action a, string what)
        {
            try { a(); } catch (Exception e) { Err(what + ": " + Describe(e)); }
        }

        private static void Attach(string consoleName)
        {
            _console = _mgr.OpenConsole(consoleName);
            _console.OnStdNotify += _onStdNotify;
            _target = _console.DebugTarget;
            if (_target == null) throw new InvalidOperationException("Console has no debug target (is XBDM running?).");
            _target.ConnectAsDebugger("Xbox360Extensions", XboxDebugConnectFlags.Force);
        }

        private static void Continue()
        {
            if (_target == null) { Err("Not connected."); return; }
            bool notStopped; _target.Go(out notStopped);
            Log("Continuing..."); Status("Running");
        }

        private static void BreakNow()
        {
            if (_target == null) { Err("Not connected."); return; }
            bool already; _target.Stop(out already);
            Log("Break requested.");
        }

        private static void Detach()
        {
            lock (Sync)
            {
                try
                {
                    if (_target != null)
                    {
                        try { _target.RemoveAllBreakpoints(); } catch { }
                        try { _target.DisconnectAsDebugger(); } catch { }
                        Release(_target); _target = null;
                    }
                    if (_console != null)
                    {
                        try { _console.OnStdNotify -= _onStdNotify; } catch { }
                        Release(_console); _console = null;
                    }
                }
                catch (Exception e) { Err(Describe(e)); }
                finally { Status("Stopped"); }
            }
        }

        // ---- notifications ----------------------------------------------------------------------

        private static void OnStdNotify(XboxDebugEventType code, IXboxEventInfo ev)
        {
            try
            {
                XBOX_EVENT_INFO info = ev.Info;
                switch (code)
                {
                    case XboxDebugEventType.ExecutionBreak:
                        Log(string.Format("Break at 0x{0:X8} (thread 0x{1:X})", info.Address, ThreadId(info)));
                        Status("Break");
                        break;
                    case XboxDebugEventType.SingleStep:
                        Log(string.Format("Step -> 0x{0:X8}", info.Address)); Status("Break");
                        break;
                    case XboxDebugEventType.DebugString:
                        Log(info.Message); ContinueThread(ev);
                        break;
                    case XboxDebugEventType.ModuleLoad:
                        Log("Module loaded: " + ModuleName(info)); ContinueThread(ev);
                        break;
                    case XboxDebugEventType.Exception:
                        if (info.Flags == XboxExceptionFlags.FirstChance && info.Code != 0x406D1388)
                        { Log(string.Format("EXCEPTION 0x{0:X8} at 0x{1:X8} (thread 0x{2:X})", info.Code, info.Address, ThreadId(info))); Status("Break"); }
                        break;
                    case XboxDebugEventType.AssertionFailed:
                    case XboxDebugEventType.AssertionFailedEx:
                        Log("ASSERT: " + info.Message); Status("Break");
                        break;
                    case XboxDebugEventType.RIP:
                    case XboxDebugEventType.BugCheck:
                        Log("FATAL: " + info.Message); Status("Break");
                        break;
                    case XboxDebugEventType.ExecStateChange:
                        Status(info.ExecState.ToString());
                        break;
                }
            }
            catch (Exception e) { Err(Describe(e)); }
            finally
            {
                // The CLR GC is slow to release these; release explicitly (as xbWatson does).
                try
                {
                    XBOX_EVENT_INFO info = ev.Info;
                    Release(ev);
                    if (info.Module != null) Release(info.Module);
                    if (info.Section != null) Release(info.Section);
                    if (info.Thread != null) Release(info.Thread);
                }
                catch { }
            }
        }

        private static void ContinueThread(IXboxEventInfo ev)
        {
            try
            {
                if (ev.Info.IsThreadStopped != 0)
                {
                    ev.Info.Thread.Continue(true);
                    bool notStopped; _target.Go(out notStopped);
                }
            }
            catch (Exception e) { Err(Describe(e)); }
        }

        // ---- helpers ----------------------------------------------------------------------------

        private static string DefaultConsole() { try { return _mgr.DefaultConsole; } catch { return string.Empty; } }
        private static uint ThreadId(XBOX_EVENT_INFO i) { try { return i.Thread != null ? i.Thread.ThreadId : 0u; } catch { return 0u; } }
        private static string ModuleName(XBOX_EVENT_INFO i) { try { return i.Module != null ? i.Module.ModuleInfo.Name : "(module)"; } catch { return "(module)"; } }

        private static string Describe(Exception e)
        {
            var ce = e as COMException;
            if (ce == null) return e.Message;
            string t = ""; try { t = _mgr.TranslateError(ce.ErrorCode); } catch { }
            return (string.IsNullOrEmpty(t) ? e.Message : t) + string.Format(" (HRESULT 0x{0:X8})", ce.ErrorCode);
        }

        private static void Release(object o)
        {
            if (o == null || !Marshal.IsComObject(o)) return;
            try { while (Marshal.ReleaseComObject(o) > 0) { } } catch { }
        }

        // ---- protocol ---------------------------------------------------------------------------

        private static void Emit(string tag, string text)
        {
            lock (Sync)
            {
                if (text == null) { Console.Out.WriteLine(tag); return; }
                // one output line per source line, so multi-line debug spew stays readable
                foreach (var l in text.Replace("\r", "").TrimEnd('\n').Split('\n'))
                    Console.Out.WriteLine(tag + "\t" + l);
            }
        }
        private static void Log(string s) { if (!string.IsNullOrEmpty(s)) Emit("LOG", s.TrimEnd(' ', '\t', '\n', '\r')); }
        private static void Status(string s) => Emit("STATUS", s);
        private static void Err(string s) => Emit("ERR", s);
    }
}
