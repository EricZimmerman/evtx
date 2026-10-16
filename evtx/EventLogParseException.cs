using System;

namespace evtx;

/// <summary>Thrown for data that cannot be used (a bad file header, or BinXml that cannot be parsed); see Offset and Field</summary>
public class EventLogParseException : Exception
{
    public EventLogParseException(string message, long offset = -1, string field = null) : base(message)
    {
        Offset = offset;
        Field = field;
    }

    /// <summary>Byte offset of the problem (in the chunk for BinXml, in the file for the header), or -1 when unknown</summary>
    public long Offset { get; }

    /// <summary>The structure being read</summary>
    public string Field { get; }
}
