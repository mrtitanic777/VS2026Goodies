using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop;
using Xbox360.Xbdm;

namespace Xbox360.DebugEngine
{
    /// <summary>
    /// The Xbox 360 AD7 debug engine. Implements the engine-side AD7 interfaces and maps them
    /// onto the managed <see cref="XbdmDebugSession"/>. Program/thread/breakpoint objects and
    /// the event pump are added in sibling files as the implementation fills in.
    /// </summary>
    [ComVisible(true)]
    [Guid(Guids.EngineIdString)]
    public sealed class AD7Engine : IDebugEngine2, IDebugEngineLaunch2
    {
        internal const int S_OK = 0;
        internal const int S_FALSE = 1;
        internal const int E_NOTIMPL = unchecked((int)0x80004001);
        internal const int E_FAIL = unchecked((int)0x80004005);

        private IDebugEventCallback2 _callback;
        private XbdmDebugSession _session;
        private string _console;
        private AD7Process _process;
        private AD7Program _program;

        internal IDebugEventCallback2 Callback => _callback;
        internal XbdmDebugSession Session => _session;

        /// <summary>Dispatch an AD7 event to VS's debugger via the event callback.</summary>
        internal void SendEvent(IDebugEvent2 eventObject, string iid, IDebugProgram2 program, IDebugThread2 thread)
        {
            if (_callback == null) return;
            var riid = new Guid(iid);
            eventObject.GetAttributes(out uint attributes);
            _callback.Event(this, _process, program, thread, eventObject, ref riid, attributes);
        }

        private XbdmNotificationListener _notifications;

        /// <summary>Open the XBDM notification channel so the console reports stops/output back to us.</summary>
        internal void StartNotifications()
        {
            _notifications = new XbdmNotificationListener();
            _notifications.NotificationReceived += OnNotification;
            _notifications.Start();
            _session?.SetupNotifications(_notifications.Port);
        }

        /// <summary>Translate an async XBDM notification into the corresponding AD7 event.</summary>
        internal void OnNotification(XbdmNotification n)
        {
            IDebugThread2 thread = new AD7Thread(this, _program, n.ThreadId);
            switch (n.Type)
            {
                case "break":
                case "data": // breakpoint / data breakpoint hit -> VS stops on the bound breakpoint
                    SendEvent(new AD7BreakpointEvent(new AD7BoundBreakpointEnum(Array.Empty<IDebugBoundBreakpoint2>())),
                              AD7BreakpointEvent.IID, _program, thread);
                    break;
                case "singlestep":
                    SendEvent(new AD7StepCompleteEvent(), AD7StepCompleteEvent.IID, _program, thread);
                    break;
                case "debugstr":
                    SendEvent(new AD7OutputDebugStringEvent((n.Get("string") ?? n.Raw) + "\n"),
                              AD7OutputDebugStringEvent.IID, _program, thread);
                    break;
                case "create":
                    SendEvent(new AD7ThreadCreateEvent(), AD7ThreadCreateEvent.IID, _program, thread);
                    break;
                case "terminate":
                    SendEvent(new AD7ThreadDestroyEvent(0), AD7ThreadDestroyEvent.IID, _program, thread);
                    break;
            }
        }

        // ---- IDebugEngine2 -------------------------------------------------
        public int GetEngineId(out Guid pguidEngine)
        {
            pguidEngine = Guids.EngineId;
            return S_OK;
        }

        public int Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms,
                          IDebugEventCallback2 pCallback, enum_ATTACH_REASON dwReason)
        {
            _callback = pCallback;
            // TODO: bind rgpPrograms[0] to an XBDM session and raise the load/attach events.
            return S_OK;
        }

        public int CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP)
        {
            ppPendingBP = new AD7PendingBreakpoint(this, pBPRequest);
            return S_OK;
        }

        public int CauseBreak()
        {
            // Break into the running title (XBDM STOP).
            _session?.Stop();
            return S_OK;
        }

        public int ContinueFromSynchronousEvent(IDebugEvent2 pEvent) => S_OK;
        public int DestroyProgram(IDebugProgram2 pProgram) => S_OK;
        public int EnumPrograms(out IEnumDebugPrograms2 ppEnum) { ppEnum = null; return E_NOTIMPL; }
        public int RemoveAllSetExceptions(ref Guid guidType) => S_OK;
        public int RemoveSetException(EXCEPTION_INFO[] pException) => S_OK;
        public int SetException(EXCEPTION_INFO[] pException) => S_OK;
        public int SetLocale(ushort wLangID) => S_OK;
        public int SetMetric(string pszMetric, object varValue) => S_OK;
        public int SetRegistryRoot(string pszRegistryRoot) => S_OK;

        // ---- IDebugEngineLaunch2 ------------------------------------------
        public int LaunchSuspended(string pszServer, IDebugPort2 pPort, string pszExe, string pszArgs,
                                   string pszDir, string bstrEnv, string pszOptions, enum_LAUNCH_FLAGS dwLaunchFlags,
                                   uint hStdInput, uint hStdOutput, uint hStdError, IDebugEventCallback2 pCallback,
                                   out IDebugProcess2 ppProcess)
        {
            _callback = pCallback;
            _console = pszServer;
            _session = new XbdmDebugSession(new XbdmClient());
            _process = new AD7Process(this, pPort, System.IO.Path.GetFileName(pszExe ?? "Xbox360Title.xex"));
            ppProcess = _process;
            return S_OK;
        }

        public int ResumeProcess(IDebugProcess2 pProcess)
        {
            // Bind the program and raise the standard AD7 startup event sequence so VS
            // shows the debug session. (Console connect + magicboot land with the port supplier.)
            _program = new AD7Program(this, _process, _process?.Name);
            if (_process != null) _process.Programs = new IDebugProgram2[] { _program };

            SendEvent(new AD7EngineCreateEvent(this), AD7EngineCreateEvent.IID, null, null);
            SendEvent(new AD7ProgramCreateEvent(), AD7ProgramCreateEvent.IID, _program, null);
            SendEvent(new AD7LoadCompleteEvent(), AD7LoadCompleteEvent.IID, _program, null);
            return S_OK;
        }
        public int CanTerminateProcess(IDebugProcess2 pProcess) => S_OK;

        public int TerminateProcess(IDebugProcess2 pProcess)
        {
            _session?.Dispose();
            _session = null;
            return S_OK;
        }
    }
}
