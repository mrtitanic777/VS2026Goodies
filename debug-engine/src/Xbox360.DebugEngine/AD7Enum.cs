using System;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Xbox360.DebugEngine
{
    /// <summary>Shared implementation of the AD7 COM enumerator pattern
    /// (Next/Skip/Reset/Clone/GetCount). Skip/Reset/GetCount implicitly satisfy each
    /// IEnumDebugXxx2 interface; subclasses add the typed Next and Clone.</summary>
    internal abstract class AD7Enum<T>
    {
        protected readonly T[] _items;
        protected uint _pos;
        protected AD7Enum(T[] items) { _items = items ?? Array.Empty<T>(); }

        public int Reset() { _pos = 0; return AD7Engine.S_OK; }
        public int Skip(uint celt) { _pos += celt; return _pos <= _items.Length ? AD7Engine.S_OK : AD7Engine.S_FALSE; }
        public int GetCount(out uint pcelt) { pcelt = (uint)_items.Length; return AD7Engine.S_OK; }

        protected int NextImpl(uint celt, T[] rgelt, ref uint pceltFetched)
        {
            uint i = 0;
            while (i < celt && _pos < _items.Length) rgelt[i++] = _items[_pos++];
            pceltFetched = i;
            return i == celt ? AD7Engine.S_OK : AD7Engine.S_FALSE;
        }
    }

    internal sealed class AD7ThreadEnum : AD7Enum<IDebugThread2>, IEnumDebugThreads2
    {
        public AD7ThreadEnum(IDebugThread2[] a) : base(a) { }
        public int Next(uint celt, IDebugThread2[] rgelt, ref uint pceltFetched) => NextImpl(celt, rgelt, ref pceltFetched);
        public int Clone(out IEnumDebugThreads2 ppEnum) { ppEnum = new AD7ThreadEnum(_items); return AD7Engine.S_OK; }
    }

    internal sealed class AD7ProgramEnum : AD7Enum<IDebugProgram2>, IEnumDebugPrograms2
    {
        public AD7ProgramEnum(IDebugProgram2[] a) : base(a) { }
        public int Next(uint celt, IDebugProgram2[] rgelt, ref uint pceltFetched) => NextImpl(celt, rgelt, ref pceltFetched);
        public int Clone(out IEnumDebugPrograms2 ppEnum) { ppEnum = new AD7ProgramEnum(_items); return AD7Engine.S_OK; }
    }

    internal sealed class AD7BoundBreakpointEnum : AD7Enum<IDebugBoundBreakpoint2>, IEnumDebugBoundBreakpoints2
    {
        public AD7BoundBreakpointEnum(IDebugBoundBreakpoint2[] a) : base(a) { }
        public int Next(uint celt, IDebugBoundBreakpoint2[] rgelt, ref uint pceltFetched) => NextImpl(celt, rgelt, ref pceltFetched);
        public int Clone(out IEnumDebugBoundBreakpoints2 ppEnum) { ppEnum = new AD7BoundBreakpointEnum(_items); return AD7Engine.S_OK; }
    }

    internal sealed class AD7FrameInfoEnum : AD7Enum<FRAMEINFO>, IEnumDebugFrameInfo2
    {
        public AD7FrameInfoEnum(FRAMEINFO[] a) : base(a) { }
        public int Next(uint celt, FRAMEINFO[] rgelt, ref uint pceltFetched) => NextImpl(celt, rgelt, ref pceltFetched);
        public int Clone(out IEnumDebugFrameInfo2 ppEnum) { ppEnum = new AD7FrameInfoEnum(_items); return AD7Engine.S_OK; }
    }

    internal sealed class AD7ProcessEnum : AD7Enum<IDebugProcess2>, IEnumDebugProcesses2
    {
        public AD7ProcessEnum(IDebugProcess2[] a) : base(a) { }
        public int Next(uint celt, IDebugProcess2[] rgelt, ref uint pceltFetched) => NextImpl(celt, rgelt, ref pceltFetched);
        public int Clone(out IEnumDebugProcesses2 ppEnum) { ppEnum = new AD7ProcessEnum(_items); return AD7Engine.S_OK; }
    }

    internal sealed class AD7PortEnum : AD7Enum<IDebugPort2>, IEnumDebugPorts2
    {
        public AD7PortEnum(IDebugPort2[] a) : base(a) { }
        public int Next(uint celt, IDebugPort2[] rgelt, ref uint pceltFetched) => NextImpl(celt, rgelt, ref pceltFetched);
        public int Clone(out IEnumDebugPorts2 ppEnum) { ppEnum = new AD7PortEnum(_items); return AD7Engine.S_OK; }
    }
}
