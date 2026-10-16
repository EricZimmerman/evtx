using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Force.Crc32;

namespace evtx.Test;

/// <summary>Builds evtx files: a file header and chunks of records with BinXml written token by token</summary>
public sealed class EvtxBuilder
{
    private readonly List<ChunkBuilder> _chunks = new();

    public ChunkBuilder Chunk()
    {
        var chunk = new ChunkBuilder();
        _chunks.Add(chunk);
        return chunk;
    }

    public byte[] Build()
    {
        var ms = new MemoryStream();
        var header = new byte[0x1000];
        Encoding.ASCII.GetBytes("ElfFile\0").CopyTo(header, 0);
        BitConverter.GetBytes((long)(_chunks.Count - 1)).CopyTo(header, 0x10);
        BitConverter.GetBytes(0x80).CopyTo(header, 0x20);
        BitConverter.GetBytes((short)1).CopyTo(header, 0x24);
        BitConverter.GetBytes((short)3).CopyTo(header, 0x26);
        BitConverter.GetBytes((short)0x1000).CopyTo(header, 0x28);
        BitConverter.GetBytes((short)_chunks.Count).CopyTo(header, 0x2A);
        BitConverter.GetBytes(Crc32Algorithm.Compute(header, 0, 0x78)).CopyTo(header, 0x7C);
        ms.Write(header, 0, header.Length);
        foreach (var c in _chunks)
        {
            var b = c.Build();
            ms.Write(b, 0, b.Length);
        }

        return ms.ToArray();
    }
}

public sealed class ChunkBuilder
{
    private readonly byte[] _b = new byte[0x10000];
    private readonly Dictionary<string, int> _names = new();
    private readonly List<long> _numbers = new();
    private int _pos = 0x200;
    private int _lastRecord;

    public int Position => _pos;

    /// <summary>A record: its number, time and BinXml (written at the record's place in the chunk)</summary>
    public ChunkBuilder Record(long number, DateTime time, Action<ChunkBuilder> binXml, Action<ChunkBuilder> slack = null)
    {
        var start = WriteRecord(number, time, binXml, slack);
        _lastRecord = start;
        _numbers.Add(number);
        return this;
    }

    /// <summary>A record written in place (e.g. in another record's slack) that the chunk header does not count</summary>
    public ChunkBuilder HiddenRecord(long number, DateTime time, Action<ChunkBuilder> binXml)
    {
        WriteRecord(number, time, binXml, null);
        return this;
    }

    private int WriteRecord(long number, DateTime time, Action<ChunkBuilder> binXml, Action<ChunkBuilder> slack)
    {
        var start = _pos;
        U32(0x00002A2A);
        U32(0); //size, patched
        U64((ulong)number);
        U64((ulong)time.ToFileTimeUtc());
        binXml(this);
        slack?.Invoke(this);
        //the size copy, 8 byte aligned
        while ((_pos + 4 - start) % 8 != 0)
        {
            Byte(0);
        }

        var size = _pos + 4 - start;
        U32((uint)size);
        BitConverter.GetBytes(size).CopyTo(_b, start + 4);
        return start;
    }

    public byte[] Build()
    {
        Encoding.ASCII.GetBytes("ElfChnk\0").CopyTo(_b, 0);
        if (_numbers.Count > 0)
        {
            BitConverter.GetBytes(_numbers[0]).CopyTo(_b, 0x08);
            BitConverter.GetBytes(_numbers[^1]).CopyTo(_b, 0x10);
            BitConverter.GetBytes(_numbers[0]).CopyTo(_b, 0x18);
            BitConverter.GetBytes(_numbers[^1]).CopyTo(_b, 0x20);
        }
        else
        {
            BitConverter.GetBytes(-1L).CopyTo(_b, 0x18);
        }

        BitConverter.GetBytes(0x80).CopyTo(_b, 0x28);
        BitConverter.GetBytes(_lastRecord).CopyTo(_b, 0x2C);
        BitConverter.GetBytes(_pos).CopyTo(_b, 0x30);
        BitConverter.GetBytes(Crc32Algorithm.Compute(_b, 0x200, _pos - 0x200)).CopyTo(_b, 0x34);
        var crc = Crc32Algorithm.Compute(_b, 0, 0x78);
        crc = Crc32Algorithm.Append(crc, _b, 0x80, 0x180);
        BitConverter.GetBytes(crc).CopyTo(_b, 0x7C);
        return (byte[])_b.Clone();
    }

    // ===== BinXml tokens =====

    public ChunkBuilder FragmentHeader()
    {
        Byte(0x0F);
        Byte(1);
        Byte(1);
        Byte(0);
        return this;
    }

    public ChunkBuilder Eof() => Byte(0x00);

    /// <summary>An element: start tag (with attributes), content, end tag (or an empty tag when content is null)</summary>
    public ChunkBuilder Element(string name, bool inTemplate, Action<ChunkBuilder> content, int dependencyId = -1,
        params (string Name, Action<ChunkBuilder> Value)[] attributes)
    {
        Byte((byte)(attributes.Length > 0 ? 0x41 : 0x01));
        if (inTemplate)
        {
            U16((ushort)(dependencyId < 0 ? 0xFFFF : dependencyId));
        }

        var sizeAt = _pos;
        U32(0);
        var start = _pos;
        Name(name);
        if (attributes.Length > 0)
        {
            var listAt = _pos;
            U32(0);
            var listStart = _pos;
            for (var i = 0; i < attributes.Length; i++)
            {
                Byte((byte)(i < attributes.Length - 1 ? 0x46 : 0x06));
                Name(attributes[i].Name);
                attributes[i].Value(this);
            }

            BitConverter.GetBytes(_pos - listStart).CopyTo(_b, listAt);
        }

        if (content == null)
        {
            Byte(0x03);
        }
        else
        {
            Byte(0x02);
            content(this);
            Byte(0x04);
        }

        BitConverter.GetBytes(_pos - start).CopyTo(_b, sizeAt);
        return this;
    }

