using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NLog;

namespace evtx.Tags
{
    public class OpenStartElementTag : IBinXml
    {
        private readonly ChunkInfo _chunk;

        public OpenStartElementTag(long recordPosition, BinaryReader dataStream, ChunkInfo chunk, bool hasAttribute)
        {
            var l = LogManager.GetLogger("BuildTag");

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
                l.Warn($"Unexpected data at offset 0x{(chunk.AbsoluteOffset+recordPosition+dataStream.BaseStream.Position):X}! This usually means the record is corrupt or incomplete!");
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

            l.Trace($"Name: {Name.Value}{subinfo}{att}");

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
            var sb = new StringBuilder();

            sb.Append($"<{Name.Value}");

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
                sb.Append(" " + string.Join(" ", attrStrings));
            }

            foreach (var node in Nodes)
            {
                if (node is EndElementTag)
                {
                    sb.AppendLine($"</{Name.Value}>");
                }
                else if (node is CloseEmptyElementTag)
                {
                    sb.AppendLine(node.AsXml(substitutionEntries, parentOffset));
                }
                else if (node is CloseStartElementTag)
                {
                    sb.Append(node.AsXml(substitutionEntries, parentOffset));
                }
                else
                {
//                    if (Name.Value == "Keywords" && node is OptionalSubstitution kw)
//                    {
//                        var subBytes = substitutionEntries.Single(t => t.Position == kw.SubstitutionId).DataBytes;
//
//                        if (subBytes.Length >= 8)
//                        {
//                            var kwVal = BitConverter.ToUInt64(subBytes, 0);
//                            sb.Append($"{TagBuilder.GetKeywordDescription(kwVal)}");
//                        }
//                        else
//                        {
//                            sb.Append($"{TagBuilder.GetKeywordDescription(1)}");
//                        }
//                    }
//                    else 
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
                                        sb.AppendLine(te.AsXml(te.SubstitutionEntries, parentOffset));
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
                                sb.Append(escapedo);
                            }
                        }
                        else
                        {
                            //normal sub
                            var escapedn = new XText(node.AsXml(substitutionEntries, parentOffset)).ToString();
                            sb.Append(escapedn);
                            //sb.Append(node.AsXml(substitutionEntries,parentOffset));
                        }
                    }
                    else
                    {
                        sb.Append(node.AsXml(substitutionEntries, parentOffset));
                    }
                }
            }


            return sb.ToString();
        }
    }
}