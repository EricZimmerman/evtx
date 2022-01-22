using System.Collections.Generic;

namespace evtx.Tags;

public class CloseEmptyElementTag : IBinXml
{
    public CloseEmptyElementTag(long recordPosition)
    {
        RecordPosition = recordPosition;
        Size = 1;
    }

    public long RecordPosition { get; }
    public long Size { get; }

    public string AsXml(List<SubstitutionArrayEntry> substitutionEntries, long parentOffset)
    {
        return "/>";
    }

    public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.CloseEmptyElementTag;
}