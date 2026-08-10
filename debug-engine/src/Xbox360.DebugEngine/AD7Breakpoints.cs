using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Xbox360.DebugEngine
{
    /// <summary>An address in the title's memory (IDebugCodeContext2 / IDebugMemoryContext2).</summary>
    internal sealed class AD7CodeContext : IDebugCodeContext2
    {
        private readonly AD7Engine _engine;
        public ulong Address { get; }
        public AD7CodeContext(AD7Engine engine, ulong address) { _engine = engine; Address = address; }

        public int GetName(out string pbstrName) { pbstrName = $"0x{Address:X8}"; return AD7Engine.S_OK; }

        public int GetInfo(enum_CONTEXT_INFO_FIELDS dwFields, CONTEXT_INFO[] pinfo)
        {
            var ci = new CONTEXT_INFO();
            if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS) != 0)
            { ci.bstrAddress = $"0x{Address:X8}"; ci.dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS; }
            pinfo[0] = ci;
            return AD7Engine.S_OK;
        }

        public int Add(ulong dwCount, out IDebugMemoryContext2 ppMemCxt) { ppMemCxt = new AD7CodeContext(_engine, Address + dwCount); return AD7Engine.S_OK; }
        public int Subtract(ulong dwCount, out IDebugMemoryContext2 ppMemCxt) { ppMemCxt = new AD7CodeContext(_engine, Address - dwCount); return AD7Engine.S_OK; }

        public int Compare(enum_CONTEXT_COMPARE compare, IDebugMemoryContext2[] rgpMemoryContextSet, uint dwMemoryContextSetLen, out uint pdwMemoryContext)
        {
            pdwMemoryContext = uint.MaxValue;
            for (uint i = 0; i < dwMemoryContextSetLen; i++)
            {
                if (rgpMemoryContextSet[i] is AD7CodeContext o)
                {
                    bool match;
                    switch (compare)
                    {
                        case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN: match = Address < o.Address; break;
                        case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN: match = Address > o.Address; break;
                        case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN_OR_EQUAL: match = Address <= o.Address; break;
                        case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN_OR_EQUAL: match = Address >= o.Address; break;
                        default: match = Address == o.Address; break;
                    }
                    if (match) { pdwMemoryContext = i; return AD7Engine.S_OK; }
                }
            }
            return AD7Engine.S_FALSE;
        }

        // Source mapping needs the Xbox symbol (pdb/xdb) reader — deferred.
        public int GetDocumentContext(out IDebugDocumentContext2 ppSrcCxt) { ppSrcCxt = null; return AD7Engine.S_OK; }
        public int GetLanguageInfo(ref string pbstrLanguage, ref Guid pguidLanguage) => AD7Engine.S_OK;
    }

    internal sealed class AD7BreakpointResolution : IDebugBreakpointResolution2
    {
        private readonly AD7CodeContext _context;
        public AD7BreakpointResolution(AD7CodeContext context) { _context = context; }

        public int GetBreakpointType(enum_BP_TYPE[] pBPType) { pBPType[0] = enum_BP_TYPE.BPT_CODE; return AD7Engine.S_OK; }

        public int GetResolutionInfo(enum_BPRESI_FIELDS dwFields, BP_RESOLUTION_INFO[] pBPResolutionInfo)
        {
            var ri = new BP_RESOLUTION_INFO();
            if ((dwFields & enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION) != 0)
            {
                var loc = new BP_RESOLUTION_LOCATION { bpType = (uint)enum_BP_TYPE.BPT_CODE };
                loc.unionmember1 = Marshal.GetComInterfaceForObject(_context, typeof(IDebugCodeContext2));
                ri.bpResLocation = loc;
                ri.dwFields |= enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION;
            }
            pBPResolutionInfo[0] = ri;
            return AD7Engine.S_OK;
        }
    }

    internal sealed class AD7BoundBreakpoint : IDebugBoundBreakpoint2
    {
        private readonly AD7Engine _engine;
        private readonly AD7PendingBreakpoint _pending;
        private readonly AD7BreakpointResolution _resolution;
        public uint Address { get; }
        private bool _enabled = true, _deleted;

        public AD7BoundBreakpoint(AD7Engine engine, AD7PendingBreakpoint pending, uint address)
        {
            _engine = engine; _pending = pending; Address = address;
            _resolution = new AD7BreakpointResolution(new AD7CodeContext(engine, address));
        }

        public int Enable(int fEnable)
        {
            _enabled = fEnable != 0;
            if (_enabled) _engine.Session?.SetBreakpoint(Address);   // XBDM: BREAK ADDR=0x..
            else _engine.Session?.ClearBreakpoint(Address);          // XBDM: BREAK ADDR=0x.. CLEAR
            return AD7Engine.S_OK;
        }

        public int Delete() { _deleted = true; _engine.Session?.ClearBreakpoint(Address); return AD7Engine.S_OK; }
        public int GetBreakpointResolution(out IDebugBreakpointResolution2 ppBPResolution) { ppBPResolution = _resolution; return AD7Engine.S_OK; }
        public int GetPendingBreakpoint(out IDebugPendingBreakpoint2 ppPendingBreakpoint) { ppPendingBreakpoint = _pending; return AD7Engine.S_OK; }
        public int GetHitCount(out uint pdwHitCount) { pdwHitCount = 0; return AD7Engine.S_OK; }
        public int GetState(enum_BP_STATE[] pState)
        { pState[0] = _deleted ? enum_BP_STATE.BPS_DELETED : _enabled ? enum_BP_STATE.BPS_ENABLED : enum_BP_STATE.BPS_DISABLED; return AD7Engine.S_OK; }
        public int SetCondition(BP_CONDITION bpCondition) => AD7Engine.S_OK;
        public int SetHitCount(uint dwHitCount) => AD7Engine.S_OK;
        public int SetPassCount(BP_PASSCOUNT bpPassCount) => AD7Engine.S_OK;
    }

    internal sealed class AD7PendingBreakpoint : IDebugPendingBreakpoint2
    {
        private readonly AD7Engine _engine;
        private readonly IDebugBreakpointRequest2 _request;
        private readonly List<AD7BoundBreakpoint> _bound = new List<AD7BoundBreakpoint>();
        private bool _enabled = true, _deleted;

        public AD7PendingBreakpoint(AD7Engine engine, IDebugBreakpointRequest2 request) { _engine = engine; _request = request; }

        public int GetBreakpointRequest(out IDebugBreakpointRequest2 ppBPRequest) { ppBPRequest = _request; return AD7Engine.S_OK; }
        public int CanBind(out IEnumDebugErrorBreakpoints2 ppErrorEnum) { ppErrorEnum = null; return AD7Engine.S_OK; }

        public int Bind()
        {
            uint address = ResolveAddress();
            if (address == 0) return AD7Engine.S_FALSE; // unresolved (source→address needs symbols)
            var bp = new AD7BoundBreakpoint(_engine, this, address);
            _bound.Add(bp);
            bp.Enable(1);
            _engine.SendEvent(new AD7BreakpointBoundEvent(this, new AD7BoundBreakpointEnum(_bound.ToArray())),
                              AD7BreakpointBoundEvent.IID, null, null);
            return AD7Engine.S_OK;
        }

        private uint ResolveAddress()
        {
            var info = new BP_REQUEST_INFO[1];
            _request.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION, info);
            var loc = info[0].bpLocation;
            if (loc.bpLocationType == (uint)enum_BP_LOCATION_TYPE.BPLT_CODE_CONTEXT && loc.unionmember1 != IntPtr.Zero)
            {
                if (Marshal.GetObjectForIUnknown(loc.unionmember1) is AD7CodeContext ctx)
                    return (uint)ctx.Address;
            }
            // TODO: BPLT_CODE_FILE_LINE -> resolve via the Xbox symbol (pdb/xdb) reader.
            return 0;
        }

        public int EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 ppEnum) { ppEnum = new AD7BoundBreakpointEnum(_bound.ToArray()); return AD7Engine.S_OK; }
        public int EnumErrorBreakpoints(enum_BP_ERROR_TYPE bpErrorType, out IEnumDebugErrorBreakpoints2 ppEnum) { ppEnum = null; return AD7Engine.S_OK; }
        public int Enable(int fEnable) { _enabled = fEnable != 0; foreach (var b in _bound) b.Enable(fEnable); return AD7Engine.S_OK; }
        public int Delete() { _deleted = true; foreach (var b in _bound) b.Delete(); _bound.Clear(); return AD7Engine.S_OK; }
        public int GetState(PENDING_BP_STATE_INFO[] pState)
        { pState[0].state = _deleted ? enum_PENDING_BP_STATE.PBPS_DELETED : _enabled ? enum_PENDING_BP_STATE.PBPS_ENABLED : enum_PENDING_BP_STATE.PBPS_DISABLED; return AD7Engine.S_OK; }
        public int SetCondition(BP_CONDITION bpCondition) => AD7Engine.S_OK;
        public int SetPassCount(BP_PASSCOUNT bpPassCount) => AD7Engine.S_OK;
        public int Virtualize(int fVirtualize) => AD7Engine.S_OK;
    }
}
