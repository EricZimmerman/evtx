using System;

namespace evtx.Tags
{
    public class EndElementTag : IBinXml
    {
        public EndElementTag(long recordPosition)
        {
            RecordPosition = recordPosition;
            Size = 1;
        }

        public long RecordPosition { get; }
        public long Size { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.EndElementTag;
    }
}