namespace evtx
{
    public interface IBinXml
    {
        long ChunkOffset { get; }
        long RecordPosition { get; }

        int Size { get; }

        string AsXml();
    }
}