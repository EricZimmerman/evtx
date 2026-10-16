using System;
using System.Collections.Generic;

namespace evtx.BinXml;

/// <summary>A BinXml node (MS-EVEN6 2.2.12) as parsed from a record, a template definition or a BinXml value</summary>
internal abstract class Node
{
}

internal sealed class ElementNode : Node
{
    public ElementNode(string name, int dependencyId)
    {
        Name = name;
        DependencyId = dependencyId;
    }

    public string Name { get; }

    /// <summary>
    ///     Index into the template instance values; when that value is NullType the element is not rendered. -1 for
    ///     none (0xFFFF, or elements outside template definitions, which have no dependency id)
    /// </summary>
    public int DependencyId { get; }

    public List<AttributeNode> Attributes { get; } = new();
    public List<Node> Content { get; } = new();

    /// <summary>Closed with CloseEmptyElementToken (rendered as &lt;Name /&gt;)</summary>
    public bool EmptyTag { get; set; }
}

internal sealed class AttributeNode
{
    public AttributeNode(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>Text, substitutions, character and entity references</summary>
    public List<Node> Value { get; } = new();
}

/// <summary>ValueText: an inline value (normally a string; other types appear in records without templates)</summary>
internal sealed class TextNode : Node
{
    public TextNode(string text)
    {
        Text = text;
    }

    public string Text { get; }
}

internal sealed class SubstitutionNode : Node
{
    public SubstitutionNode(int index, ValueType type, bool optional)
    {
        Index = index;
        Type = type;
        Optional = optional;
    }

    public int Index { get; }
    public ValueType Type { get; }
    public bool Optional { get; }
}

internal sealed class CharRefNode : Node
{
    public CharRefNode(ushort value)
    {
        Value = value;
    }

    public ushort Value { get; }
}

internal sealed class EntityRefNode : Node
{
    public EntityRefNode(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

internal sealed class CDataNode : Node
{
    public CDataNode(string text)
    {
        Text = text;
    }

    public string Text { get; }
}

internal sealed class PiNode : Node
{
    public PiNode(string target)
    {
        Target = target;
    }

    public string Target { get; }
    public string Data { get; set; } = string.Empty;
}

/// <summary>A template instance: the template definition and the substitution values for it</summary>
internal sealed class TemplateInstanceNode : Node
{
    public TemplateInstanceNode(TemplateDefinition template, Value[] values)
    {
        Template = template;
        Values = values;
    }

    public TemplateDefinition Template { get; }
    public Value[] Values { get; }
}

/// <summary>A template definition, parsed once per chunk</summary>
internal sealed class TemplateDefinition
{
    public TemplateDefinition(int offset, Guid guid, List<Node> nodes)
    {
        Offset = offset;
        Guid = guid;
        Nodes = nodes;
    }

    /// <summary>Where the definition (next template offset, GUID, size, data) is in the chunk</summary>
    public int Offset { get; }

    public Guid Guid { get; }
    public List<Node> Nodes { get; }
}

/// <summary>A substitution value: its type and where its bytes are in the chunk</summary>
internal readonly struct Value
{
    public Value(ValueType type, int offset, int size)
    {
        Type = type;
        Offset = offset;
        Size = size;
    }

    public ValueType Type { get; }
    public int Offset { get; }
    public int Size { get; }
}

/// <summary>MS-EVEN6 2.2.12 value types</summary>
internal enum ValueType : byte
{
    NullType = 0x00,
    StringType = 0x01,
    AnsiStringType = 0x02,
    Int8Type = 0x03,
    UInt8Type = 0x04,
    Int16Type = 0x05,
    UInt16Type = 0x06,
    Int32Type = 0x07,
    UInt32Type = 0x08,
    Int64Type = 0x09,
    UInt64Type = 0x0A,
    Real32Type = 0x0B,
    Real64Type = 0x0C,
    BoolType = 0x0D,
    BinaryType = 0x0E,
    GuidType = 0x0F,
    SizeTType = 0x10,
    FileTimeType = 0x11,
    SysTimeType = 0x12,
    SidType = 0x13,
    HexInt32Type = 0x14,
    HexInt64Type = 0x15,
    EvtHandle = 0x20,
    BinXmlType = 0x21,
    EvtXml = 0x23,

    /// <summary>Arrays are the element type with 0x80 set</summary>
    ArrayFlag = 0x80
}
