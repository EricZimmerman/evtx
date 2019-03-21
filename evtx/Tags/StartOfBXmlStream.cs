using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace evtx.Tags
{
    public class StartOfBXmlStream:IBinXml
    {
        public byte MajorVer{ get; }
        public byte MinorVer{ get; }
        public byte Flags{ get; }

        public StartOfBXmlStream(long chunkOffset, long recordPosition, int size, byte[] payload)
        {
            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = size;

            var   index =0;
            MajorVer = payload[index];
            index += 1;
            MinorVer = payload[index];
            index += 1;
            Flags = payload[index];
         
            var l = LogManager.GetLogger("BuildTag");
            l.Trace($"Major: {MajorVer} Minor: {MinorVer} Flags: {Flags}");
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
