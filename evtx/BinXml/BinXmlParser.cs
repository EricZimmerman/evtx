using System;
using System.Collections.Generic;
using System.Text;

namespace evtx.BinXml;

/// <summary>
///     Parses BinXml (MS-EVEN6 2.2.12) straight from a chunk's bytes. Names and template definitions are stored in the
///     chunk: a name or template definition offset equal to the current position means the data follows inline,
///     otherwise it was defined earlier in the chunk at that offset. Elements in template definitions have a 2 byte
///     dependency id; elements written directly in a record or in a BinXml value do not.
/// </summary>
internal sealed class BinXmlParser
{
    /// <summary>Nested BinXml values and templates deeper than this are treated as corrupt</summary>
    private const int MaxDepth = 32;

    private readonly byte[] _b;
    private readonly Dictionary<int, string> _names = new();
    private readonly Dictionary<int, TemplateDefinition> _templates = new();
    private readonly HashSet<int> _templatesInProgress = new();

    public BinXmlParser(byte[] chunk)
    {
        _b = chunk;
    }

    public int TemplateCount => _templates.Count;

    /// <summary>Parses a fragment (a record's event data or a BinXml value) from start up to end</summary>
    public List<Node> ParseFragment(int start, int end, bool inTemplate, int depth) => ParseFragment(start, end, inTemplate, depth, out _);

    /// <summary>Parses a fragment; stop is where parsing stopped (after the EOF token, or end)</summary>
    public List<Node> ParseFragment(int start, int end, bool inTemplate, int depth, out int stop)
    {
        if (depth > MaxDepth)
        {
            throw new EventLogParseException("BinXml nested too deeply", start, "BinXml");
        }

        end = Math.Min(end, _b.Length);
        var nodes = new List<Node>();
        var pos = start;

        while (pos < end)
        {
            var token = _b[pos];
            switch (token & 0xBF)
            {
                case 0x00:
                    //EOFToken
                    stop = pos + 1;
                    return nodes;
                case 0x0F:
                    //FragmentHeader: token, major, minor, flags
                    pos += 4;
                    break;
                case 0x0C:
                    nodes.Add(ParseTemplateInstance(ref pos, end, depth));
                    break;
                case 0x01:
                    nodes.Add(ParseElement(ref pos, end, inTemplate, depth));
                    break;
                case 0x0A:
                    nodes.Add(ParsePi(ref pos, end));
                    break;
                default:
                    throw new EventLogParseException($"Unexpected BinXml token 0x{token:X2} at the top of a fragment", pos, "BinXml");
            }
        }

        stop = pos;
        return nodes;
    }

    private ElementNode ParseElement(ref int pos, int end, bool inTemplate, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new EventLogParseException("BinXml elements nested too deeply", pos, "BinXml");
        }

        var token = _b[pos++];
        var hasAttributes = (token & 0x40) != 0;

        var dependencyId = -1;
        if (inTemplate)
        {
            dependencyId = U16(pos, end);
            pos += 2;
            if (dependencyId == 0xFFFF)
            {
                dependencyId = -1;
            }
        }

        //the data size is bytes after it, including the end token; checked against what is parsed
        var dataSize = U32(pos, end);
        pos += 4;
        var elementEnd = (long)pos + dataSize;
        if (elementEnd > end)
        {
            throw new EventLogParseException($"Element of 0x{dataSize:X} bytes runs past the end of its data", pos, "OpenStartElement");
        }

        var element = new ElementNode(ReadName(ref pos, end), dependencyId);

        if (hasAttributes)
        {
            var listSize = U32(pos, end);
            pos += 4;
            var listEnd = (long)pos + listSize;
            if (listEnd > elementEnd)
            {
                throw new EventLogParseException("Attribute list runs past the end of its element", pos, "AttributeList");
            }

            while (pos < listEnd)
            {
                if ((_b[pos] & 0xBF) != 0x06)
                {
                    throw new EventLogParseException($"Expected an attribute, found token 0x{_b[pos]:X2}", pos, "Attribute");
                }

                pos++;
                var attribute = new AttributeNode(ReadName(ref pos, end));
                ParseAttributeValue(ref pos, (int)listEnd, attribute.Value, depth);
                element.Attributes.Add(attribute);
            }
        }

        var close = Byte(pos, end);
        pos++;
        if (close == 0x03)
        {
            element.EmptyTag = true;
            return element;
        }

        if (close != 0x02)
        {
            throw new EventLogParseException($"Expected the end of a start tag, found token 0x{close:X2}", pos - 1, "CloseStartElement");
        }

