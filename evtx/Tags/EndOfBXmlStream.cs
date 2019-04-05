using System;

namespace evtx.Tags
{
    internal class EndOfBXmlStream : IBinXml
    {
        public EndOfBXmlStream(long recordPosition)
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

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.EndOfBXmlStream;
    }
}