using Microsoft.VisualStudio.Debugger.Interop;

namespace Xbox360.DebugEngine
{
    // ---- Event attribute base classes ------------------------------------
    internal class AD7AsynchronousEvent : IDebugEvent2
    {
        public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNCHRONOUS;
        int IDebugEvent2.GetAttributes(out uint a) { a = Attributes; return AD7Engine.S_OK; }
    }

    internal class AD7StoppingEvent : IDebugEvent2
    {
        public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_ASYNC_STOP;
        int IDebugEvent2.GetAttributes(out uint a) { a = Attributes; return AD7Engine.S_OK; }
    }

    internal class AD7SynchronousEvent : IDebugEvent2
    {
        public const uint Attributes = (uint)enum_EVENTATTRIBUTES.EVENT_SYNCHRONOUS;
        int IDebugEvent2.GetAttributes(out uint a) { a = Attributes; return AD7Engine.S_OK; }
    }

    // ---- Concrete events (IIDs are the well-known AD7 event GUIDs) --------

    internal sealed class AD7EngineCreateEvent : AD7AsynchronousEvent, IDebugEngineCreateEvent2
    {
        public const string IID = "FE5B734C-759D-4E59-AB04-F103343BDD06";
        private readonly IDebugEngine2 _engine;
        public AD7EngineCreateEvent(IDebugEngine2 engine) { _engine = engine; }
        int IDebugEngineCreateEvent2.GetEngine(out IDebugEngine2 engine) { engine = _engine; return AD7Engine.S_OK; }
    }

    internal sealed class AD7ProgramCreateEvent : AD7AsynchronousEvent, IDebugProgramCreateEvent2
    {
        public const string IID = "96CD11EE-ECD4-4E89-957E-B5D496FC4139";
    }

    internal sealed class AD7ProgramDestroyEvent : AD7AsynchronousEvent, IDebugProgramDestroyEvent2
    {
        public const string IID = "E147E9E3-6440-4073-A7B7-A65592C714B5";
        private readonly uint _exitCode;
        public AD7ProgramDestroyEvent(uint exitCode) { _exitCode = exitCode; }
        int IDebugProgramDestroyEvent2.GetExitCode(out uint exitCode) { exitCode = _exitCode; return AD7Engine.S_OK; }
    }

    internal sealed class AD7LoadCompleteEvent : AD7StoppingEvent, IDebugLoadCompleteEvent2
    {
        public const string IID = "B1844850-1349-45D4-9F12-495212F5EB0B";
    }

    internal sealed class AD7EntryPointEvent : AD7StoppingEvent, IDebugEntryPointEvent2
    {
        public const string IID = "E8414A3E-1642-48EC-829E-5F4040E16DA9";
    }

    internal sealed class AD7ThreadCreateEvent : AD7AsynchronousEvent, IDebugThreadCreateEvent2
    {
        public const string IID = "2090CCFC-70C5-491D-A5E8-BAD2DD9EE3EA";
    }

    internal sealed class AD7ThreadDestroyEvent : AD7AsynchronousEvent, IDebugThreadDestroyEvent2
    {
        public const string IID = "2C3B7532-A36F-4A6E-9072-49BE649B8541";
        private readonly uint _exitCode;
        public AD7ThreadDestroyEvent(uint exitCode) { _exitCode = exitCode; }
        int IDebugThreadDestroyEvent2.GetExitCode(out uint exitCode) { exitCode = _exitCode; return AD7Engine.S_OK; }
    }

    internal sealed class AD7StepCompleteEvent : AD7StoppingEvent, IDebugStepCompleteEvent2
    {
        public const string IID = "0F7F24C1-74D9-4EA6-A3EA-7EDB2D81441D";
    }

    internal sealed class AD7BreakpointEvent : AD7StoppingEvent, IDebugBreakpointEvent2
    {
        public const string IID = "501C1E21-C557-48B8-BA30-A1EAB0BC4A74";
        private readonly IEnumDebugBoundBreakpoints2 _boundBreakpoints;
        public AD7BreakpointEvent(IEnumDebugBoundBreakpoints2 boundBreakpoints) { _boundBreakpoints = boundBreakpoints; }
        int IDebugBreakpointEvent2.EnumBreakpoints(out IEnumDebugBoundBreakpoints2 ppEnum) { ppEnum = _boundBreakpoints; return AD7Engine.S_OK; }
    }

    internal sealed class AD7BreakpointBoundEvent : AD7AsynchronousEvent, IDebugBreakpointBoundEvent2
    {
        public const string IID = "1DDDB704-CF99-4B8A-B746-DABB01DD13A0";
        private readonly IDebugPendingBreakpoint2 _pendingBreakpoint;
        private readonly IEnumDebugBoundBreakpoints2 _boundBreakpoints;
        public AD7BreakpointBoundEvent(IDebugPendingBreakpoint2 pending, IEnumDebugBoundBreakpoints2 bound)
        { _pendingBreakpoint = pending; _boundBreakpoints = bound; }
        int IDebugBreakpointBoundEvent2.EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 ppEnum) { ppEnum = _boundBreakpoints; return AD7Engine.S_OK; }
        int IDebugBreakpointBoundEvent2.GetPendingBreakpoint(out IDebugPendingBreakpoint2 ppPendingBP) { ppPendingBP = _pendingBreakpoint; return AD7Engine.S_OK; }
    }

    internal sealed class AD7OutputDebugStringEvent : AD7AsynchronousEvent, IDebugOutputStringEvent2
    {
        public const string IID = "569C4BB1-7B82-46FC-AE28-4536DDAD753E";
        private readonly string _text;
        public AD7OutputDebugStringEvent(string text) { _text = text; }
        int IDebugOutputStringEvent2.GetString(out string pbstrString) { pbstrString = _text; return AD7Engine.S_OK; }
    }
}
