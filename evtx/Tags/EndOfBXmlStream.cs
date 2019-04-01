using System;

namespace evtx.Tags
{
    internal class EndOfBXmlStream : IBinXml
    {
        public EndOfBXmlStream(long chunkOffset, long recordPosition)
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
    }
}