using System;

namespace Xbox360.DebugEngine
{
    /// <summary>Identity GUIDs for the Xbox 360 debug engine + port supplier.
    /// New GUIDs (not the VS2010 native engine's) — that engine's metadata is only a blueprint.</summary>
    internal static class Guids
    {
        // The AD7 engine.
        public const string EngineIdString = "B7A2F1C0-3606-4360-8360-000000000E01";
        public static readonly Guid EngineId = new Guid(EngineIdString);
        public const string EngineName = "Xbox 360 Debugger";

        // The port supplier (enumerates/attaches to Xbox 360 consoles).
        public const string PortSupplierIdString = "B7A2F1C0-3606-4360-8360-000000000E02";
        public static readonly Guid PortSupplierId = new Guid(PortSupplierIdString);
    }
}
