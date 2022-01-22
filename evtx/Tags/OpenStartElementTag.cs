using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Serilog;

namespace evtx.Tags;

public class OpenStartElementTag : IBinXml
{
    private readonly ChunkInfo _chunk;

    public OpenStartElementTag(long recordPosition, BinaryReader dataStream, ChunkInfo chunk, bool hasAttribute)
    {

        _chunk = chunk;

        RecordPosition = recordPosition;

        Nodes = new List<IBinXml>();
        Attributes = new List<Attribute>();

        SubstitutionSlot = dataStream.ReadInt16();

        Size = dataStream.ReadInt32();

        var startPos = dataStream.BaseStream.Position;

        var elementOffset = dataStream.ReadUInt32();

        Name = chunk.GetStringTableEntry(elementOffset);

        var subinfo = string.Empty;
        if (SubstitutionSlot > -1)
        {
            subinfo = $", Substitution Slot: {SubstitutionSlot}";
        }

        if (elementOffset > recordPosition + startPos)
        {
            dataStream.BaseStream.Seek(Name.Size, SeekOrigin.Current);
        }

        if (hasAttribute)
        {
            var attrSize = dataStream.ReadInt32();
            var attrStartPos = dataStream.BaseStream.Position;

            while (dataStream.BaseStream.Position < attrStartPos + attrSize)
            {
                var attrTag = TagBuilder.BuildTag(recordPosition, dataStream, chunk);

                if (attrTag is Attribute attribute)
                {
                    Attributes.Add(attribute);
                }
            }
        }

        var i = TagBuilder.BuildTag(recordPosition, dataStream, chunk);

        if (i is EndOfBXmlStream)
        {
            Log.Warning(
                "Unexpected data at offset 0x{Offset:X}! This usually means the record is corrupt or incomplete!",chunk.AbsoluteOffset + recordPosition + dataStream.BaseStream.Position);
            return;
        }

        Trace.Assert(i is CloseStartElementTag || i is CloseEmptyElementTag,
            $"I didn't get a CloseStartElementTag: {i.GetType()}");

        Nodes.Add(i);

        var att = string.Empty;
        if (Attributes.Count > 0)
        {
            att = $", attributes: {string.Join(" | ", Attributes)}";
        }

        Log.Verbose("Name: {Name.Value}{SubInfo}{Att}",Name.Value,subinfo,att);

        while (dataStream.BaseStream.Position < startPos + Size)
        {
            var n = TagBuilder.BuildTag(recordPosition, dataStream, chunk);

            Nodes.Add(n);
        }
    }

    public StringTableEntry Name { get; }

    public List<Attribute> Attributes { get; set; }
    public List<IBinXml> Nodes { get; set; }
    public int SubstitutionSlot { get; }

    public long RecordPosition { get; }
    public long Size { get; }

    public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.OpenStartElementTag;

    public string AsXml(List<SubstitutionArrayEntry> substitutionEntries, long parentOffset)
    {
        var sb = string.Empty;

        sb +=($"<{Name.Value}");

        var attrStrings = new List<string>();

        foreach (var attribute in Attributes)
        {
            var attrVal = attribute.AsXml(substitutionEntries, parentOffset);
            if (attrVal.Length > 0)
            {
                attrStrings.Add(attrVal);
            }
        }

        if (attrStrings.Count > 0)
            //at least one attribute with a value
        {
            sb+=(" " + string.Join(" ", attrStrings));
        }

        foreach (var node in Nodes)
        {
            if (node is EndElementTag)
            {
                sb+=($"</{Name.Value}>");
            }
            else if (node is CloseEmptyElementTag)
            {
                sb+=(node.AsXml(substitutionEntries, parentOffset));
            }
            else if (node is CloseStartElementTag)
            {
                sb+=(node.AsXml(substitutionEntries, parentOffset));
            }
            else
            {
                if (node is OptionalSubstitution || node is NormalSubstitution)
                {
                    if (node is OptionalSubstitution os)
                    {
                        if (os.ValueType == TagBuilder.ValueType.BinXmlType)
                        {
                            var osData = substitutionEntries.Single(t => t.Position == os.SubstitutionId);
                            var ms = new MemoryStream(osData.DataBytes);
                            var br = new BinaryReader(ms);

                            while (br.BaseStream.Position < br.BaseStream.Length)
                            {
                                var nextTag = TagBuilder.BuildTag(parentOffset, br, _chunk);

                                if (nextTag is TemplateInstance te)
                                {
                                    sb+=(te.AsXml(te.SubstitutionEntries, parentOffset));
                                }

                                if (nextTag is EndOfBXmlStream)
                                    //nothing left to do, so exit
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            //optional sub
                            var escapedo = new XText(node.AsXml(substitutionEntries, parentOffset)).ToString();
                            sb+=(escapedo);
                        }
                    }
                    else
                    {
                        //normal sub
                        var escapedn = new XText(node.AsXml(substitutionEntries, parentOffset)).ToString();
                        sb+=(escapedn);
                        //sb.Append(node.AsXml(substitutionEntries,parentOffset));
                    }
                }
                else
                {
                    sb+=(node.AsXml(substitutionEntries, parentOffset));
                }
            }
        }


        return sb;
    }
}