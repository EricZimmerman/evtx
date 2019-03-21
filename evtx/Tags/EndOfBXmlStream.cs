using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace evtx.Tags
{
    class EndOfBXmlStream:IBinXml
    {
        public EndOfBXmlStream(long chunkOffset, long recordPosition)
        {
            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = 1;
        }

        public long ChunkOffset { get; }
        public long RecordPosition { get; }
        public int Size { get; }
        public string AsXml()
        {
            throw new NotImplementedException();
        }
    }
}
