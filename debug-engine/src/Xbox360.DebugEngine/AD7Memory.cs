using System;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Xbox360.DebugEngine
{
    /// <summary>The title's memory, read/written over XBDM GETMEM/SETMEM (IDebugMemoryBytes2).</summary>
    internal sealed class AD7MemoryBytes : IDebugMemoryBytes2
    {
        private readonly AD7Engine _engine;
        public AD7MemoryBytes(AD7Engine engine) { _engine = engine; }

        public int ReadAt(IDebugMemoryContext2 pStartContext, uint dwCount, byte[] rgbMemory,
                          out uint pdwRead, ref uint pdwUnreadable)
        {
            uint addr = pStartContext is AD7CodeContext c ? (uint)c.Address : 0;
            byte[] data = _engine.Session?.ReadMemory(addr, (int)dwCount) ?? Array.Empty<byte>();
            uint n = (uint)Math.Min(data.Length, (int)dwCount);
            Array.Copy(data, rgbMemory, n);
            pdwRead = n;
            pdwUnreadable = dwCount - n;
            return AD7Engine.S_OK;
        }

        public int WriteAt(IDebugMemoryContext2 pStartContext, uint dwCount, byte[] rgbMemory)
        {
            uint addr = pStartContext is AD7CodeContext c ? (uint)c.Address : 0;
            var data = new byte[dwCount];
            Array.Copy(rgbMemory, data, (int)dwCount);
            _engine.Session?.WriteMemory(addr, data);
            return AD7Engine.S_OK;
        }

        public int GetSize(out ulong pqwSize) { pqwSize = uint.MaxValue; return AD7Engine.S_OK; }
    }

    /// <summary>A call-stack frame. Currently a single frame at the thread's PC; full stack
    /// walking (PPC back-chain) and source/locals come with the symbol reader.</summary>
    internal sealed class AD7StackFrame : IDebugStackFrame2
    {
        private readonly AD7Engine _engine;
        private readonly AD7Thread _thread;
        private readonly ulong _pc;

        public AD7StackFrame(AD7Engine engine, AD7Thread thread, ulong pc) { _engine = engine; _thread = thread; _pc = pc; }

        public int GetName(out string pbstrName) { pbstrName = $"0x{_pc:X8}"; return AD7Engine.S_OK; }
        public int GetThread(out IDebugThread2 ppThread) { ppThread = _thread; return AD7Engine.S_OK; }
        public int GetCodeContext(out IDebugCodeContext2 ppCodeCxt) { ppCodeCxt = new AD7CodeContext(_engine, _pc); return AD7Engine.S_OK; }

        public int GetInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, FRAMEINFO[] pFrameInfo)
        {
            var fi = new FRAMEINFO();
            if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME) != 0)
            { fi.m_bstrFuncName = $"0x{_pc:X8}"; fi.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FUNCNAME; }
            if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FRAME) != 0)
            { fi.m_pFrame = this; fi.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FRAME; }
            pFrameInfo[0] = fi;
            return AD7Engine.S_OK;
        }

        public int GetPhysicalStackRange(out ulong paddrMin, out ulong paddrMax) { paddrMin = 0; paddrMax = 0; return AD7Engine.S_OK; }
        public int GetLanguageInfo(ref string pbstrLanguage, ref Guid pguidLanguage) { pbstrLanguage = "C++"; return AD7Engine.S_OK; }

        // Locals / source / expression evaluation need the symbol reader — deferred.
        public int EnumProperties(enum_DEBUGPROP_INFO_FLAGS dwFields, uint nRadix, ref Guid guidFilter, uint dwTimeout, out uint pcelt, out IEnumDebugPropertyInfo2 ppEnum)
        { pcelt = 0; ppEnum = null; return AD7Engine.E_NOTIMPL; }
        public int GetDocumentContext(out IDebugDocumentContext2 ppCxt) { ppCxt = null; return AD7Engine.S_OK; }
        public int GetExpressionContext(out IDebugExpressionContext2 ppExprCxt) { ppExprCxt = null; return AD7Engine.E_NOTIMPL; }
        public int GetDebugProperty(out IDebugProperty2 ppProperty) { ppProperty = null; return AD7Engine.E_NOTIMPL; }
    }
}
