using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace evtx
{
   public class EventRecord
    {
        public EventRecord(byte[] recordBytes)
        {
            Size = BitConverter.ToUInt32(recordBytes, 4);
            RecordNumber = BitConverter.ToInt64(recordBytes, 8);
            Timestamp = DateTimeOffset.FromFileTime(BitConverter.ToInt64(recordBytes, 0x10)).ToUniversalTime();

            PayloadBytes = new byte[recordBytes.Length - 28]; //4 for signature, 4 for first size, 8 for record #, 8 for timestamp, 4 for last size
            Buffer.BlockCopy(recordBytes,0x18,PayloadBytes,0,PayloadBytes.Length);
        }


        public uint Size { get; }
        public long RecordNumber { get; }
        public DateTimeOffset Timestamp { get; }

        public byte[] PayloadBytes { get; }

        public override string ToString()
        {
            return $"RecordNumber: 0x{RecordNumber:X} ({RecordNumber}) Timestamp: {Timestamp}";
        }
    }
}
