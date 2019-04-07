namespace evtx
{
    public class StringTableEntry
    {
        public StringTableEntry(uint offset, ushort hash, string value, int size)
        {
            Offset = offset;
            Hash = hash;
            Value = value;
            Size = size;
        }

        public uint Offset { get; }
        public int Size { get; }
        public ushort Hash { get; }
        public string Value { get; }

        public override string ToString()
        {
            return $"Offset: 0x{Offset:X4} Hash: 0x{Hash:X4} Value: {Value}";
        }
    }
}