using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace evtx.BinXml;

/// <summary>
///     Substitution and inline values as text. The formats are EvtxECmd's (maps depend on them): times as
///     yyyy-MM-dd HH:mm:ss.fffffff UTC, GUIDs without braces, binary as AA-BB, booleans as True/False, hex as 0x1F
/// </summary>
internal static class ValueFormatter
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fffffff";

    private static readonly long MaxFileTime = DateTime.MaxValue.ToFileTimeUtc();

    private static readonly Encoding Ansi = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    /// <summary>
    ///     A value as text. Inline (ValueText) strings are kept as they are; substituted strings have NULs trimmed and
    ///     runs of control characters replaced with ", "
    /// </summary>
    public static string Format(byte[] b, Value v, bool inline = false)
    {
        var o = v.Offset;
        var n = v.Size;

        switch (v.Type)
        {
            case ValueType.NullType:
                return string.Empty;
            case ValueType.StringType:
                var s = Encoding.Unicode.GetString(b, o, n & ~1);
                return inline ? s : CleanString(s.Trim('\0'));
            case ValueType.AnsiStringType:
                return CleanString(Ansi.GetString(b, o, n).Trim('\0'));
            case ValueType.Int8Type:
                return n < 1 ? string.Empty : ((sbyte)b[o]).ToString(CultureInfo.InvariantCulture);
            case ValueType.UInt8Type:
                return n < 1 ? string.Empty : b[o].ToString(CultureInfo.InvariantCulture);
            case ValueType.Int16Type:
                return n < 2 ? Hex(b, o, n) : BitConverter.ToInt16(b, o).ToString(CultureInfo.InvariantCulture);
            case ValueType.UInt16Type:
                return n < 2 ? Hex(b, o, n) : BitConverter.ToUInt16(b, o).ToString(CultureInfo.InvariantCulture);
            case ValueType.Int32Type:
                return n < 4 ? Hex(b, o, n) : BitConverter.ToInt32(b, o).ToString(CultureInfo.InvariantCulture);
            case ValueType.UInt32Type:
                return n < 4 ? Hex(b, o, n) : BitConverter.ToUInt32(b, o).ToString(CultureInfo.InvariantCulture);
            case ValueType.Int64Type:
                return n < 8 ? Hex(b, o, n) : BitConverter.ToInt64(b, o).ToString(CultureInfo.InvariantCulture);
            case ValueType.UInt64Type:
                return n < 8 ? Hex(b, o, n) : BitConverter.ToUInt64(b, o).ToString(CultureInfo.InvariantCulture);
            case ValueType.Real32Type:
                return n < 4 ? Hex(b, o, n) : BitConverter.ToSingle(b, o).ToString(CultureInfo.InvariantCulture);
            case ValueType.Real64Type:
                return n < 8 ? Hex(b, o, n) : BitConverter.ToDouble(b, o).ToString(CultureInfo.InvariantCulture);
            case ValueType.BoolType:
                return n < 1 ? string.Empty : (b[o] != 0).ToString();
            case ValueType.BinaryType:
                return Hex(b, o, n);
            case ValueType.GuidType:
                return n < 16 ? Hex(b, o, n) : Guid(b, o);
            case ValueType.SizeTType:
                return n switch
                {
                    4 => $"0x{BitConverter.ToUInt32(b, o):X}",
                    8 => $"0x{BitConverter.ToUInt64(b, o):X}",
                    _ => Hex(b, o, n)
                };
            case ValueType.FileTimeType:
                return n < 8 ? Hex(b, o, n) : FileTime(BitConverter.ToInt64(b, o));
            case ValueType.SysTimeType:
                return n < 16 ? Hex(b, o, n) : SystemTime(b, o);
            case ValueType.SidType:
                return Sid(b, o, n, out _);
            case ValueType.HexInt32Type:
                return n < 4 ? Hex(b, o, n) : $"0x{BitConverter.ToInt32(b, o):X}";
            case ValueType.HexInt64Type:
                return n < 8 ? Hex(b, o, n) : $"0x{BitConverter.ToInt64(b, o):X}";
            case ValueType.EvtXml:
            case ValueType.EvtHandle:
                return "UNKNOWN: Please submit to saericzimmerman@gmail.com!";
            case ValueType.BinXmlType:
                return "BinaryXML";
        }

        if (((byte)v.Type & 0x80) != 0)
        {
            return FormatArray(b, v);
        }

        //an unknown type: the bytes
        return Hex(b, o, n);
    }

    /// <summary>Array items, joined as EvtxECmd always has: strings with ", ", everything else with ","</summary>
    private static string FormatArray(byte[] b, Value v)
    {
        var o = v.Offset;
        var end = v.Offset + v.Size;
        var items = new List<string>();

        switch ((ValueType)((byte)v.Type & 0x7F))
        {
            case ValueType.StringType:
                foreach (var item in Encoding.Unicode.GetString(b, o, v.Size & ~1).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    items.Add(item);
                }

                return string.Join(", ", items);
            case ValueType.AnsiStringType:
                foreach (var item in Ansi.GetString(b, o, v.Size).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    items.Add(item);
                }

                return string.Join(", ", items);
            case ValueType.Int8Type:
                for (var i = o; i < end; i++)
                {
                    items.Add(((sbyte)b[i]).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.UInt8Type:
                for (var i = o; i < end; i++)
                {
                    items.Add(b[i].ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.Int16Type:
                for (var i = o; i + 2 <= end; i += 2)
                {
                    items.Add(BitConverter.ToInt16(b, i).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.UInt16Type:
                for (var i = o; i + 2 <= end; i += 2)
                {
                    items.Add(BitConverter.ToUInt16(b, i).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.Int32Type:
                for (var i = o; i + 4 <= end; i += 4)
                {
                    items.Add(BitConverter.ToInt32(b, i).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.UInt32Type:
                for (var i = o; i + 4 <= end; i += 4)
                {
                    items.Add(BitConverter.ToUInt32(b, i).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.Int64Type:
                for (var i = o; i + 8 <= end; i += 8)
                {
                    items.Add(BitConverter.ToInt64(b, i).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.UInt64Type:
                for (var i = o; i + 8 <= end; i += 8)
                {
                    items.Add(BitConverter.ToUInt64(b, i).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.Real32Type:
                for (var i = o; i + 4 <= end; i += 4)
                {
                    items.Add(BitConverter.ToSingle(b, i).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.Real64Type:
                //8 bytes each (this used to step by 4)
                for (var i = o; i + 8 <= end; i += 8)
                {
                    items.Add(BitConverter.ToDouble(b, i).ToString(CultureInfo.InvariantCulture));
                }

                break;
            case ValueType.BoolType:
                for (var i = o; i + 4 <= end; i += 4)
                {
                    items.Add((BitConverter.ToInt32(b, i) != 0).ToString());
                }

                break;
            case ValueType.GuidType:
                for (var i = o; i + 16 <= end; i += 16)
                {
                    items.Add(Guid(b, i));
                }

                break;
            case ValueType.SizeTType:
                //the pointer size of the system that logged it: 8 bytes each unless that does not fit
                var width = v.Size % 8 == 0 ? 8 : 4;
                for (var i = o; i + width <= end; i += width)
                {
                    items.Add(width == 8 ? $"0x{BitConverter.ToUInt64(b, i):X}" : $"0x{BitConverter.ToUInt32(b, i):X}");
                }

                break;
            case ValueType.FileTimeType:
                for (var i = o; i + 8 <= end; i += 8)
                {
                    items.Add(FileTime(BitConverter.ToInt64(b, i)));
                }

                break;
            case ValueType.SysTimeType:
                for (var i = o; i + 16 <= end; i += 16)
                {
                    items.Add(SystemTime(b, i));
                }

                break;
            case ValueType.SidType:
                for (var i = o; i + 8 <= end;)
                {
                    items.Add(Sid(b, i, end - i, out var used));
                    i += used;
                }

                break;
            case ValueType.HexInt32Type:
                for (var i = o; i + 4 <= end; i += 4)
                {
                    items.Add($"0x{BitConverter.ToInt32(b, i):X}");
                }

                break;
            case ValueType.HexInt64Type:
                for (var i = o; i + 8 <= end; i += 8)
                {
                    items.Add($"0x{BitConverter.ToInt64(b, i):X}");
                }

                break;
            default:
                return Hex(b, o, v.Size);
        }

        return string.Join(",", items);
    }

    /// <summary>A FILETIME as UTC, or its raw value when it is not a valid time</summary>
    public static string FileTime(long raw)
    {
        if (raw < 0 || raw > MaxFileTime)
        {
            return $"0x{raw:X16}";
        }

        return new DateTime(DateTime.FromFileTimeUtc(raw).Ticks, DateTimeKind.Utc).ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>The ticks of a FILETIME, or null when it is not valid</summary>
    public static DateTimeOffset? FileTimeValue(long raw)
    {
        if (raw < 0 || raw > MaxFileTime)
        {
            return null;
        }

        return new DateTimeOffset(DateTime.FromFileTimeUtc(raw).Ticks, TimeSpan.Zero);
    }

    private static string SystemTime(byte[] b, int o)
    {
        var t = SystemTimeValue(b, o);
        return t?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? Hex(b, o, 16);
    }

    public static DateTimeOffset? SystemTimeValue(byte[] b, int o)
    {
        var year = BitConverter.ToUInt16(b, o);
        var month = BitConverter.ToUInt16(b, o + 2);
        var day = BitConverter.ToUInt16(b, o + 6);
        var hours = BitConverter.ToUInt16(b, o + 8);
        var minutes = BitConverter.ToUInt16(b, o + 10);
        var seconds = BitConverter.ToUInt16(b, o + 12);
        var milliseconds = BitConverter.ToUInt16(b, o + 14);

        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) ||
            hours > 23 || minutes > 59 || seconds > 59 || milliseconds > 999)
        {
            return null;
        }

        return new DateTimeOffset(year, month, day, hours, minutes, seconds, milliseconds, TimeSpan.Zero);
    }

    /// <summary>A SID (revision, subauthority count, 48 bit big-endian authority, subauthorities)</summary>
    private static string Sid(byte[] b, int o, int n, out int used)
    {
        used = n;
        if (n < 8)
        {
            return Hex(b, o, n);
        }

        var count = b[o + 1];
        var size = 8 + 4 * count;
        if (size > n)
        {
            //fewer subauthorities than the count says: the ones that are there
            count = (byte)((n - 8) / 4);
            size = 8 + 4 * count;
        }

        used = size;

        long authority = 0;
        for (var i = 2; i < 8; i++)
        {
            authority = (authority << 8) | b[o + i];
        }

        var sb = new StringBuilder();
        sb.Append("S-").Append(b[o].ToString(CultureInfo.InvariantCulture)).Append('-').Append(authority.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < count; i++)
        {
            sb.Append('-').Append(BitConverter.ToUInt32(b, o + 8 + i * 4).ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string Guid(byte[] b, int o)
    {
        var g = new byte[16];
        Buffer.BlockCopy(b, o, g, 0, 16);
        return new Guid(g).ToString();
    }

    private static string Hex(byte[] b, int o, int n) => n <= 0 ? string.Empty : BitConverter.ToString(b, o, n);

    /// <summary>Runs of control characters (line breaks, tabs, NULs in multi-strings) become ", "</summary>
    private static string CleanString(string s)
    {
        var start = 0;
        while (start < s.Length && char.IsControl(s[start]) == false)
        {
            start++;
        }

        if (start == s.Length)
        {
            return s;
        }

        var sb = new StringBuilder(s.Length + 8);
        sb.Append(s, 0, start);
        var inRun = false;
        for (var i = start; i < s.Length; i++)
        {
            if (char.IsControl(s[i]))
            {
                if (inRun == false)
                {
                    sb.Append(", ");
                    inRun = true;
                }
            }
            else
            {
                sb.Append(s[i]);
                inRun = false;
            }
        }

        return sb.ToString();
    }
}
