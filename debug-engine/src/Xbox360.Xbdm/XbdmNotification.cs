using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Xbox360.Xbdm
{
    /// <summary>A parsed asynchronous XBDM notification (break, debugstr, create, terminate,
    /// modload, execution, singlestep, exception, ...). Fields are the key=value pairs that follow.</summary>
    public sealed class XbdmNotification
    {
        public string Type { get; private set; }
        public IReadOnlyDictionary<string, string> Fields { get; private set; }
        public string Raw { get; private set; }

        public uint ThreadId => GetHex("thread");
        public uint Address => GetHex("addr");

        public string Get(string key) => Fields.TryGetValue(key, out var v) ? v : null;

        public uint GetHex(string key)
        {
            if (!Fields.TryGetValue(key, out var v)) return 0;
            v = v.Trim();
            if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) v = v.Substring(2);
            return uint.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ? r : 0;
        }

        public static XbdmNotification Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            var parts = line.Split(new[] { ' ' }, 2);
            var fields = parts.Length > 1 ? ParseKeyValues(parts[1]) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return new XbdmNotification { Type = parts[0].ToLowerInvariant(), Fields = fields, Raw = line };
        }

        internal static Dictionary<string, string> ParseKeyValues(string s)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            while (i < s.Length)
            {
                while (i < s.Length && s[i] == ' ') i++;
                int ks = i;
                while (i < s.Length && s[i] != '=' && s[i] != ' ') i++;
                if (i >= s.Length || s[i] != '=') break;
                string key = s.Substring(ks, i - ks); i++;
                string val;
                if (i < s.Length && s[i] == '"') { int q = s.IndexOf('"', i + 1); if (q < 0) q = s.Length - 1; val = s.Substring(i, q - i + 1); i = q + 1; }
                else { int vs = i; while (i < s.Length && s[i] != ' ') i++; val = s.Substring(vs, i - vs); }
                d[key] = val;
            }
            return d;
        }

        public override string ToString() => Raw;
    }

    /// <summary>Listens for the console's asynchronous XBDM notification connection. The debug
    /// session issues "NOTIFY reconnectport=&lt;Port&gt; reverse" so the console connects back here;
    /// each notification line raises <see cref="NotificationReceived"/>.</summary>
    public sealed class XbdmNotificationListener : IDisposable
    {
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public int Port { get; private set; }
        public event Action<XbdmNotification> NotificationReceived;

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "XBDM notifications" };
            _thread.Start();
        }

        private void Loop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; }
                using (client)
                using (var stream = client.GetStream())
                {
                    string line;
                    while (_running && (line = ReadLine(stream)) != null)
                    {
                        var n = XbdmNotification.Parse(line);
                        if (n != null) NotificationReceived?.Invoke(n);
                    }
                }
            }
        }

        private static string ReadLine(NetworkStream s)
        {
            var sb = new StringBuilder();
            int b;
            try { while ((b = s.ReadByte()) != -1) { if (b == '\n') break; if (b != '\r') sb.Append((char)b); } }
            catch { return null; }
            return b == -1 && sb.Length == 0 ? null : sb.ToString();
        }

        public void Dispose()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }
    }
}