        ParseContent(ref pos, (int)elementEnd, element.Content, inTemplate, depth);
        return element;
    }

    private void ParseAttributeValue(ref int pos, int end, List<Node> value, int depth)
    {
        while (pos < end)
        {
            switch (_b[pos] & 0xBF)
            {
                case 0x05:
                    value.Add(ParseValueText(ref pos, end));
                    break;
                case 0x0D:
                case 0x0E:
                    value.Add(ParseSubstitution(ref pos, end));
                    break;
                case 0x08:
                    value.Add(ParseCharRef(ref pos, end));
                    break;
                case 0x09:
                    pos++;
                    value.Add(new EntityRefNode(ReadName(ref pos, end)));
                    break;
                default:
                    //the next attribute
                    return;
            }
        }
    }

    private void ParseContent(ref int pos, int end, List<Node> content, bool inTemplate, int depth)
    {
        while (pos < end)
        {
            var token = _b[pos];
            switch (token & 0xBF)
            {
                case 0x04:
                    //EndElementToken
                    pos++;
                    return;
                case 0x01:
                    content.Add(ParseElement(ref pos, end, inTemplate, depth + 1));
                    break;
                case 0x05:
                    content.Add(ParseValueText(ref pos, end));
                    break;
                case 0x0D:
                case 0x0E:
                    content.Add(ParseSubstitution(ref pos, end));
                    break;
                case 0x07:
                    pos++;
                    content.Add(new CDataNode(ReadLengthPrefixedString(ref pos, end)));
                    break;
                case 0x08:
                    content.Add(ParseCharRef(ref pos, end));
                    break;
                case 0x09:
                    pos++;
                    content.Add(new EntityRefNode(ReadName(ref pos, end)));
                    break;
                case 0x0A:
                    content.Add(ParsePi(ref pos, end));
                    break;
                case 0x0C:
                    content.Add(ParseTemplateInstance(ref pos, end, depth + 1));
                    break;
                case 0x0F:
                    pos += 4;
                    break;
                default:
                    throw new EventLogParseException($"Unexpected BinXml token 0x{token:X2} in element content", pos, "Content");
            }
        }

        throw new EventLogParseException("Element has no end", pos, "EndElement");
    }

    private Node ParseCharRef(ref int pos, int end)
    {
        pos++;
        var value = (ushort)U16(pos, end);
        pos += 2;
        return new CharRefNode(value);
    }

    private PiNode ParsePi(ref int pos, int end)
    {
        pos++;
        var pi = new PiNode(ReadName(ref pos, end));
        if (pos < end && _b[pos] == 0x0B)
        {
            pos++;
            pi.Data = ReadLengthPrefixedString(ref pos, end);
        }

        return pi;
    }

    private SubstitutionNode ParseSubstitution(ref int pos, int end)
    {
        var optional = (_b[pos] & 0xBF) == 0x0E;
        pos++;
        var index = U16(pos, end);
        pos += 2;
        var type = (ValueType)Byte(pos, end);
        pos++;
        return new SubstitutionNode(index, type, optional);
    }

    /// <summary>ValueText: a type, then the value (strings are length prefixed; other types have their fixed size)</summary>
    private TextNode ParseValueText(ref int pos, int end)
    {
        pos++;
        var type = (ValueType)Byte(pos, end);
        pos++;

        if (type == ValueType.StringType)
        {
            return new TextNode(ReadLengthPrefixedString(ref pos, end));
        }

        int size;
        switch (type)
        {
            case ValueType.NullType:
                size = 0;
                break;
            case ValueType.Int8Type:
            case ValueType.UInt8Type:
                size = 1;
                break;
            case ValueType.Int16Type:
            case ValueType.UInt16Type:
                size = 2;
                break;
            case ValueType.Int32Type:
            case ValueType.UInt32Type:
            case ValueType.Real32Type:
            case ValueType.HexInt32Type:
            case ValueType.BoolType:
                size = 4;
                break;
            case ValueType.Int64Type:
            case ValueType.UInt64Type:
            case ValueType.Real64Type:
            case ValueType.HexInt64Type:
            case ValueType.FileTimeType:
            case ValueType.SizeTType:
                size = 8;
                break;
            case ValueType.GuidType:
            case ValueType.SysTimeType:
                size = 16;
                break;
            case ValueType.SidType:
                size = 8 + 4 * Byte(pos + 1, end);
                break;
            default:
                throw new EventLogParseException($"Inline value of type {type} has no size", pos, "ValueText");
        }

        if (pos + size > end)
        {
            throw new EventLogParseException($"Inline {type} value runs past the end of its data", pos, "ValueText");
        }

        var text = ValueFormatter.Format(_b, new Value(type, pos, size), true);
        pos += size;
        return new TextNode(text);
    }

    private TemplateInstanceNode ParseTemplateInstance(ref int pos, int end, int depth)
    {
        //token, an unknown byte, the template id, the definition's offset
        pos += 2;
        var templateId = U32(pos, end);
        pos += 4;
        var definitionOffset = (int)U32(pos, end);
        pos += 4;

        TemplateDefinition template;
        if (definitionOffset == pos)
        {
            //defined here: next template offset, GUID, data size, data
            var dataSize = (int)U32(pos + 20, end);
            template = GetTemplate(definitionOffset, depth);
            pos += 24 + dataSize;
        }
        else
        {
            template = GetTemplate(definitionOffset, depth);
        }

        var count = U32(pos, end);
        pos += 4;
        if (count > (end - pos) / 4)
        {
            throw new EventLogParseException($"Template instance has {count} substitution values, more than fit in its data (template id 0x{templateId:X})", pos, "TemplateInstanceData");
        }

        var values = new Value[count];
        var descriptor = pos;
        pos += (int)count * 4;
        for (var i = 0; i < count; i++)
        {
            var size = U16(descriptor + i * 4, end);
            var type = (ValueType)_b[descriptor + i * 4 + 2];
            if (pos + size > end)
            {
                throw new EventLogParseException($"Substitution value {i} ({type}, 0x{size:X} bytes) runs past the end of the record", pos, "Value");
            }

            values[i] = new Value(type, pos, size);
            pos += size;
        }

        return new TemplateInstanceNode(template, values);
    }

    /// <summary>The template definition at offset in the chunk: next template offset, GUID, data size, data</summary>
    private TemplateDefinition GetTemplate(int offset, int depth)
    {
        if (_templates.TryGetValue(offset, out var template))
        {
            return template;
        }

        if (offset < 0x200 || offset + 24 > _b.Length)
        {
            throw new EventLogParseException($"Template definition offset 0x{offset:X} is outside the chunk", offset, "TemplateDefinition");
        }

        if (_templatesInProgress.Add(offset) == false)
        {
            throw new EventLogParseException($"Template definition at 0x{offset:X} refers to itself", offset, "TemplateDefinition");
        }

        try
        {
            var guidBytes = new byte[16];
            Buffer.BlockCopy(_b, offset + 4, guidBytes, 0, 16);
            var dataSize = (int)U32(offset + 20, _b.Length);
            var dataStart = offset + 24;
            if (dataSize < 0 || dataStart + (long)dataSize > _b.Length)
            {
                throw new EventLogParseException($"Template definition at 0x{offset:X} (0x{dataSize:X} bytes) runs past the end of the chunk", offset, "TemplateDefinition");
            }

            var nodes = ParseFragment(dataStart, dataStart + dataSize, true, depth + 1);
            template = new TemplateDefinition(offset, new Guid(guidBytes), nodes);
            _templates[offset] = template;
            return template;
        }
        finally
        {
            _templatesInProgress.Remove(offset);
        }
    }

    /// <summary>A name: its chunk offset, and when that is the current position, the name itself inline</summary>
    private string ReadName(ref int pos, int end)
    {
        var offset = (int)U32(pos, end);
        pos += 4;

        var name = GetName(offset);
        if (offset == pos)
        {
            //next string offset, hash, character count, characters, NUL
            pos += 8 + U16(pos + 6, end) * 2 + 2;
        }

        return name;
    }

    private string GetName(int offset)
    {
        if (_names.TryGetValue(offset, out var name))
        {
            return name;
        }

        if (offset < 0 || offset + 8 > _b.Length)
        {
            throw new EventLogParseException($"Name offset 0x{offset:X} is outside the chunk", offset, "Name");
        }

        var chars = BitConverter.ToUInt16(_b, offset + 6);
        if (offset + 8 + chars * 2 > _b.Length)
        {
            throw new EventLogParseException($"Name at 0x{offset:X} ({chars} characters) runs past the end of the chunk", offset, "Name");
        }

        name = Encoding.Unicode.GetString(_b, offset + 8, chars * 2);
        _names[offset] = name;
        return name;
    }

    private string ReadLengthPrefixedString(ref int pos, int end)
    {
        var chars = U16(pos, end);
        pos += 2;
        if (pos + chars * 2 > end)
        {
            throw new EventLogParseException($"String of {chars} characters runs past the end of its data", pos, "String");
        }

        var s = Encoding.Unicode.GetString(_b, pos, chars * 2);
        pos += chars * 2;
        return s;
    }

    private byte Byte(int pos, int end)
    {
        if (pos >= end)
        {
            throw new EventLogParseException("BinXml ends early", pos, "BinXml");
        }

        return _b[pos];
    }

    private int U16(int pos, int end)
    {
        if (pos + 2 > end)
        {
            throw new EventLogParseException("BinXml ends early", pos, "BinXml");
        }

        return BitConverter.ToUInt16(_b, pos);
    }

    private uint U32(int pos, int end)
    {
        if (pos + 4 > end)
        {
            throw new EventLogParseException("BinXml ends early", pos, "BinXml");
        }

        return BitConverter.ToUInt32(_b, pos);
    }
}
