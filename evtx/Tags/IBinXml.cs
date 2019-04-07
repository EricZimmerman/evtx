using System.Collections.Generic;
using evtx.Tags;

namespace evtx
{
    public interface IBinXml
    {
        long RecordPosition { get; }

        long Size { get; }

        TagBuilder.BinaryTag TagType { get; }

        string AsXml(List<SubstitutionArrayEntry> substitutionEntries);
    }
}