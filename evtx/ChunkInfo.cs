using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace evtx
{
   public class ChunkInfo
    {
        public ChunkInfo(byte[] chunkBytes, long offset, int chunkNumber)
        {
            ChunkBytes = chunkBytes;
            Offset = offset;
            ChunkNumber = chunkNumber;

            FirstLogRecprd = BitConverter.ToInt64(chunkBytes, 0x8);
          LastLogRecord= BitConverter.ToInt64(chunkBytes, 0x10);
            FirstFileRecprd = BitConverter.ToInt64(chunkBytes, 0x18);
            LastFileRecprd = BitConverter.ToInt64(chunkBytes, 0x20);
        }

        public long LastFileRecprd { get;  }

        public long FirstFileRecprd { get;  }

        public long LastLogRecord { get;  }

        public long FirstLogRecprd { get;  }

        public byte[] ChunkBytes { get; }
        public long Offset { get; }
        public int ChunkNumber { get; }

        public override string ToString()
        {
            return $"Offset 0x{Offset:X8} Chunk #: {ChunkNumber.ToString().PadRight(5)} FirstLogRecprd: {FirstLogRecprd.ToString().PadRight(8)} LastLogRecord: {LastLogRecord.ToString().PadRight(8)} FirstFileRecprd: {FirstFileRecprd.ToString().PadRight(8)} LastFileRecprd: {LastFileRecprd.ToString().PadRight(8)}";
        }
    }
}
