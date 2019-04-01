namespace evtx
{
    public interface IBinXml
    {
        long ChunkOffset { get; }
        long RecordPosition { get; }

        long Size { get; }

        string AsXml();
    }
}