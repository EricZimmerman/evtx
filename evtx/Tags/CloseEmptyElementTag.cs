using System;

namespace evtx.Tags
{
    public class CloseEmptyElementTag : IBinXml
    {
        public CloseEmptyElementTag(long chunkOffset, long recordPosition)
        {
            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = 1;
        }

        public long ChunkOffset { get; }
        public long RecordPosition { get; }
        public long Size { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.CloseEmptyElementTag;
    }
}