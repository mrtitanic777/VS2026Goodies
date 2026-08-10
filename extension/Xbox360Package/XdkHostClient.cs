using System;
using System.Diagnostics;
using System.IO;

namespace Xbox360Package
{
    /// <summary>
    /// Extension-side driver for the 32-bit Xbox360.XdkHost process.
    ///
    /// Visual Studio is x64 and XDevkit is effectively x86-only — its typelib describes 32-bit struct
    /// layouts, so reading XBOX_EVENT_INFO / XBOX_PROCESS_INFO from a 64-bit process access-violates
    /// (verified against real hardware; it would hard-crash VS). All XDevkit COM therefore runs in the
    /// out-of-process 32-bit host, and we drive it with a line protocol over stdin/stdout.
    /// </summary>
    internal sealed class XdkHostClient : IDisposable
    {
        private readonly Action<string> _log;
        private readonly Action<string> _status;
        private Process _proc;

        public XdkHostClient(Action<string> log, Action<string> status) { _log = log; _status = status; }

        public bool IsRunning => _proc != null && !_proc.HasExited;

        /// <summary>The host ships under XdkHostPayload\ in the VSIX; fall back to the extension root.</summary>
        private static string HostPath
        {
            get
            {
                string dir = Path.GetDirectoryName(typeof(Xbox360ExtensionsPackage).Assembly.Location);
                string p = Path.Combine(dir, "XdkHostPayload", "Xbox360.XdkHost.exe");
                return File.Exists(p) ? p : Path.Combine(dir, "Xbox360.XdkHost.exe");
            }
        }

        public bool Start()
        {
            if (IsRunning) return true;
            string exe = HostPath;
            if (!File.Exists(exe)) { _log?.Invoke("Error: XDevkit host not found: " + exe); return false; }

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (s, e) => { if (e.Data != null) Dispatch(e.Data); };
            _proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) _log?.Invoke("host: " + e.Data); };
            _proc.Exited += (s, e) => _status?.Invoke("Host exited");
            try { _proc.Start(); }
            catch (Exception ex) { _log?.Invoke("Error starting XDevkit host: " + ex.Message); _proc = null; return false; }
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            return true;
        }

        /// <summary>Route one host line (TAG{TAB}text) to the log/status sinks.</summary>
        private void Dispatch(string line)
        {
            int t = line.IndexOf('\t');
            string tag = t < 0 ? line : line.Substring(0, t);
            string text = t < 0 ? string.Empty : line.Substring(t + 1);
            switch (tag)
            {
                case "LOG": _log?.Invoke(text); break;
                case "ERR": _log?.Invoke("Error: " + text); break;
                case "STATUS": _status?.Invoke(text); break;
                case "READY": break;
                default: _log?.Invoke(line); break;
            }
        }

        private void Send(string cmd)
        {
            if (!IsRunning) { _log?.Invoke("Error: the XDevkit host isn't running."); return; }
            try { _proc.StandardInput.WriteLine(cmd); _proc.StandardInput.Flush(); }
            catch (Exception e) { _log?.Invoke("Error: " + e.Message); }
        }

        public void Connect(string console) { if (Start()) Send("connect\t" + console); }
        public void DeployAndDebug(string console, string xex) { if (Start()) Send("debug\t" + console + "\t" + xex); }
        public void Continue() => Send("continue");
        public void Break() => Send("break");
        public void StopSession() => Send("stop");

        public void Dispose()
        {
            try
            {
                if (IsRunning)
                {
                    Send("quit");
                    if (!_proc.WaitForExit(3000)) _proc.Kill();
                }
            }
            catch { }
            try { _proc?.Dispose(); } catch { }
            _proc = null;
        }
    }
}
