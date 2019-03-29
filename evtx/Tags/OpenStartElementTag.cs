using System;
using System.IO;
using NLog;

namespace evtx.Tags
{
    public class OpenStartElementTag : IBinXml
    {
        public OpenStartElementTag(long chunkOffset, long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("BuildTag");

            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;

            var dependencyId = dataStream.ReadInt16();

            Size = dataStream.ReadInt32();

            //throw new Exception("wtf over");


            if (dependencyId != -1)
            {
                //there is a dependency
                l.Debug("There is a dependency");
            }


            l.Debug($"Size is 0x{Size:X}");
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