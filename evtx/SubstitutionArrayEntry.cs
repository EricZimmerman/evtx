using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            var index = 0;
            switch (ValType)
            {
                case TagBuilder.ValueType.NullType:
                    return string.Empty;

                case TagBuilder.ValueType.StringType:
                    var s = Encoding.Unicode.GetString(DataBytes).Trim('\0');
                    s = Regex.Replace(s, @"\p{C}+", ", ");
                    return s;
                case TagBuilder.ValueType.AnsiStringType:
                    var sa = Encoding.GetEncoding(1252).GetString(DataBytes).Trim('\0');
                    sa = Regex.Replace(sa, @"\p{C}+", ", ");
                    return sa;
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

                    var sts = GetSystemTime(DataBytes);

                    return sts.ToString("yyyy-MM-dd HH:mm:ss.fffffff");

                case TagBuilder.ValueType.SidType:
                    return ConvertHexStringToSidString(DataBytes);

                case TagBuilder.ValueType.HexInt32Type:
                    return $"0x{BitConverter.ToInt32(DataBytes, 0):X}";
                case TagBuilder.ValueType.HexInt64Type:
                    return $"0x{BitConverter.ToInt64(DataBytes, 0):X}";

                case TagBuilder.ValueType.EvtXml:
                case TagBuilder.ValueType.EvtHandle:
                    return "UNKNOWN: Please submit to saericzimmerman@gmail.com!";

                case TagBuilder.ValueType.BinXmlType:
                    return "BinaryXML";

                case TagBuilder.ValueType.ArrayUnicodeString:
                    var tsu = Encoding.Unicode.GetString(DataBytes)
                        .Split(new[] {'\0'}, StringSplitOptions.RemoveEmptyEntries);
                    return string.Join(", ", tsu).Trim('\0');

                case TagBuilder.ValueType.ArrayAsciiString:
                    var tsa = Encoding.GetEncoding(1252).GetString(DataBytes)
                        .Split(new[] {'\0'}, StringSplitOptions.RemoveEmptyEntries);
                    return string.Join(", ", tsa).Trim('\0');

                case TagBuilder.ValueType.Array64BitIntSigned:
                    var a64i = new List<long>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var ul = BitConverter.ToInt64(DataBytes, index);
                        index += 8;
                        a64i.Add(ul);
                    }

                    return string.Join(",", a64i);

                case TagBuilder.ValueType.Array64BitIntUnsigned:
                    var a64ui = new List<ulong>();

                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var ul = BitConverter.ToUInt64(DataBytes, index);
                        index += 8;
                        a64ui.Add(ul);
                    }

                    return string.Join(",", a64ui);

                case TagBuilder.ValueType.Array16BitIntUnsigned:
                    var a16ui = new List<ushort>();

                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var ul = BitConverter.ToUInt16(DataBytes, index);
                        index += 2;
                        a16ui.Add(ul);
                    }

                    return string.Join(",", a16ui);

                case TagBuilder.ValueType.Array16BitIntSigned:
                    var a16i = new List<short>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var ul = BitConverter.ToInt16(DataBytes, index);
                        index += 2;
                        a16i.Add(ul);
                    }

                    return string.Join(",", a16i);

                case TagBuilder.ValueType.Array32BitIntSigned:
                    var a32i = new List<int>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var ul = BitConverter.ToInt32(DataBytes, index);
                        index += 4;
                        a32i.Add(ul);
                    }

                    return string.Join(",", a32i);
                case TagBuilder.ValueType.Array32BitIntUnsigned:
                    var a32ui = new List<uint>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var ul = BitConverter.ToUInt32(DataBytes, index);
                        index += 4;
                        a32ui.Add(ul);
                    }

                    return string.Join(",", a32ui);

                case TagBuilder.ValueType.Array8BitIntSigned:
                    var sb = new List<sbyte>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        sb.Add((sbyte) DataBytes[index]);
                        index += 1;
                    }

                    return string.Join(",", sb);

                case TagBuilder.ValueType.Array8BitIntUnsigned:
                    var b = new List<byte>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        b.Add(DataBytes[index]);
                        index += 1;
                    }

                    return string.Join(",", b);

                case TagBuilder.ValueType.ArrayFloat32Bit:
                    var sl = new List<float>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var sn = BitConverter.ToSingle(DataBytes, index);
                        index += 4;
                        sl.Add(sn);
                    }

                    return string.Join(",", sl);

                case TagBuilder.ValueType.ArrayFloat64Bit:
                    var dl = new List<double>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var dn = BitConverter.ToDouble(DataBytes, index);
                        index += 4;
                        dl.Add(dn);
                    }

                    return string.Join(",", dl);

                case TagBuilder.ValueType.ArrayBool:
                    var boo = new List<bool>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var bv = BitConverter.ToInt32(DataBytes, index) != 0;
                        boo.Add(bv);
                        index += 4;
                    }

                    return string.Join(",", boo);

                case TagBuilder.ValueType.ArrayFileTime:
                    var dto = new List<string>();
                    index = 0;
                    while (index < DataBytes.Length)
                    {
                        var ts = DateTime.FromFileTime(BitConverter.ToInt64(DataBytes, index)).ToUniversalTime();
                        dto.Add(ts.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
                        index += 8;
                    }

                    return string.Join(",", dto);

                case TagBuilder.ValueType.ArraySystemTime:
                    var st = new List<string>();
                    index = 0;

                    while (index < DataBytes.Length)
                    {
                        var stb = new byte[16];
                        Buffer.BlockCopy(DataBytes, index, stb, 0, 16);
                        index += 16;

                        var nst = GetSystemTime(stb).ToUniversalTime();

                        st.Add(nst.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
                    }

                    return string.Join(",", st);

                case TagBuilder.ValueType.ArrayGuid:
                    var gl = new List<string>();
                    index = 0;

                    while (index < DataBytes.Length)
                    {
                        var gb = new byte[16];
                        Buffer.BlockCopy(DataBytes, index, gb, 0, 16);
                        index += 16;
                        var ng = new Guid(gb);
                        gl.Add(ng.ToString());
                    }

                    return string.Join(",", gl);
                case TagBuilder.ValueType.ArraySizeType:

                case TagBuilder.ValueType.ArraySids:
                case TagBuilder.ValueType.Array32BitHex:
                case TagBuilder.ValueType.Array64BitHex:

                default:
                    throw new ArgumentOutOfRangeException(
                        $"When converting substitution array entry to string, ran into unknown Value type: {ValType}. Please submit to saericzimmerman@gmail.com!");
            }
        }

        private DateTimeOffset GetSystemTime(byte[] dataBytes)
        {
            var year = BitConverter.ToUInt16(dataBytes, 0);
            var month = BitConverter.ToUInt16(dataBytes, 2);
            var dayOfWeek = BitConverter.ToUInt16(dataBytes, 4);
            var day = BitConverter.ToUInt16(dataBytes, 6);
            var hours = BitConverter.ToUInt16(dataBytes, 8);
            var mins = BitConverter.ToUInt16(dataBytes, 10);
            var secs = BitConverter.ToUInt16(dataBytes, 12);
            var msecs = BitConverter.ToUInt16(dataBytes, 14);

            var sts = new DateTimeOffset(year, month, day, hours, mins, secs, msecs, TimeSpan.Zero);
            return sts;
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