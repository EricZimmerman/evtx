using System;
using System.Collections.Generic;
using System.IO;
using NLog;

namespace evtx.Tags
{
    internal class TemplateInstance : IBinXml
    {
        public TemplateInstance(long chunkOffset, long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("BuildTag");
            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = 10; //default size until we know better

            SubstitutionEntries = new List<SubstitutionArrayEntry>();

            var startPos = recordPosition + dataStream.BaseStream.Position;
            var origIndex = dataStream.BaseStream.Position;

            var version = dataStream.ReadByte();

            TemplateId = dataStream.ReadInt32();

            TemplateOffset = dataStream.ReadInt32();

            NextTemplateOffset = dataStream.ReadInt32();

            Template = chunk.GetTemplate(TemplateOffset);

            Size = Template.Size + 0x22;
            if (TemplateOffset < startPos)
            {
                //the template has already been defined, so we just need to send back the placeholder size, which is 10
                Size = 10;
            }
        }

        public Template Template { get; }

        public List<SubstitutionArrayEntry> SubstitutionEntries { get; }


        public int TemplateOffset { get; }
        public int NextTemplateOffset { get; }
        public int TemplateId { get; }

        public long ChunkOffset { get; }
        public long RecordPosition { get; }
        public int Size { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }
    }
}