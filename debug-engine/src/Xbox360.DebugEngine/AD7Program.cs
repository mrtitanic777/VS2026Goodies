using System;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Xbox360.DebugEngine
{
    /// <summary>The running Xbox 360 title as a debuggable program. Implements
    /// IDebugProgram2/3; one method satisfies both since IDebugProgram3 = 2 + ExecuteOnThread.</summary>
    internal sealed class AD7Program : IDebugProgram2, IDebugProgram3
    {
        private readonly AD7Engine _engine;
        private readonly AD7Process _process;
        public Guid ProgramGuid { get; } = Guid.NewGuid();
        public string Name { get; }

        public AD7Program(AD7Engine engine, AD7Process process, string name)
        {
            _engine = engine;
            _process = process;
            Name = name ?? "Xbox 360 Title";
        }

        // ---- Identity / info ----
        public int GetName(out string pbstrName) { pbstrName = Name; return AD7Engine.S_OK; }
        public int GetProgramId(out Guid pguidProgramId) { pguidProgramId = ProgramGuid; return AD7Engine.S_OK; }
        public int GetProcess(out IDebugProcess2 ppProcess) { ppProcess = _process; return AD7Engine.S_OK; }
        public int GetEngineInfo(out string pbstrEngine, out Guid pguidEngine)
        { pbstrEngine = Guids.EngineName; pguidEngine = Guids.EngineId; return AD7Engine.S_OK; }

        // ---- Threads / modules / memory ----
        public int EnumThreads(out IEnumDebugThreads2 ppEnum)
        {
            var threads = _engine.Session?.GetThreads();
            var list = new System.Collections.Generic.List<IDebugThread2>();
            if (threads != null) foreach (var t in threads) list.Add(new AD7Thread(_engine, this, t.Id));
            ppEnum = new AD7ThreadEnum(list.ToArray());
            return AD7Engine.S_OK;
        }
        public int EnumModules(out IEnumDebugModules2 ppEnum) { ppEnum = null; return AD7Engine.E_NOTIMPL; }
        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes) { ppMemoryBytes = new AD7MemoryBytes(_engine); return AD7Engine.S_OK; }

        // ---- Execution control (mapped to XBDM) ----
        public int CauseBreak() { _engine.Session?.Stop(); return AD7Engine.S_OK; }

        public int Continue(IDebugThread2 pThread) => Resume(pThread);
        public int Execute() => Resume(null);
        public int ExecuteOnThread(IDebugThread2 pThread) => Resume(pThread);

        private int Resume(IDebugThread2 pThread)
        {
            if (_engine.Session == null) return AD7Engine.E_FAIL;
            if (pThread is AD7Thread t) { _engine.Session.Continue(t.ThreadId); return AD7Engine.S_OK; }
            var threads = _engine.Session.GetThreads();
            if (threads != null) foreach (var th in threads) _engine.Session.Continue(th.Id);
            return AD7Engine.S_OK;
        }

        public int Step(IDebugThread2 pThread, enum_STEPKIND sk, enum_STEPUNIT step)
        {
            // Single-step over XBDM (continue-with-step / temp breakpoints) — implemented with breakpoints.
            return AD7Engine.E_NOTIMPL;
        }

        public int Terminate() { _engine.Session?.Dispose(); return AD7Engine.S_OK; }
        public int Detach() => AD7Engine.S_OK;
        public int CanDetach() => AD7Engine.S_OK;
        public int Attach(IDebugEventCallback2 pCallback) => AD7Engine.S_OK;

        // ---- Not yet implemented (code contexts / disassembly / properties / dump) ----
        public int EnumCodeContexts(IDebugDocumentPosition2 pDocPos, out IEnumDebugCodeContexts2 ppEnum) { ppEnum = null; return AD7Engine.E_NOTIMPL; }
        public int EnumCodePaths(string pszHint, IDebugCodeContext2 pStart, IDebugStackFrame2 pFrame, int fSource, out IEnumCodePaths2 ppEnum, out IDebugCodeContext2 ppSafety) { ppEnum = null; ppSafety = null; return AD7Engine.E_NOTIMPL; }
        public int GetDisassemblyStream(enum_DISASSEMBLY_STREAM_SCOPE dwScope, IDebugCodeContext2 pCodeContext, out IDebugDisassemblyStream2 ppDisassemblyStream) { ppDisassemblyStream = null; return AD7Engine.E_NOTIMPL; }
        public int GetDebugProperty(out IDebugProperty2 ppProperty) { ppProperty = null; return AD7Engine.E_NOTIMPL; }
        public int GetENCUpdate(out object ppUpdate) { ppUpdate = null; return AD7Engine.S_OK; }
        public int WriteDump(enum_DUMPTYPE DUMPTYPE, string pszDumpUrl) => AD7Engine.E_NOTIMPL;
    }
}
