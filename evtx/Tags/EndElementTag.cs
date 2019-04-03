using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace evtx.Tags
{
    class EndElementTag:IBinXml
    {
        public EndElementTag( long recordPosition)
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

        public TagBuilder.BinaryTag TagType { get; }
    }
}
