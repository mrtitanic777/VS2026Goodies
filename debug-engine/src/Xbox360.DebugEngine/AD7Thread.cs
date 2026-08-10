using System;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Xbox360.DebugEngine
{
    /// <summary>An Xbox 360 thread, mapping IDebugThread2 onto XBDM thread operations.</summary>
    internal sealed class AD7Thread : IDebugThread2
    {
        private readonly AD7Engine _engine;
        private readonly IDebugProgram2 _program;
        public uint ThreadId { get; }

        public AD7Thread(AD7Engine engine, IDebugProgram2 program, uint threadId)
        {
            _engine = engine;
            _program = program;
            ThreadId = threadId;
        }

        public int GetThreadId(out uint pdwThreadId) { pdwThreadId = ThreadId; return AD7Engine.S_OK; }
        public int GetName(out string pbstrName) { pbstrName = $"Xbox 360 Thread 0x{ThreadId:X8}"; return AD7Engine.S_OK; }
        public int SetThreadName(string pszName) => AD7Engine.S_OK;
        public int GetProgram(out IDebugProgram2 ppProgram) { ppProgram = _program; return AD7Engine.S_OK; }

        public int Suspend(out uint pdwSuspendCount)
        {
            _engine.Session?.Suspend(ThreadId);
            pdwSuspendCount = 0;
            return AD7Engine.S_OK;
        }

        public int Resume(out uint pdwSuspendCount)
        {
            _engine.Session?.Resume(ThreadId);
            pdwSuspendCount = 0;
            return AD7Engine.S_OK;
        }

        public int GetThreadProperties(enum_THREADPROPERTY_FIELDS dwFields, THREADPROPERTIES[] ptp)
        {
            var p = new THREADPROPERTIES();
            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_ID) != 0) { p.dwThreadId = ThreadId; p.dwFields |= enum_THREADPROPERTY_FIELDS.TPF_ID; }
            if ((dwFields & enum_THREADPROPERTY_FIELDS.TPF_NAME) != 0) { p.bstrName = $"Xbox 360 Thread 0x{ThreadId:X8}"; p.dwFields |= enum_THREADPROPERTY_FIELDS.TPF_NAME; }
            ptp[0] = p;
            return AD7Engine.S_OK;
        }

        // A single frame at the thread's PC. Full PPC back-chain walking + PC extraction from
        // GETCONTEXT comes with the symbol reader / hardware validation.
        public int EnumFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, out IEnumDebugFrameInfo2 ppEnum)
        {
            var frame = new AD7StackFrame(_engine, this, 0);
            var info = new FRAMEINFO[1];
            frame.GetInfo(dwFieldSpec, nRadix, info);
            ppEnum = new AD7FrameInfoEnum(info);
            return AD7Engine.S_OK;
        }

        public int GetLogicalThread(IDebugStackFrame2 pStackFrame, out IDebugLogicalThread2 ppLogicalThread)
        { ppLogicalThread = null; return AD7Engine.E_NOTIMPL; }

        public int CanSetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext) => AD7Engine.S_FALSE;
        public int SetNextStatement(IDebugStackFrame2 pStackFrame, IDebugCodeContext2 pCodeContext) => AD7Engine.E_NOTIMPL;
    }
}
