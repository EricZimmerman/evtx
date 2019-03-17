using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Exception = System.Exception;

//TODO rename project to EventLog?
namespace evtx
{
    public class EventLog
    {
        private static Logger _logger = LogManager.GetLogger("EventLog");

        public long NextRecordId { get; }

        //TODO install alphafs and replace all system.io refs
        public EventLog(Stream fileStream)
        {
            const long evtxSignature = 0x00656c6946666c45;
            const long chunkSignature = 0x6B6E6843666C45;

            
            var headerBytes = new byte[4096];
            fileStream.Read(headerBytes, 0, 4096);

            if (BitConverter.ToInt64(headerBytes, 0) != evtxSignature)
            {
                throw new Exception("Invalid signature! Expected 'ElfFile'");
            }

            CurrentChunk = BitConverter.ToInt64(headerBytes, 0x10);
            NextRecordId = BitConverter.ToInt64(headerBytes, 0x18);
            var size = BitConverter.ToInt32(headerBytes, 0x20);
            Revision = BitConverter.ToInt16(headerBytes, 0x24);
            Version = BitConverter.ToInt16(headerBytes, 0x26);
            var headerSize = BitConverter.ToInt16(headerBytes, 0x28);
            ChunkCount = BitConverter.ToInt16(headerBytes, 0x2A);
          
            IsDirty = BitConverter.ToInt16(headerBytes, 0x78);
            IsLogFull = BitConverter.ToInt16(headerBytes, 0x80);

            Crc = BitConverter.ToInt32(headerBytes, 0x7C);

            //we are at offset 0x1000 and ready to start

            //chunk size == 65536, or 0x10000

            var chunkBuffer = new byte[0x10000];

            Chunks = new List<ChunkInfo>();

            var chunkOffset = fileStream.Position;
            var bytesRead = fileStream.Read(chunkBuffer, 0, 0x10000);
            
            var chunkNumber = 0;
            while (bytesRead > 0)
            {
                var chunkSig = BitConverter.ToInt64(chunkBuffer, 0);

                _logger.Trace($"chunk offset: {chunkOffset}, sig: {Encoding.ASCII.GetString(chunkBuffer, 0, 8)} signature val: 0x{chunkSig:X}");
             
                if (chunkSig == chunkSignature)
                {
                    Chunks.Add(new ChunkInfo(chunkBuffer,chunkOffset,chunkNumber));
                }
                else
                {
                    _logger.Trace($"Skipping chunk at 0x{chunkOffset:X} as it does not have correct signature");
                }

                chunkOffset = fileStream.Position;
                bytesRead = fileStream.Read(chunkBuffer, 0, 0x10000);

                chunkNumber += 1;
            }

           
       

        }

        public List<ChunkInfo> Chunks { get; }

        public long CurrentChunk { get;  }

        public short ChunkCount { get;  }

        public int Crc { get;  }

        public short IsDirty { get;  }
        public short IsLogFull { get;  }

        public short Revision { get;  }
        public short Version { get;  }
    }
}
