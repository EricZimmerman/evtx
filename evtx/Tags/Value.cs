using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Serilog;

namespace evtx.Tags;

public class Value : IBinXml
{
    public Value(long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
    {

        RecordPosition = recordPosition;

        ValueDataType = (TagBuilder.ValueType) dataStream.ReadByte();

        Size = dataStream.ReadInt16();

        switch (ValueDataType)
        {
            case TagBuilder.ValueType.StringType:
                ValueData = Encoding.Unicode.GetString(dataStream.ReadBytes((int) (Size * 2)));
                break;
            default:
                throw new ArgumentOutOfRangeException($"Value Type {ValueDataType} is not handled! Handle it!");
        }

        Log.Verbose("{This}",this);
    }

    public string ValueData { get; }
    public ValueType ValueDataType { get; }

    public long RecordPosition { get; }
    public long Size { get; }

    public string AsXml(List<SubstitutionArrayEntry> substitutionEntries, long parentOffset)
    {
        return ValueData;
    }

    public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.Value;

    public override string ToString()
    {
        return $"Type: {ValueDataType}, Value Data: {ValueData}";
    }
}