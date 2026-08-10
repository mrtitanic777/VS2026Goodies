using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Xbox360.DebugEngine
{
    /// <summary>Supplies "ports" — each an Xbox 360 console addressed by name/IP — to VS's
    /// Attach-to-Process and launch flows (IDebugPortSupplier2). Public + ComVisible + [Guid]
    /// so the pkgdef can COM-register it under its PortSupplier CLSID.</summary>
    [ComVisible(true)]
    [Guid(Guids.PortSupplierIdString)]
    public sealed class AD7PortSupplier : IDebugPortSupplier2
    {
        private readonly List<AD7Port> _ports = new List<AD7Port>();

        public int GetPortSupplierId(out Guid pguidPortSupplier) { pguidPortSupplier = Guids.PortSupplierId; return AD7Engine.S_OK; }
        public int GetPortSupplierName(out string pbstrName) { pbstrName = "Xbox 360 Consoles"; return AD7Engine.S_OK; }
        public int CanAddPort() => AD7Engine.S_OK;

        public int AddPort(IDebugPortRequest2 pRequest, out IDebugPort2 ppPort)
        {
            pRequest.GetPortName(out string name);
            var port = new AD7Port(this, pRequest, name);
            _ports.Add(port);
            ppPort = port;
            return AD7Engine.S_OK;
        }

        public int RemovePort(IDebugPort2 pPort)
        {
            if (pPort is AD7Port p) _ports.Remove(p);
            return AD7Engine.S_OK;
        }

        public int EnumPorts(out IEnumDebugPorts2 ppEnum)
        {
            ppEnum = new AD7PortEnum(_ports.ToArray());
            return AD7Engine.S_OK;
        }

        public int GetPort(ref Guid guidPort, out IDebugPort2 ppPort)
        {
            foreach (var p in _ports)
            {
                p.GetPortId(out Guid id);
                if (id == guidPort) { ppPort = p; return AD7Engine.S_OK; }
            }
            ppPort = null;
            return AD7Engine.E_FAIL;
        }
    }

    /// <summary>A single Xbox 360 console (addressed by name/IP) as an IDebugPort2.</summary>
    internal sealed class AD7Port : IDebugPort2
    {
        private readonly AD7PortSupplier _supplier;
        private readonly IDebugPortRequest2 _request;
        public string Name { get; }
        public Guid PortId { get; } = Guid.NewGuid();

        public AD7Port(AD7PortSupplier supplier, IDebugPortRequest2 request, string name)
        {
            _supplier = supplier; _request = request; Name = name;
        }

        public int GetPortName(out string pbstrName) { pbstrName = Name; return AD7Engine.S_OK; }
        public int GetPortId(out Guid pguidPort) { pguidPort = PortId; return AD7Engine.S_OK; }
        public int GetPortSupplier(out IDebugPortSupplier2 ppSupplier) { ppSupplier = _supplier; return AD7Engine.S_OK; }
        public int GetPortRequest(out IDebugPortRequest2 ppRequest) { ppRequest = _request; return AD7Engine.S_OK; }

        // Process enumeration for Attach lists the title running on the console; a full
        // implementation queries the console over XBDM. Empty for now (launch path is primary).
        public int EnumProcesses(out IEnumDebugProcesses2 ppEnum) { ppEnum = new AD7ProcessEnum(Array.Empty<IDebugProcess2>()); return AD7Engine.S_OK; }
        public int GetProcess(AD_PROCESS_ID ProcessId, out IDebugProcess2 ppProcess) { ppProcess = null; return AD7Engine.E_NOTIMPL; }
    }
}
