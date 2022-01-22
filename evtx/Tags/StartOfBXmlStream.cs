using System;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace evtx.Tags;

public class StartOfBXmlStream : IBinXml
{
    public StartOfBXmlStream(long recordPosition, BinaryReader dataStream)
    {
        RecordPosition = recordPosition;
        Size = 4;

        MajorVer = dataStream.ReadByte();

        MinorVer = dataStream.ReadByte();

        Flags = dataStream.ReadByte();

        Log.Verbose("Major: {MajorVer} Minor: {MinorVer} Flags: {Flags}",MajorVer,MinorVer,Flags);
    }

    public int MajorVer { get; }
    public int MinorVer { get; }
    public int Flags { get; }

    public long RecordPosition { get; }
    public long Size { get; }

    public string AsXml(List<SubstitutionArrayEntry> substitutionEntries, long parentOffset)
    {
        throw new NotImplementedException();
    }

    public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.StartOfBXmlStream;
}