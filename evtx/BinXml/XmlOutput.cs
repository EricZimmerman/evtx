using System;
using System.Collections.Generic;
using System.Text;

namespace evtx.BinXml;

/// <summary>A rendered element: substitutions resolved, ready to write or query</summary>
internal sealed class OutElement
{
    public OutElement(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public List<KeyValuePair<string, string>> Attributes { get; } = new();

    /// <summary>Strings (text), <see cref="OutElement" />s, <see cref="OutCData" />s and <see cref="OutPi" />s</summary>
    public List<object> Children { get; } = new();

    public bool EmptyTag { get; set; }

    /// <summary>Where the element starts and ends in the written XML</summary>
    public int XmlStart { get; set; }

    public int XmlEnd { get; set; }

    public string Attribute(string name)
    {
        foreach (var a in Attributes)
        {
            if (a.Key == name)
            {
                return a.Value;
            }
        }

        return null;
    }

    public OutElement Child(string name)
    {
        foreach (var c in Children)
        {
            if (c is OutElement e && e.Name == name)
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>The text directly in the element (as the XML parser that used to read it saw it)</summary>
    public string Text()
    {
        string single = null;
        StringBuilder sb = null;
        foreach (var c in Children)
        {
            if (c is string s)
            {
                if (single == null)
                {
                    single = s;
                }
                else
                {
                    sb ??= new StringBuilder(single);
                    sb.Append(s);
                }
            }
        }

        var text = sb?.ToString() ?? single ?? string.Empty;
        return IsWhitespace(text) ? string.Empty : text;
    }

    internal static bool IsWhitespace(string s)
    {
        foreach (var c in s)
        {
            if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class OutCData
{
    public OutCData(string text)
    {
        Text = text;
    }

    public string Text { get; }
}

internal sealed class OutPi
{
    public OutPi(string target, string data)
    {
        Target = target;
        Data = data;
    }

    public string Target { get; }
    public string Data { get; }
}

/// <summary>Resolves template instances and substitutions into <see cref="OutElement" />s</summary>
internal sealed class Renderer
{
    private const int MaxDepth = 32;

    private readonly byte[] _b;
    private readonly BinXmlParser _parser;

    public Renderer(byte[] chunk, BinXmlParser parser)
    {
        _b = chunk;
        _parser = parser;
    }

    /// <summary>The record's top-level output: its root element is always rendered</summary>
    private List<object> _root;

    /// <summary>Renders a record's nodes into output</summary>
    public void RenderRecord(List<Node> nodes, List<object> output)
    {
        _root = output;
        Render(nodes, null, output, 0);
    }

    public void Render(List<Node> nodes, Value[] values, List<object> output, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new EventLogParseException("Template instances nested too deeply", -1, "TemplateInstance");
        }

        foreach (var node in nodes)
        {
            switch (node)
            {
                case TemplateInstanceNode ti:
                    Render(ti.Template.Nodes, ti.Values, output, depth + 1);
                    break;
                case ElementNode e:
                    //MS-EVEN6 2.2.12.1 says an element whose dependency value is NULL is not rendered, but EvtxECmd
                    //always rendered them (e.g. an empty <Binary></Binary>), as the Rust evtx parser does
                    var element = new OutElement(e.Name) { EmptyTag = e.EmptyTag };
                    foreach (var a in e.Attributes)
                    {
                        if (AttributeValue(a, values, out var value))
                        {
                            element.Attributes.Add(new KeyValuePair<string, string>(a.Name, value));
                        }
                    }

                    Render(e.Content, values, element.Children, depth);
                    output.Add(element);
                    break;
                case TextNode t:
                    output.Add(t.Text);
                    break;
                case SubstitutionNode s:
                    RenderSubstitution(s, values, output, depth);
                    break;
                case CharRefNode c:
                    output.Add(((char)c.Value).ToString());
                    break;
                case EntityRefNode er:
                    output.Add(Entity(er.Name));
                    break;
                case CDataNode cd:
                    output.Add(new OutCData(cd.Text));
                    break;
                case PiNode pi:
                    output.Add(new OutPi(pi.Target, pi.Data));
                    break;
            }
        }
    }

    private void RenderSubstitution(SubstitutionNode s, Value[] values, List<object> output, int depth)
    {
        if (values == null || s.Index >= values.Length)
        {
            return;
        }

        var v = values[s.Index];
        switch (v.Type)
        {
            case ValueType.NullType:
                return;
            case ValueType.BinXmlType:
                //MS-EVEN6 2.2.12.3: a BinXml value is rendered in place (typically a template instance of its own)
                if (v.Size > 0)
                {
                    var nested = _parser.ParseFragment(v.Offset, v.Offset + v.Size, false, depth + 1, out _);
                    Render(nested, null, output, depth + 1);
                }

                return;
            default:
                output.Add(ValueFormatter.Format(_b, v));
                return;
        }
    }

    /// <returns>False when the attribute is not rendered: its value is only optional substitutions that are NULL</returns>
    private bool AttributeValue(AttributeNode a, Value[] values, out string value)
    {
        value = string.Empty;
        if (a.Value.Count == 1)
        {
            switch (a.Value[0])
            {
                case TextNode t:
                    value = t.Text;
                    return true;
                case SubstitutionNode s:
                    if (values == null || s.Index >= values.Length)
                    {
                        return s.Optional == false;
                    }

                    var v = values[s.Index];
                    if (v.Type == ValueType.NullType)
                    {
                        return s.Optional == false;
                    }

                    value = ValueFormatter.Format(_b, v);
                    return true;
            }
        }

        var sb = new StringBuilder();
        var any = false;
        foreach (var part in a.Value)
        {
            switch (part)
            {
                case TextNode t:
                    sb.Append(t.Text);
                    any = true;
                    break;
                case SubstitutionNode s:
                    if (values != null && s.Index < values.Length && values[s.Index].Type != ValueType.NullType)
                    {
                        sb.Append(ValueFormatter.Format(_b, values[s.Index]));
                        any = true;
                    }
                    else if (s.Optional == false)
                    {
                        any = true;
                    }

                    break;
                case CharRefNode c:
                    sb.Append((char)c.Value);
                    any = true;
                    break;
                case EntityRefNode er:
                    sb.Append(Entity(er.Name));
                    any = true;
                    break;
            }
        }

        value = sb.ToString();
        return any || a.Value.Count == 0;
    }

    private static string Entity(string name)
    {
        switch (name)
        {
            case "amp":
                return "&";
            case "lt":
                return "<";
            case "gt":
                return ">";
            case "quot":
                return "\"";
            case "apos":
                return "'";
            default:
                return $"&{name};";
        }
    }
}

/// <summary>
///     Writes <see cref="OutElement" />s as EvtxECmd always has (an XmlDocument saved with 2 space indents and CRLF):
///     whitespace-only text is dropped, line breaks in text are CRLF, white space in attribute values is a space,
///     xmlns attributes are left out, and text in an element stops the indenting inside it
/// </summary>
internal sealed class XmlTextOutput
{
    private const string NewLine = "\r\n";

    private readonly StringBuilder _sb;
    private readonly Stack<bool> _mixedStack = new();
    private int _level;
    private bool _mixed;
    private bool _lastWasText = true;

    public XmlTextOutput(StringBuilder sb)
    {
        _sb = sb;
    }

    public void Write(OutElement root)
    {
        WriteElement(root);
    }

    private void WriteElement(OutElement e)
    {
        if (_mixed == false && _lastWasText == false)
        {
            Indent();
        }

        _level++;
        _mixedStack.Push(_mixed);

        e.XmlStart = _sb.Length;
        _sb.Append('<').Append(e.Name);
        foreach (var a in e.Attributes)
        {
            if (a.Key == "xmlns" || a.Key.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                continue;
            }

            _sb.Append(' ').Append(a.Key).Append("=\"");
            AppendAttribute(a.Value);
            _sb.Append('"');
        }

        _lastWasText = false;

        var children = LoadedChildren(e);
        if (children.Count == 0)
        {
            _sb.Append(e.EmptyTag ? " />" : "></" + e.Name + ">");
            _level--;
            _mixed = _mixedStack.Pop();
            e.XmlEnd = _sb.Length;
            return;
        }

        _sb.Append('>');
        //the root element does not inherit mixed content; others do
        _mixed = _level != 1 && _mixedStack.Peek();
        var contentStart = _sb.Length;

        foreach (var c in children)
        {
            switch (c)
            {
                case string s:
                    AppendText(s);
                    _mixed = true;
                    _lastWasText = true;
                    break;
                case OutElement child:
                    WriteElement(child);
                    break;
                case OutCData cd:
                    _sb.Append("<![CDATA[").Append(cd.Text.Replace("]]>", "]]]]><![CDATA[>")).Append("]]>");
                    _mixed = true;
                    _lastWasText = false;
                    break;
                case OutPi pi:
                    if (_mixed == false && _lastWasText == false)
                    {
                        Indent();
                    }

                    _sb.Append("<?").Append(pi.Target);
                    if (pi.Data.Length > 0)
                    {
                        _sb.Append(' ').Append(pi.Data);
                    }

                    _sb.Append("?>");
                    _lastWasText = false;
                    break;
            }
        }

        _level--;
        if (_mixed == false && _sb.Length != contentStart && _lastWasText == false)
        {
            Indent();
        }

        _mixed = _mixedStack.Pop();
        _sb.Append("</").Append(e.Name).Append('>');
        _lastWasText = false;
        e.XmlEnd = _sb.Length;
    }

    /// <summary>Adjacent text merged, whitespace-only text dropped (as an XmlDocument loads it)</summary>
    internal static List<object> LoadedChildren(OutElement e)
    {
        var result = new List<object>(e.Children.Count);
        StringBuilder text = null;
        string first = null;

        void Flush()
        {
            var s = text?.ToString() ?? first;
            if (s != null && s.Length > 0 && OutElement.IsWhitespace(s) == false)
            {
                result.Add(s);
            }

            text = null;
            first = null;
        }

        foreach (var c in e.Children)
        {
            if (c is string s)
            {
                if (first == null)
                {
                    first = s;
                }
                else
                {
                    text ??= new StringBuilder(first);
                    text.Append(s);
                }
            }
            else
            {
                Flush();
                result.Add(c);
            }
        }

        Flush();
        return result;
    }

    /// <summary>Text as an XML parser reads back what is written: line breaks are LF, impossible characters U+FFFD</summary>
    internal static string TextValue(string s)
    {
        if (NeedsChange(s) == false)
        {
            return s;
        }

        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\r')
            {
                if (i + 1 < s.Length && s[i + 1] == '\n')
                {
                    i++;
                }

                sb.Append('\n');
            }
            else if (c == '\n')
            {
                sb.Append('\n');
            }
            else
            {
                AppendValid(sb, s, ref i);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Text as an XPathDocument of the written XML has it: that reader does not normalize line endings, so line
    ///     breaks stay CRLF as written; impossible characters U+FFFD
    /// </summary>
    internal static string XPathTextValue(string s)
    {
        if (NeedsChange(s) == false)
        {
            return s;
        }

        var sb = new StringBuilder(s.Length + 8);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\r')
            {
                if (i + 1 < s.Length && s[i + 1] == '\n')
                {
                    i++;
                }

                sb.Append("\r\n");
            }
            else if (c == '\n')
            {
                sb.Append("\r\n");
            }
            else
            {
                AppendValid(sb, s, ref i);
            }
        }

        return sb.ToString();
    }

    /// <summary>An attribute value as an XML parser reads back what is written: line breaks and tabs are spaces</summary>
    internal static string AttributeValue(string s)
    {
        if (NeedsChange(s) == false)
        {
            return s;
        }

        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\r')
            {
                if (i + 1 < s.Length && s[i + 1] == '\n')
                {
                    i++;
                }

                sb.Append(' ');
            }
            else if (c == '\n' || c == '\t')
            {
                sb.Append(' ');
            }
            else
            {
                AppendValid(sb, s, ref i);
            }
        }

        return sb.ToString();
    }

    private static bool NeedsChange(string s)
    {
        foreach (var c in s)
        {
            if (c < 0x20 || c >= 0xD800 && c <= 0xDFFF || c >= 0xFFFE)
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendValid(StringBuilder sb, string s, ref int i)
    {
        var c = s[i];
        if (c >= 0x20 && c < 0xD800 || c == '\t' || c > 0xDFFF && c < 0xFFFE)
        {
            sb.Append(c);
            return;
        }

        if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
        {
            sb.Append(c).Append(s[i + 1]);
            i++;
            return;
        }

        sb.Append('\uFFFD');
    }

    private void Indent()
    {
        _sb.Append(NewLine);
        _sb.Append(' ', _level * 2);
    }

    private void AppendText(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '&':
                    _sb.Append("&amp;");
                    break;
                case '<':
                    _sb.Append("&lt;");
                    break;
                case '>':
                    _sb.Append("&gt;");
                    break;
                case '\r':
                    if (i + 1 < s.Length && s[i + 1] == '\n')
                    {
                        i++;
                    }

                    _sb.Append(NewLine);
                    break;
                case '\n':
                    _sb.Append(NewLine);
                    break;
                default:
                    AppendChar(s, ref i);
                    break;
            }
        }
    }

    private void AppendAttribute(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '&':
                    _sb.Append("&amp;");
                    break;
                case '<':
                    _sb.Append("&lt;");
                    break;
                case '>':
                    _sb.Append("&gt;");
                    break;
                case '"':
                    _sb.Append("&quot;");
                    break;
                case '\r':
                    //attribute value normalization: a line break (CRLF counts once) or tab is a space
                    if (i + 1 < s.Length && s[i + 1] == '\n')
                    {
                        i++;
                    }

                    _sb.Append(' ');
                    break;
                case '\n':
                case '\t':
                    _sb.Append(' ');
                    break;
                default:
                    AppendChar(s, ref i);
                    break;
            }
        }
    }

    /// <summary>Characters XML cannot hold (other control characters, lone surrogates, U+FFFE/U+FFFF) become U+FFFD</summary>
    private void AppendChar(string s, ref int i)
    {
        var c = s[i];
        if (c >= 0x20 && c < 0xD800 || c == '\t' || c > 0xDFFF && c < 0xFFFE)
        {
            _sb.Append(c);
            return;
        }

        if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
        {
            _sb.Append(c).Append(s[i + 1]);
            i++;
            return;
        }

        _sb.Append('�');
    }
}
