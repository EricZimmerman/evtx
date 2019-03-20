using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace evtx
{
   public interface IBinXml
    {
        int ChunkOffset { get; }
        int RecordPosition { get; }

        int Size { get; }


    }
}
