using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Xbox360.Xbdm
{
    /// <summary>
    /// A managed client for the Xbox Debug Monitor (XBDM) protocol — a line-based TCP
    /// protocol on port 730 with FTP-style 3-digit status codes. This is the transport
    /// foundation for the Xbox 360 AD7 debug engine. Higher-level debug commands
    /// (breakpoints, threads, memory) layer on top of <see cref="SendCommand"/>.
    /// </summary>
    public sealed class XbdmClient : IDisposable
    {
        public const int DefaultPort = 730;

        private TcpClient _tcp;
        private NetworkStream _stream;

        public string Host { get; private set; }
        public bool IsConnected => _tcp != null && _tcp.Connected;

        /// <summary>Connect to a console and read its greeting (a 201 "connected" response).</summary>
        public XbdmResponse Connect(string host, int port = DefaultPort, int timeoutMs = 5000)
        {
            Host = host;
            _tcp = new TcpClient();
            IAsyncResult ar = _tcp.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
            {
                _tcp.Close();
                throw new TimeoutException($"XBDM connect to {host}:{port} timed out after {timeoutMs} ms.");
            }
            _tcp.EndConnect(ar);
            _stream = _tcp.GetStream();
            return ReadResponse(); // console sends "201- connected"
        }

        /// <summary>Send a command line and read the framed response.</summary>
        public XbdmResponse SendCommand(string command)
        {
            if (_stream == null) throw new InvalidOperationException("XBDM client is not connected.");
            byte[] bytes = Encoding.ASCII.GetBytes(command + "\r\n");
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
            return ReadResponse();
        }

        /// <summary>Read exactly <paramref name="count"/> raw bytes (for 203 binary payloads,
        /// whose length the issuing command already knows).</summary>
        public byte[] ReadBinary(int count)
        {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = _stream.Read(buffer, offset, count - offset);
                if (read <= 0) throw new IOException("XBDM connection closed while reading binary payload.");
                offset += read;
            }
            return buffer;
        }

        private XbdmResponse ReadResponse()
        {
            string status = ReadLine();
            if (status.Length < 3 || !int.TryParse(status.Substring(0, 3), NumberStyles.Integer,
                                                    CultureInfo.InvariantCulture, out int code))
            {
                return new XbdmResponse(0, status);
            }

            // After the 3-digit code the console writes "- <message>" (or a bare space).
            string message = status.Length > 3 ? status.Substring(3).TrimStart('-', ' ') : string.Empty;

            if (code == (int)XbdmStatus.MultilineResponse)
            {
                var lines = new List<string>();
                for (;;)
                {
                    string line = ReadLine();
                    if (line == ".") break; // multi-line body terminator
                    lines.Add(line);
                }
                return new XbdmResponse(code, message, lines);
            }

            return new XbdmResponse(code, message);
        }

        /// <summary>Read a single CRLF-terminated line. Reads byte-wise so we never consume
        /// past a line boundary into a following binary payload.</summary>
        private string ReadLine()
        {
            var sb = new StringBuilder();
            int b;
            while ((b = _stream.ReadByte()) != -1)
            {
                if (b == '\n') break;
                if (b == '\r') continue;
                sb.Append((char)b);
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            try { _stream?.Dispose(); } catch { /* ignore */ }
            try { _tcp?.Close(); } catch { /* ignore */ }
        }
    }
}
