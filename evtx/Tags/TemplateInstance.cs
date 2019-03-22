using System;
using System.Collections.Generic;
using NLog;

namespace evtx.Tags
{
    internal class TemplateInstance : IBinXml
    {
        public TemplateInstance(long chunkOffset, long recordPosition, int index, byte[] payload,
            Dictionary<int, Template> templates)
        {
            var l = LogManager.GetLogger("BuildTag");
            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = 10; //default size until we know better

            SubstitutionEntries = new List<SubstitutionArrayEntry>();

            var startPos = recordPosition + index;
            var origIndex = index;

            index += 1; //move past op code
            var version = payload[index];
            index += 1;

            TemplateId = BitConverter.ToInt32(payload, index);
            index += 4;

            TemplateOffset = BitConverter.ToInt32(payload, index);
            index += 4;

            NextTemplateOffset = BitConverter.ToInt32(payload, index);
            index += 4;

            Template = templates[TemplateOffset];

            Size = Template.Size + 0x22;
            if (TemplateOffset < startPos)
            {
                //the template has already been defined, so we just need to send back the placeholder size, which is 10
                Size = 10;
            }
        }

        public Template Template { get; }

        public  List<SubstitutionArrayEntry> SubstitutionEntries { get; }


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