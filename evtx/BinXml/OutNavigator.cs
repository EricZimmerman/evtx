using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace evtx.BinXml;

/// <summary>
///     XPath over a rendered event without writing and parsing XML (as maps used to, with an XPathDocument of the
///     event XML): the same nodes and values that document had (no xmlns attributes, whitespace-only text dropped,
///     line breaks in text as CRLF, white space in attribute values as spaces)
/// </summary>
internal sealed class OutNavigator : XPathNavigator
{
    private readonly NameTable _names;
    private NavNode _node;
    private int _attribute = -1;

    public OutNavigator(OutElement root)
    {
        _names = new NameTable();
        var document = new NavNode(XPathNodeType.Root, string.Empty, null, null);
        document.Children.Add(NavNode.From(root, document));
        _node = document;
    }

    private OutNavigator(OutNavigator other)
    {
        _names = other._names;
        _node = other._node;
        _attribute = other._attribute;
    }

    public override XmlNameTable NameTable => _names;

    public override XPathNodeType NodeType => _attribute >= 0 ? XPathNodeType.Attribute : _node.Type;

    public override string LocalName => _attribute >= 0 ? _node.Attributes[_attribute].Key : _node.Name;

    public override string Name => LocalName;

    public override string NamespaceURI => string.Empty;

    public override string Prefix => string.Empty;

    public override string BaseURI => string.Empty;

    public override bool IsEmptyElement => _node.Type == XPathNodeType.Element && _node.Children.Count == 0;

    public override string Value => _attribute >= 0 ? _node.Attributes[_attribute].Value : _node.StringValue();

    public override XPathNavigator Clone() => new OutNavigator(this);

    public override bool IsSamePosition(XPathNavigator other) =>
        other is OutNavigator o && o._node == _node && o._attribute == _attribute;

    public override bool MoveTo(XPathNavigator other)
    {
        if (other is not OutNavigator o || o._names != _names)
        {
            return false;
        }

        _node = o._node;
        _attribute = o._attribute;
        return true;
    }

    public override bool MoveToFirstAttribute()
    {
        if (_attribute >= 0 || _node.Attributes.Count == 0)
        {
            return false;
        }

        _attribute = 0;
        return true;
    }

    public override bool MoveToNextAttribute()
    {
        if (_attribute < 0 || _attribute + 1 >= _node.Attributes.Count)
        {
            return false;
        }

        _attribute++;
        return true;
    }

    public override bool MoveToFirstNamespace(XPathNamespaceScope namespaceScope) => false;

    public override bool MoveToNextNamespace(XPathNamespaceScope namespaceScope) => false;

    public override bool MoveToNext()
    {
        if (_attribute >= 0 || _node.Parent == null || _node.Index + 1 >= _node.Parent.Children.Count)
        {
            return false;
        }

        _node = _node.Parent.Children[_node.Index + 1];
        return true;
    }

    public override bool MoveToPrevious()
    {
        if (_attribute >= 0 || _node.Parent == null || _node.Index == 0)
        {
            return false;
        }

        _node = _node.Parent.Children[_node.Index - 1];
        return true;
    }

    public override bool MoveToFirstChild()
    {
        if (_attribute >= 0 || _node.Children.Count == 0)
        {
            return false;
        }

        _node = _node.Children[0];
        return true;
    }

    public override bool MoveToParent()
    {
        if (_attribute >= 0)
        {
            _attribute = -1;
            return true;
        }

        if (_node.Parent == null)
        {
            return false;
        }

        _node = _node.Parent;
        return true;
    }

    public override bool MoveToId(string id) => false;

    private sealed class NavNode
    {
        public NavNode(XPathNodeType type, string name, string value, NavNode parent)
        {
            Type = type;
            Name = name;
            Text = value;
            Parent = parent;
        }

        public XPathNodeType Type { get; }
        public string Name { get; }
        public string Text { get; }
        public NavNode Parent { get; }
        public int Index { get; private set; }
        public List<NavNode> Children { get; } = new();
        public List<KeyValuePair<string, string>> Attributes { get; } = new();

        public static NavNode From(OutElement e, NavNode parent)
        {
            var node = new NavNode(XPathNodeType.Element, e.Name, null, parent);
            foreach (var a in e.Attributes)
            {
                if (a.Key != "xmlns" && a.Key.StartsWith("xmlns:", System.StringComparison.Ordinal) == false)
                {
                    node.Attributes.Add(new KeyValuePair<string, string>(a.Key, XmlTextOutput.AttributeValue(a.Value)));
                }
            }

            foreach (var c in XmlTextOutput.LoadedChildren(e))
            {
                NavNode child = c switch
                {
                    OutElement ce => From(ce, node),
                    string s => new NavNode(XPathNodeType.Text, string.Empty, XmlTextOutput.XPathTextValue(s), node),
                    OutCData cd => new NavNode(XPathNodeType.Text, string.Empty, cd.Text, node),
                    OutPi pi => new NavNode(XPathNodeType.ProcessingInstruction, pi.Target, pi.Data, node),
                    _ => null
                };

                if (child != null)
                {
                    child.Index = node.Children.Count;
                    node.Children.Add(child);
                }
            }

            return node;
        }

        /// <summary>XPath string-value: the text, or all descendant text</summary>
        public string StringValue()
        {
            if (Type == XPathNodeType.Text || Type == XPathNodeType.ProcessingInstruction)
            {
                return Text;
            }

            if (Children.Count == 1 && Children[0].Type == XPathNodeType.Text)
            {
                return Children[0].Text;
            }

            var sb = new StringBuilder();
            Append(sb);
            return sb.ToString();
        }

        private void Append(StringBuilder sb)
        {
            foreach (var c in Children)
            {
                if (c.Type == XPathNodeType.Text)
                {
                    sb.Append(c.Text);
                }
                else if (c.Type == XPathNodeType.Element)
                {
                    c.Append(sb);
                }
            }
        }
    }
}
