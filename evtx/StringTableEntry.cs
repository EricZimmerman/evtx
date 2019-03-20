using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace evtx
{
   public class StringTableEntry
    {
        public StringTableEntry(uint offset, ushort hash, string value)
        {
            Offset = offset;
            Hash = hash;
            Value = value;
        }

        public uint Offset { get; }
        public ushort Hash { get; }
        public string Value { get; }

        public override string ToString()
        {
            return $"RecordPosition: 0x{Offset:X} Hash: 0x{Hash:X} Value: {Value}";
        }
    }
}
