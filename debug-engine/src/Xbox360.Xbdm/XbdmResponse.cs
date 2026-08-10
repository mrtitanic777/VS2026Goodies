using System;
using System.Collections.Generic;

namespace Xbox360.Xbdm
{
    /// <summary>Well-known XBDM status codes (FTP-style 3-digit codes).</summary>
    public enum XbdmStatus
    {
        Ok = 200,                 // single-line success
        Connected = 201,          // greeting sent by the console on connect
        MultilineResponse = 202,  // multi-line text follows, terminated by a "." line
        BinaryResponse = 203,     // binary payload follows (length-prefixed by command)
        SendBinaryData = 204,     // console is ready to receive binary data
        Ready = 205,
        DedicatedConnection = 206,

        UndefinedError = 400,
        MaxConnectionsExceeded = 401,
        FileNotFound = 402,
        NoSuchModule = 403,
        MemoryNotMapped = 404,
        NoSuchThread = 405,
        FailedClockSet = 406,
        UnknownCommand = 407,
        NotStopped = 408,
        FileMustBeCopied = 409,
        FileAlreadyExists = 410,
        DirectoryNotEmpty = 411,
        FilenameInvalid = 412,
        FileCannotBeCreated = 413,
        AccessDenied = 414,
        NoRoomOnDevice = 415,
        NotDebuggable = 416,
        TypeInvalid = 417,
        DataNotAvailable = 418,
        BoxNotLocked = 420,
        KeyExchangeRequired = 421,
        DedicatedConnectionRequired = 422
    }

    /// <summary>A parsed XBDM response: the status code, its inline message, and any
    /// multi-line body lines (for 202 responses).</summary>
    public sealed class XbdmResponse
    {
        public int Code { get; }
        public string Message { get; }
        public IReadOnlyList<string> Lines { get; }
        public byte[] Binary { get; }

        public XbdmStatus Status => (XbdmStatus)Code;
        public bool IsSuccess => Code >= 200 && Code < 300;
        public bool IsError => Code >= 400;

        public XbdmResponse(int code, string message, IReadOnlyList<string> lines = null, byte[] binary = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            Lines = lines ?? Array.Empty<string>();
            Binary = binary;
        }

        public override string ToString() => $"{Code} {Message}" + (Lines.Count > 0 ? $" (+{Lines.Count} lines)" : "");
    }
}