    public ChunkBuilder Text(string s)
    {
        Byte(0x05);
        Byte(0x01);
        U16((ushort)s.Length);
        Bytes(Encoding.Unicode.GetBytes(s));
        return this;
    }

    /// <summary>A ValueText token of another type (as in records without templates)</summary>
    public ChunkBuilder TypedText(byte type, byte[] value)
    {
        Byte(0x05);
        Byte(type);
        Bytes(value);
        return this;
    }

    public ChunkBuilder Substitution(int index, byte type, bool optional = false)
    {
        Byte((byte)(optional ? 0x0E : 0x0D));
        U16((ushort)index);
        Byte(type);
        return this;
    }

    public ChunkBuilder CharRef(ushort value)
    {
        Byte(0x08);
        U16(value);
        return this;
    }

    public ChunkBuilder EntityRef(string name)
    {
        Byte(0x09);
        Name(name);
        return this;
    }

    public ChunkBuilder CData(string s)
    {
        Byte(0x07);
        U16((ushort)s.Length);
        Bytes(Encoding.Unicode.GetBytes(s));
        return this;
    }

    public ChunkBuilder Pi(string target, string data)
    {
        Byte(0x0A);
        Name(target);
        Byte(0x0B);
        U16((ushort)data.Length);
        Bytes(Encoding.Unicode.GetBytes(data));
        return this;
    }

    /// <summary>
    ///     A template instance. With a definition it is written inline (and its offset returned); with only an
    ///     offset, it refers to a definition written earlier in the chunk
    /// </summary>
    public int TemplateInstance(Action<ChunkBuilder> definition, int definitionOffset, params Val[] values)
    {
        Byte(0x0C);
        Byte(0x01);
        U32(0x12345678);
        if (definition != null)
        {
            definitionOffset = _pos + 4;
            U32((uint)definitionOffset);
            U32(0); //next template
            Bytes(Guid.NewGuid().ToByteArray());
            var sizeAt = _pos;
            U32(0);
            var start = _pos;
            FragmentHeader();
            definition(this);
            Eof();
            BitConverter.GetBytes(_pos - start).CopyTo(_b, sizeAt);
        }
        else
        {
            U32((uint)definitionOffset);
        }

        U32((uint)values.Length);
        var descriptors = _pos;
        foreach (var v in values)
        {
            U16(0);
            Byte(v.Type);
            Byte(0);
        }

        for (var i = 0; i < values.Length; i++)
        {
            var start = _pos;
            if (values[i].Write != null)
            {
                values[i].Write(this);
            }
            else
            {
                Bytes(values[i].Bytes);
            }

            BitConverter.GetBytes((ushort)(_pos - start)).CopyTo(_b, descriptors + i * 4);
        }

        return definitionOffset;
    }

    /// <summary>A name: its offset, and the name itself the first time (inline)</summary>
    public ChunkBuilder Name(string name)
    {
        if (_names.TryGetValue(name, out var offset))
        {
            U32((uint)offset);
            return this;
        }

        offset = _pos + 4;
        _names[name] = offset;
        U32((uint)offset);
        U32(0); //next string
        U16(Hash(name));
        U16((ushort)name.Length);
        Bytes(Encoding.Unicode.GetBytes(name));
        U16(0);
        return this;
    }

    private static ushort Hash(string s)
    {
        uint h = 0;
        foreach (var c in s)
        {
            h = h * 65599 + c;
        }

        return (ushort)h;
    }

    public ChunkBuilder Byte(byte b)
    {
        _b[_pos++] = b;
        return this;
    }

    public ChunkBuilder U16(ushort v) => Bytes(BitConverter.GetBytes(v));
    public ChunkBuilder U32(uint v) => Bytes(BitConverter.GetBytes(v));
    public ChunkBuilder U64(ulong v) => Bytes(BitConverter.GetBytes(v));

    public ChunkBuilder Bytes(byte[] bytes)
    {
        bytes.CopyTo(_b, _pos);
        _pos += bytes.Length;
        return this;
    }

    public void Poke(int offset, params byte[] bytes) => bytes.CopyTo(_b, offset);
}

/// <summary>A substitution value: its type and bytes, or a writer for BinXml values (written in place)</summary>
public sealed class Val
{
    public Val(byte type, byte[] bytes, Action<ChunkBuilder> write = null)
    {
        Type = type;
        Bytes = bytes;
        Write = write;
    }

    public byte Type { get; }
    public byte[] Bytes { get; }
    public Action<ChunkBuilder> Write { get; }
}

/// <summary>Substitution values</summary>
public static class V
{
    public static Val Null(int size = 0) => new(0x00, new byte[size]);
    public static Val Str(string s) => new(0x01, Encoding.Unicode.GetBytes(s));
    public static Val U8(byte v) => new(0x04, new[] { v });
    public static Val U16(ushort v) => new(0x06, BitConverter.GetBytes(v));
    public static Val I32(int v) => new(0x07, BitConverter.GetBytes(v));
    public static Val U64(ulong v) => new(0x0A, BitConverter.GetBytes(v));
    public static Val Hex64(long v) => new(0x15, BitConverter.GetBytes(v));
    public static Val FileTime(DateTime t) => new(0x11, BitConverter.GetBytes(t.ToFileTimeUtc()));
    public static Val Of(byte type, byte[] bytes) => new(type, bytes);
    public static Val BinXml(Action<ChunkBuilder> write) => new(0x21, null, write);
}
