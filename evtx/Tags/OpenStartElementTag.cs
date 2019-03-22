using System;
using NLog;

namespace evtx.Tags
{
    public class OpenStartElementTag : IBinXml
    {
        public OpenStartElementTag(long chunkOffset, long recordPosition, int size, byte[] payload)
        {
            var l = LogManager.GetLogger("BuildTag");

            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = size;

            throw new Exception("wtf over");

            var index = 0;

            index += 1; //move past op code

            var dep = BitConverter.ToInt16(payload, index);
            index += 2;

            if (dep != -1)
            {
                //there is a dependency
                l.Debug("There is a dependency");
            }

            var dataSize = BitConverter.ToInt32(payload, index);
            index += 2;
            l.Debug($"datasize is 0x{dataSize:X}");
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