using System;
using System.Collections.Generic;
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

            Nodes = new List<IBinXml>();

            l.Debug($"stream pos at start: 0x{dataStream.BaseStream.Position:X}");

            var dependencyId = dataStream.ReadInt16();

            l.Debug($"dependencyId: 0x{dependencyId:X}");

            Size = dataStream.ReadInt32();
            l.Debug($"OpenStartElementTag size: 0x{Size:X}");
            var startPos = dataStream.BaseStream.Position;
            var elementOffset = dataStream.ReadUInt32();

            var elementName = chunk.GetStringTableEntry(elementOffset);

            l.Debug($"Name: {elementName.Value}");
            l.Debug($"stream pos: 0x{dataStream.BaseStream.Position:X}");

            dataStream.BaseStream.Seek(elementName.Size, SeekOrigin.Current);

            if (dependencyId != -1)
            {
                var attrSize = dataStream.ReadInt32();
                var a = TagBuilder.BuildTag(chunkOffset, recordPosition, dataStream, chunk);
            }

            var i = TagBuilder.BuildTag(chunkOffset, recordPosition, dataStream, chunk);

            l.Debug(i);

            while (dataStream.BaseStream.Position<startPos+Size)
            {
                

            }

            

            


        }

        public List<IBinXml> Nodes { get; set; }

        public long ChunkOffset { get; }
        public long RecordPosition { get; }
        public long Size { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.OpenStartElementTag;
    }
}