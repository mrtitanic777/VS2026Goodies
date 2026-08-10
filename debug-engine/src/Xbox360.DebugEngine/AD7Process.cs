using System;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Xbox360.DebugEngine
{
    /// <summary>The Xbox 360 title as a debuggable process (IDebugProcess2).</summary>
    internal sealed class AD7Process : IDebugProcess2
    {
        private readonly AD7Engine _engine;
        private readonly IDebugPort2 _port;
        public Guid ProcessGuid { get; } = Guid.NewGuid();
        public string Name { get; }
        internal IDebugProgram2[] Programs { get; set; } = Array.Empty<IDebugProgram2>();

        public AD7Process(AD7Engine engine, IDebugPort2 port, string name)
        {
            _engine = engine;
            _port = port;
            Name = name ?? "Xbox 360 Title";
        }

        public int GetProcessId(out Guid pguidProcessId) { pguidProcessId = ProcessGuid; return AD7Engine.S_OK; }
        public int GetName(enum_GETNAME_TYPE gnType, out string pbstrName) { pbstrName = Name; return AD7Engine.S_OK; }
        public int GetPort(out IDebugPort2 ppPort) { ppPort = _port; return AD7Engine.S_OK; }

        public int GetPhysicalProcessId(AD_PROCESS_ID[] pProcessId)
        {
            pProcessId[0].ProcessIdType = (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_GUID;
            pProcessId[0].guidProcessId = ProcessGuid;
            return AD7Engine.S_OK;
        }

        public int EnumPrograms(out IEnumDebugPrograms2 ppEnum)
        {
            ppEnum = new AD7ProgramEnum(Programs);
            return AD7Engine.S_OK;
        }

        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            var program = Programs.Length > 0 ? Programs[0] : null;
            var xbdmThreads = _engine.Session?.GetThreads();
            var list = new System.Collections.Generic.List<IDebugThread2>();
            if (xbdmThreads != null)
                foreach (var t in xbdmThreads)
                    list.Add(new AD7Thread(_engine, program, t.Id));
            ppEnum = new AD7ThreadEnum(list.ToArray());
            return AD7Engine.S_OK;
        }

        public int GetInfo(enum_PROCESS_INFO_FIELDS Fields, PROCESS_INFO[] pProcessInfo)
        {
            var info = new PROCESS_INFO();
            if ((Fields & enum_PROCESS_INFO_FIELDS.PIF_PROCESS_ID) != 0)
            { info.ProcessId.ProcessIdType = (uint)enum_AD_PROCESS_ID.AD_PROCESS_ID_GUID; info.ProcessId.guidProcessId = ProcessGuid; info.Fields |= enum_PROCESS_INFO_FIELDS.PIF_PROCESS_ID; }
            if ((Fields & enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME) != 0)
            { info.bstrBaseName = Name; info.Fields |= enum_PROCESS_INFO_FIELDS.PIF_BASE_NAME; }
            pProcessInfo[0] = info;
            return AD7Engine.S_OK;
        }

        public int CauseBreak() { _engine.Session?.Stop(); return AD7Engine.S_OK; }
        public int Terminate() { _engine.Session?.Dispose(); return AD7Engine.S_OK; }
        public int CanDetach() => AD7Engine.S_OK;
        public int Detach() => AD7Engine.S_OK;

        public int Attach(IDebugEventCallback2 pCallback, Guid[] rgguidSpecificEngines, uint celtSpecificEngines, int[] rghrEngineAttach)
            => AD7Engine.S_OK;

        public int GetAttachedSessionName(out string pbstrSessionName) { pbstrSessionName = null; return AD7Engine.E_NOTIMPL; }
        public int GetServer(out IDebugCoreServer2 ppServer) { ppServer = null; return AD7Engine.E_NOTIMPL; }
    }
}
