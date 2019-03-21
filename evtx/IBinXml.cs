using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace evtx
{
   public interface IBinXml
    {
        long ChunkOffset { get; }
        long RecordPosition { get; }

        int Size { get; }

        string AsXml();
    }
}
