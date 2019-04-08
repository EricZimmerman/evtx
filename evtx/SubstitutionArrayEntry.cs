using System;
using System.Globalization;
using System.Linq;
using System.Text;
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

        public string GetDataAsString()
        {
            switch (ValType)
            {
                case TagBuilder.ValueType.NullType:
                    return string.Empty;

                case TagBuilder.ValueType.StringType:
                    return Encoding.Unicode.GetString(DataBytes).Trim('\0');
                case TagBuilder.ValueType.AnsiStringType:
                    return Encoding.GetEncoding(1252).GetString(DataBytes).Trim('\0');
                case TagBuilder.ValueType.Int8Type:
                    return ((sbyte) DataBytes[0]).ToString();
                case TagBuilder.ValueType.UInt8Type:
                    return DataBytes[0].ToString();
                case TagBuilder.ValueType.Int16Type:
                    return BitConverter.ToInt16(DataBytes, 0).ToString();
                case TagBuilder.ValueType.UInt16Type:
                    return BitConverter.ToUInt16(DataBytes, 0).ToString();
                case TagBuilder.ValueType.Int32Type:
                    return BitConverter.ToInt32(DataBytes, 0).ToString();
                case TagBuilder.ValueType.UInt32Type:
                    return BitConverter.ToUInt32(DataBytes, 0).ToString();
                case TagBuilder.ValueType.Int64Type:
                    return BitConverter.ToInt64(DataBytes, 0).ToString();
                case TagBuilder.ValueType.UInt64Type:
                    return BitConverter.ToUInt64(DataBytes, 0).ToString();
                case TagBuilder.ValueType.Real32Type:
                    return BitConverter.ToSingle(DataBytes, 0).ToString(CultureInfo.InvariantCulture);
                case TagBuilder.ValueType.Real64Type:
                    return BitConverter.ToDouble(DataBytes, 0).ToString(CultureInfo.InvariantCulture);
                case TagBuilder.ValueType.BoolType:
                    return BitConverter.ToBoolean(DataBytes, 0).ToString();
                case TagBuilder.ValueType.BinaryType:
                    return BitConverter.ToString(DataBytes);
                case TagBuilder.ValueType.GuidType:
                    var g = new Guid(DataBytes);
                    return g.ToString();

                case TagBuilder.ValueType.SizeTType:
                    return BitConverter.ToString(DataBytes);

                case TagBuilder.ValueType.FileTimeType:
                    var fts = DateTimeOffset.FromFileTime(BitConverter.ToInt64(DataBytes, 0)).ToUniversalTime();
                    return fts.ToString("yyyy-MM-dd HH:mm:ss.fffffff");

                case TagBuilder.ValueType.SysTimeType:
                    var year = BitConverter.ToUInt16(DataBytes, 0);
                    var month = BitConverter.ToUInt16(DataBytes, 1);
                    var dayOfWeek = BitConverter.ToUInt16(DataBytes, 2);
                    var day = BitConverter.ToUInt16(DataBytes, 3);
                    var hours = BitConverter.ToUInt16(DataBytes, 4);
                    var mins = BitConverter.ToUInt16(DataBytes, 5);
                    var secs = BitConverter.ToUInt16(DataBytes, 6);
                    var msecs = BitConverter.ToUInt16(DataBytes, 7);

                    var sts = new DateTimeOffset(year, month, day, hours, mins, secs, msecs, TimeSpan.Zero);

                    return sts.ToString("yyyy-MM-dd HH:mm:ss.fffffff");

                case TagBuilder.ValueType.SidType:
                    return ConvertHexStringToSidString(DataBytes);

                case TagBuilder.ValueType.HexInt32Type:
                case TagBuilder.ValueType.HexInt64Type:
                    return BitConverter.ToString(DataBytes);

                case TagBuilder.ValueType.EvtXml:
                case TagBuilder.ValueType.EvtHandle:
                    return "UNKNOWN: Please submit to saericzimmerman@gmail.com!";


                case TagBuilder.ValueType.BinXmlType:
                    return "BinaryXML";

                case TagBuilder.ValueType.ArrayUnicodeString:
                    var tsu = Encoding.Unicode.GetString(DataBytes).Split(new [] {'\0'},StringSplitOptions.RemoveEmptyEntries);
                    return string.Join(", ", tsu).Trim('\0');

                case TagBuilder.ValueType.ArrayAsciiString:
                    var tsa = Encoding.GetEncoding(1252).GetString(DataBytes).Split(new [] {'\0'},StringSplitOptions.RemoveEmptyEntries);
                    return string.Join(", ", tsa).Trim('\0');

                case TagBuilder.ValueType.Array8BitIntSigned:
                case TagBuilder.ValueType.Array8BitIntUnsigned:
                case TagBuilder.ValueType.Array16BitIntSigned:
                case TagBuilder.ValueType.Array16BitIntUnsigned:
                case TagBuilder.ValueType.Array32BitIntSigned:
                case TagBuilder.ValueType.Array32BitIntUnsigned:
                case TagBuilder.ValueType.Array64BitIntSigned:
                case TagBuilder.ValueType.Array64BitIntUnsigned:
                case TagBuilder.ValueType.ArrayFloat32Bit:
                case TagBuilder.ValueType.ArrayFloat64Bit:
                case TagBuilder.ValueType.ArrayBool:
                case TagBuilder.ValueType.ArrayGuid:
                case TagBuilder.ValueType.ArraySizeType:
                case TagBuilder.ValueType.ArrayFileTime:
                case TagBuilder.ValueType.ArraySystemTime:
                case TagBuilder.ValueType.ArraySids:
                case TagBuilder.ValueType.Array32BitHex:
                case TagBuilder.ValueType.Array64BitHex:

                default:
                    throw new ArgumentOutOfRangeException($"ValType: {ValType}");
            }
        }

        private static string ConvertHexStringToSidString(byte[] hex)
        {
            const string header = "S";

            var sidVersion = hex[0].ToString();
            var authId = BitConverter.ToInt32(hex.Skip(4).Take(4).Reverse().ToArray(), 0);
            var index = 8;

            var sid = $"{header}-{sidVersion}-{authId}";

            do
            {
                var tempAuthHex = hex.Skip(index).Take(4).ToArray();

                var tempAuth = BitConverter.ToUInt32(tempAuthHex, 0);

                index += 4;

                sid = $"{sid}-{tempAuth}";
            } while (index < hex.Length);

            return sid;
        }

        public override string ToString()
        {
            var vdb = string.Empty;
            if (ValType != TagBuilder.ValueType.BinXmlType && ValType != TagBuilder.ValueType.NullType &&
                ValType != TagBuilder.ValueType.StringType)
            {
                vdb = $" : Data bytes: {BitConverter.ToString(DataBytes)}";
            }

            return
                $"Position: {Position.ToString().PadRight(5)} Size: 0x{Size.ToString("X").PadRight(5)}  Type: {ValType.ToString().PadRight(15)} Value: : {GetDataAsString().PadRight(50)}{vdb}";
        }
    }
}