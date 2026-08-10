using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Xbox360.Xbdm
{
    /// <summary>A single Xbox 360 thread as reported by THREADS/THREADINFO.</summary>
    public sealed class XbdmThread
    {
        public uint Id { get; set; }
        public ulong Base { get; set; }
        public override string ToString() => $"thread 0x{Id:X8}";
    }

    /// <summary>A loaded module (xex/section) as reported by MODULES.</summary>
    public sealed class XbdmModule
    {
        public string Name { get; set; }
        public ulong Base { get; set; }
        public ulong Size { get; set; }
        public override string ToString() => $"{Name} @ 0x{Base:X}";
    }

    /// <summary>
    /// The debug command layer over <see cref="XbdmClient"/>. Command strings use the
    /// exact wire formats embedded in xbdm.dll (uppercase; e.g. "GETMEM ADDR=0x.. LENGTH=0x..").
    /// Response PARSING for multi-line bodies is best-effort and marked where it should be
    /// confirmed against real hardware.
    /// </summary>
    public sealed class XbdmDebugSession : IDisposable
    {
        private readonly XbdmClient _client;
        public XbdmClient Client => _client;

        public XbdmDebugSession(XbdmClient client) { _client = client; }

        public XbdmResponse Connect(string host, int port = XbdmClient.DefaultPort, int timeoutMs = 5000)
            => _client.Connect(host, port, timeoutMs);

        // ---- Info ----------------------------------------------------------
        public string GetConsoleName()
        {
            var r = _client.SendCommand("DBGNAME");
            return r.IsSuccess ? r.Message : null;
        }

        public XbdmResponse SystemInfo() => _client.SendCommand("SYSTEMINFO");

        // ---- Execution control (verbs & params from xbdm.dll) --------------
        /// <summary>Break the title as soon as it starts ("BREAK START").</summary>
        public XbdmResponse BreakOnStart() => _client.SendCommand("BREAK START");

        /// <summary>Set an execution breakpoint at an address ("BREAK ADDR=0x%08x").</summary>
        public XbdmResponse SetBreakpoint(uint address)
            => _client.SendCommand($"BREAK ADDR=0x{address:X8}");

        /// <summary>Clear an execution breakpoint ("BREAK ADDR=0x%08x CLEAR").</summary>
        public XbdmResponse ClearBreakpoint(uint address)
            => _client.SendCommand($"BREAK ADDR=0x{address:X8} CLEAR");

        /// <summary>Data (read/write/execute) breakpoint ("BREAK &lt;kind&gt;=0x%08x SIZE=%lu").</summary>
        public XbdmResponse SetDataBreakpoint(string kind /* WRITE|READ|EXECUTE */, uint address, uint size)
            => _client.SendCommand($"BREAK {kind.ToUpperInvariant()}=0x{address:X8} SIZE={size}");

        public XbdmResponse Stop() => _client.SendCommand("STOP");
        public XbdmResponse Continue(uint threadId, bool exception = false)
            => _client.SendCommand($"CONTINUE THREAD=0x{threadId:X8}{(exception ? " EXCEPTION" : "")}");
        public XbdmResponse Suspend(uint threadId) => _client.SendCommand($"SUSPEND THREAD=0x{threadId:X8}");
        public XbdmResponse Resume(uint threadId) => _client.SendCommand($"RESUME THREAD=0x{threadId:X8}");
        public XbdmResponse Halt(uint threadId) => _client.SendCommand($"HALT THREAD=0x{threadId:X8}");

        /// <summary>Register the notification reconnect port ("NOTIFY reconnectport=%d reverse").</summary>
        public XbdmResponse SetupNotifications(int reconnectPort, bool reverse = true)
            => _client.SendCommand($"NOTIFY reconnectport={reconnectPort}{(reverse ? " reverse" : "")}");

        // ---- Threads -------------------------------------------------------
        /// <summary>List thread ids ("THREADS" → 202 multiline of hex ids).</summary>
        public IList<XbdmThread> GetThreads()
        {
            var list = new List<XbdmThread>();
            var r = _client.SendCommand("THREADS");
            if (r.Code != (int)XbdmStatus.MultilineResponse) return list;
            foreach (var line in r.Lines)
            {
                uint id = ParseFirstHex(line);
                if (id != 0) list.Add(new XbdmThread { Id = id });
            }
            return list;
        }

        public XbdmResponse GetThreadInfo(uint threadId) => _client.SendCommand($"THREADINFO THREAD=0x{threadId:X8}");
        public XbdmResponse GetContext(uint threadId) => _client.SendCommand($"GETCONTEXT THREAD=0x{threadId:X8}");

        // ---- Modules -------------------------------------------------------
        /// <summary>List loaded modules ("MODULES" → 202 multiline of key=value lines).</summary>
        public IList<XbdmModule> GetModules()
        {
            var list = new List<XbdmModule>();
            var r = _client.SendCommand("MODULES");
            if (r.Code != (int)XbdmStatus.MultilineResponse) return list;
            foreach (var line in r.Lines)
            {
                var kv = ParseKeyValues(line);
                var m = new XbdmModule();
                if (kv.TryGetValue("name", out var n)) m.Name = n.Trim('"');
                if (kv.TryGetValue("base", out var b)) m.Base = ParseHexOrDec(b);
                if (kv.TryGetValue("size", out var s)) m.Size = ParseHexOrDec(s);
                if (m.Name != null) list.Add(m);
            }
            return list;
        }

        // ---- Memory --------------------------------------------------------
        /// <summary>Read memory ("GETMEM ADDR=0x%08x LENGTH=0x%08x" → 202 multiline hex).
        /// The console returns hex text, using "??" for unmapped bytes (mapped to 0).</summary>
        public byte[] ReadMemory(uint address, int length)
        {
            var r = _client.SendCommand($"GETMEM ADDR=0x{address:X8} LENGTH=0x{length:X8}");
            if (r.Code != (int)XbdmStatus.MultilineResponse) return Array.Empty<byte>();
            var bytes = new List<byte>(length);
            foreach (var line in r.Lines)
            {
                for (int i = 0; i + 1 < line.Length; i += 2)
                {
                    string pair = line.Substring(i, 2);
                    if (pair == "??") { bytes.Add(0); continue; }
                    if (byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte v))
                        bytes.Add(v);
                }
            }
            return bytes.ToArray();
        }

        /// <summary>Write memory ("SETMEM ADDR=0x%08x DATA=&lt;hex&gt;").</summary>
        public XbdmResponse WriteMemory(uint address, byte[] data)
        {
            var sb = new StringBuilder();
            foreach (var b in data) sb.Append(b.ToString("X2"));
            return _client.SendCommand($"SETMEM ADDR=0x{address:X8} DATA={sb}");
        }

        // ---- helpers -------------------------------------------------------
        internal static uint ParseFirstHex(string s)
        {
            int idx = s.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
            int start = idx >= 0 ? idx + 2 : 0;
            int end = start;
            while (end < s.Length && Uri.IsHexDigit(s[end])) end++;
            if (end > start && uint.TryParse(s.Substring(start, end - start), NumberStyles.HexNumber,
                                             CultureInfo.InvariantCulture, out uint v)) return v;
            return 0;
        }

        internal static ulong ParseHexOrDec(string s)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ulong.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h : 0;
            return ulong.TryParse(s, out var d) ? d : 0;
        }

        internal static Dictionary<string, string> ParseKeyValues(string line)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            while (i < line.Length)
            {
                while (i < line.Length && line[i] == ' ') i++;
                int keyStart = i;
                while (i < line.Length && line[i] != '=' && line[i] != ' ') i++;
                if (i >= line.Length || line[i] != '=') { break; }
                string key = line.Substring(keyStart, i - keyStart);
                i++; // skip '='
                string val;
                if (i < line.Length && line[i] == '"')
                {
                    int q = line.IndexOf('"', i + 1);
                    if (q < 0) q = line.Length - 1;
                    val = line.Substring(i, q - i + 1);
                    i = q + 1;
                }
                else
                {
                    int vs = i;
                    while (i < line.Length && line[i] != ' ') i++;
                    val = line.Substring(vs, i - vs);
                }
                d[key] = val;
            }
            return d;
        }

        public void Dispose() => _client?.Dispose();
    }
}
