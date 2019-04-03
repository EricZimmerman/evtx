using evtx.Tags;

namespace evtx
{
    public interface IBinXml
    {
        long RecordPosition { get; }

        long Size { get; }

        string AsXml();

        TagBuilder.BinaryTag TagType { get; }
    }
}