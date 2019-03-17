using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace evtx
{
   public class ChunkInfo
    {
        public ChunkInfo(byte[] chunkBytes, long offset, int chunkNumber)
        {
            ChunkBytes = chunkBytes;
            Offset = offset;
            ChunkNumber = chunkNumber;

            FirstLogRecord = BitConverter.ToInt64(chunkBytes, 0x8);
            LastLogRecord= BitConverter.ToInt64(chunkBytes, 0x10);
            FirstFileRecord = BitConverter.ToInt64(chunkBytes, 0x18);
            LastFileRecord = BitConverter.ToInt64(chunkBytes, 0x20);

            var tableOffset = BitConverter.ToUInt32(chunkBytes, 0x28);
            var lastRecordOffset = BitConverter.ToUInt32(chunkBytes, 0x2C);
            var nextRecordOffset = BitConverter.ToUInt32(chunkBytes, 0x30);
            Crc = BitConverter.ToInt32(chunkBytes, 0x7c);

            var index = 0;
            var tableData = new byte[0x100];
            Buffer.BlockCopy(chunkBytes,(int) tableOffset,tableData,0,0x100);

            StringTableEntries = new List<StringTableEntry>();

            var stringOffsets = new List<uint>();

            while (index<tableData.Length)
            {
                var stringOffset = BitConverter.ToUInt32(tableData, index);
                index += 4;
                if (stringOffset == 0)
                {
                    continue;
                }

                stringOffsets.Add(stringOffset);
            }

            foreach (var stringOffset in stringOffsets)
            {
                index = (int) stringOffset+4;
                var hash = BitConverter.ToUInt16(chunkBytes, index);
                index += 2;
                var stringLen = BitConverter.ToUInt16(chunkBytes, index);
                index += 2;
                var stringVal = Encoding.Unicode.GetString(chunkBytes, index, stringLen * 2);

                StringTableEntries.Add(new StringTableEntry(stringOffset, hash, stringVal));
            }

            var templateTableData = new byte[0x80];
            Buffer.BlockCopy(chunkBytes,(int) tableOffset + 0x100,templateTableData,0,0x80);

            var tableTemplateOffsets = new List<uint>();

            index = 0;
            while (index<templateTableData.Length)
            {
                var templateOffset = BitConverter.ToUInt32(templateTableData, index);
                index += 4;
                if (templateOffset == 0)
                {
                    continue;
                }

                tableTemplateOffsets.Add(templateOffset);
            }

            var l = LogManager.GetLogger("ChunkInfo");

            //l.Info(tableTemplateOffsets.Count);

            index = (int) tableOffset + 0x100 + 0x80; //get to start of event Records

            EventRecords = new List<EventRecord>();

            var recordSig = 0x2a2a;
            while (index<chunkBytes.Length)
            {
                var sig = BitConverter.ToInt32(chunkBytes, index);
              
                if (sig != recordSig)
                {
                    break;
                }

                var recordSize = BitConverter.ToUInt32(chunkBytes, index + 4);
                var recordBuff = new byte[recordSize];
                Buffer.BlockCopy(chunkBytes,index,recordBuff,0,(int) recordSize);
                index += (int)recordSize;

                var er = new EventRecord(recordBuff);

                EventRecords.Add(er);
            }

        }

        public List<EventRecord> EventRecords { get;  }

        public List<StringTableEntry> StringTableEntries { get; }

        public int Crc { get;  }

        public long LastFileRecord { get;  }

        public long FirstFileRecord { get;  }

        public long LastLogRecord { get;  }

        public long FirstLogRecord { get;  }

        public byte[] ChunkBytes { get; }
        public long Offset { get; }
        public int ChunkNumber { get; }

        public override string ToString()
        {
            return $"Offset 0x{Offset:X8} Chunk #: {ChunkNumber.ToString().PadRight(5)} FirstLogRecord: {FirstLogRecord.ToString().PadRight(8)} LastLogRecord: {LastLogRecord.ToString().PadRight(8)} FirstFileRecord: {FirstFileRecord.ToString().PadRight(8)} LastFileRecord: {LastFileRecord.ToString().PadRight(8)}";
        }
    }
}
