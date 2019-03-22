using System;
using evtx.Tags;

namespace evtx
{
    public class SubstitutionArrayEntry
    {
        public SubstitutionArrayEntry(int position, int size, TagBuilder.ValueType valType)
        {
            Position = position;
            Size = size;
            ValType = valType;
        }

        public int Position { get; }
        public int Size { get; }
        public TagBuilder.ValueType ValType { get; }
        public byte[] DataBytes { get; set; }

        public override string ToString()
        {
            return
                $"Position: {Position.ToString().PadRight(5)} Size: 0x{Size.ToString("X").PadRight(5)}  Type: {ValType} Data bytes: {BitConverter.ToString(DataBytes)}";
        }
    }
}