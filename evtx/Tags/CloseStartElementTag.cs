using System;

namespace evtx.Tags
{
    public class CloseStartElementTag : IBinXml
    {
        public CloseStartElementTag(long recordPosition)
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

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.CloseStartElementTag;
    }
}