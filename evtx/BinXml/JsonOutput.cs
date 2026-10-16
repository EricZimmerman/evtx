using System.Collections.Generic;
using System.Text;

namespace evtx.BinXml;

/// <summary>
///     An element as JSON, exactly as Json.NET's SerializeXmlNode wrote the XmlDocument EvtxECmd used to load the
///     payload into: attributes as "@Name", text as "#text", children with the same name grouped into an array (at the
///     first one's place), an element with only text as that text, &lt;x/&gt; as null and &lt;x&gt;&lt;/x&gt; as ""
/// </summary>
internal static class JsonOutput
{
    /// <param name="crlf">
    ///     Line breaks in text as CRLF (as the whole event's XML had them when it was loaded) rather than LF (as the
    ///     payload had them, after the XmlReader that extracted it)
    /// </param>
    public static string Write(OutElement root, bool crlf)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        AppendString(sb, root.Name);
        sb.Append(':');
        AppendElement(sb, root, crlf);
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendElement(StringBuilder sb, OutElement e, bool crlf)
    {
        var children = XmlTextOutput.LoadedChildren(e);
        var attributes = Attributes(e);

        if (attributes.Count == 0 && children.Count == 1 && children[0] is string text)
        {
            AppendString(sb, TextValue(text, crlf));
            return;
        }

        if (attributes.Count == 0 && children.Count == 0)
        {
            sb.Append(e.EmptyTag ? "null" : "\"\"");
            return;
        }

        sb.Append('{');
        var first = true;
        foreach (var a in attributes)
        {
            Comma(sb, ref first);
            AppendString(sb, "@" + a.Key);
            sb.Append(':');
            AppendString(sb, XmlTextOutput.AttributeValue(a.Value));
        }

        AppendGrouped(sb, children, ref first, crlf);
        sb.Append('}');
    }

    /// <summary>Children grouped by name in the order each name first appears; a name used more than once is an array</summary>
    private static void AppendGrouped(StringBuilder sb, List<object> children, ref bool first, bool crlf)
    {
        if (children.Count == 1)
        {
            Comma(sb, ref first);
            AppendNamed(sb, children[0], crlf);
            return;
        }

        var order = new List<string>();
        var groups = new Dictionary<string, List<object>>();
        foreach (var c in children)
        {
            var name = PropertyName(c);
            if (groups.TryGetValue(name, out var group) == false)
            {
                group = new List<object>();
                groups.Add(name, group);
                order.Add(name);
            }

            group.Add(c);
        }

        foreach (var name in order)
        {
            var group = groups[name];
            Comma(sb, ref first);
            if (group.Count == 1)
            {
                AppendNamed(sb, group[0], crlf);
                continue;
            }

            AppendString(sb, name);
            sb.Append(":[");
            for (var i = 0; i < group.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                AppendValue(sb, group[i], crlf);
            }

            sb.Append(']');
        }
    }

    private static void AppendNamed(StringBuilder sb, object node, bool crlf)
    {
        AppendString(sb, PropertyName(node));
        sb.Append(':');
        AppendValue(sb, node, crlf);
    }

    private static void AppendValue(StringBuilder sb, object node, bool crlf)
    {
        switch (node)
        {
            case OutElement e:
                AppendElement(sb, e, crlf);
                break;
            case string s:
                AppendString(sb, TextValue(s, crlf));
                break;
            case OutCData cd:
                AppendString(sb, cd.Text);
                break;
            case OutPi pi:
                AppendString(sb, pi.Data);
                break;
        }
    }

    private static string PropertyName(object node)
    {
        return node switch
        {
            OutElement e => e.Name,
            OutCData => "#cdata-section",
            OutPi pi => "?" + pi.Target,
            _ => "#text"
        };
    }

    private static List<KeyValuePair<string, string>> Attributes(OutElement e)
    {
        var list = new List<KeyValuePair<string, string>>(e.Attributes.Count);
        foreach (var a in e.Attributes)
        {
            if (a.Key != "xmlns" && a.Key.StartsWith("xmlns:", System.StringComparison.Ordinal) == false)
            {
                list.Add(a);
            }
        }

        return list;
    }

    /// <summary>Text as it was after being written and loaded again: impossible characters are U+FFFD</summary>
    private static string TextValue(string s, bool crlf)
    {
        return crlf ? XmlTextOutput.XPathTextValue(s) : XmlTextOutput.TextValue(s);
    }

    private static void Comma(StringBuilder sb, ref bool first)
    {
        if (first)
        {
            first = false;
            return;
        }

        sb.Append(',');
    }

    /// <summary>A JSON string as Json.NET escapes it by default</summary>
    private static void AppendString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case (char)0x85:
                case (char)0x2028:
                case (char)0x2029:
                    sb.Append("\\u").Append(((int)c).ToString("x4"));
                    break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
