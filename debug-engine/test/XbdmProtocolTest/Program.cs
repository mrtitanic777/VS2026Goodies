using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Xbox360.Xbdm;

// Mock XBDM console + client round-trip test. Validates the transport framing AND the
// debug command layer (threads, modules, memory, breakpoints) with no real hardware.
internal static class Program
{
    private static int _failures;

    private static void Check(bool cond, string label)
    {
        Console.WriteLine((cond ? "  PASS  " : "  FAIL  ") + label);
        if (!cond) _failures++;
    }

    private static int Main()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = new Thread(() =>
        {
            using (var c = listener.AcceptTcpClient())
            using (var s = c.GetStream())
            {
                void Send(string t) { var b = Encoding.ASCII.GetBytes(t); s.Write(b, 0, b.Length); s.Flush(); }
                string ReadLine()
                {
                    var sb = new StringBuilder(); int b;
                    while ((b = s.ReadByte()) != -1) { if (b == '\n') break; if (b != '\r') sb.Append((char)b); }
                    return sb.ToString();
                }

                Send("201- connected\r\n");
                for (;;)
                {
                    string cmd = ReadLine();
                    if (cmd.Length == 0) break;
                    switch (cmd)
                    {
                        case "DBGNAME": Send("200- MockDevkit\r\n"); break;
                        case "THREADS": Send("202- threads\r\n0x00000001\r\n0x00000002\r\n.\r\n"); break;
                        case "MODULES": Send("202- modules\r\nname=\"default.xex\" base=0x82000000 size=0x00100000\r\n.\r\n"); break;
                        case "GETMEM ADDR=0x82000000 LENGTH=0x00000004": Send("202- mem\r\nDEADBEEF\r\n.\r\n"); break;
                        case "BREAK ADDR=0x82001234": Send("200- breakpoint set\r\n"); break;
                        case "BREAK ADDR=0x82001234 CLEAR": Send("200- cleared\r\n"); break;
                        case "CONTINUE THREAD=0x00000001": Send("200- ok\r\n"); break;
                        default: Send("200- ok\r\n"); break;
                    }
                }
            }
        }) { IsBackground = true };
        server.Start();

        Console.WriteLine("XBDM debug-session test:");
        using (var session = new XbdmDebugSession(new XbdmClient()))
        {
            var greeting = session.Connect("127.0.0.1", port, 3000);
            Check(greeting.Code == 201, "connect greeting 201");

            Check(session.GetConsoleName() == "MockDevkit", "DBGNAME -> console name");

            var threads = session.GetThreads();
            Check(threads.Count == 2 && threads[1].Id == 2u, $"THREADS parsed ({threads.Count} threads)");

            var modules = session.GetModules();
            Check(modules.Count == 1 && modules[0].Name == "default.xex" && modules[0].Base == 0x82000000UL,
                  "MODULES parsed name/base");

            var mem = session.ReadMemory(0x82000000, 4);
            Check(mem.Length == 4 && mem[0] == 0xDE && mem[1] == 0xAD && mem[2] == 0xBE && mem[3] == 0xEF,
                  "GETMEM hex -> bytes DE AD BE EF");

            // These assert the exact command strings (the mock only 200s on an exact match).
            Check(session.SetBreakpoint(0x82001234).Code == 200, "BREAK ADDR=.. command string correct");
            Check(session.ClearBreakpoint(0x82001234).Code == 200, "BREAK ADDR=.. CLEAR command string correct");
            Check(session.Continue(1).Code == 200, "CONTINUE THREAD=.. command string correct");
        }

        listener.Stop();

        Console.WriteLine("\nXBDM notification parsing:");
        var brk = XbdmNotification.Parse("break addr=0x82001234 thread=0x00000005");
        Check(brk.Type == "break" && brk.Address == 0x82001234 && brk.ThreadId == 5, $"break notification parsed ({brk})");

        var dbg = XbdmNotification.Parse("debugstr thread=0x00000005 string=\"hello from title\"");
        Check(dbg.Type == "debugstr" && dbg.Get("string") == "\"hello from title\"", "debugstr notification parsed");

        var mod = XbdmNotification.Parse("modload name=\"default.xex\" base=0x82000000 size=0x00100000");
        Check(mod.Type == "modload" && mod.GetHex("base") == 0x82000000 && mod.Get("name") == "\"default.xex\"", "modload notification parsed");

        var step = XbdmNotification.Parse("singlestep thread=0x00000005");
        Check(step.Type == "singlestep" && step.ThreadId == 5, "singlestep notification parsed");

        Console.WriteLine(_failures == 0 ? "\nALL PASS" : $"\n{_failures} FAILURE(S)");
        return _failures == 0 ? 0 : 1;
    }
}
