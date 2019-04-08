using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NLog;

namespace evtx.Tags
{
    internal class TemplateInstance : IBinXml
    {
        public TemplateInstance(long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("BuildTag");

            RecordPosition = recordPosition;
            Size = 10; //default size until we know better

            SubstitutionEntries = new List<SubstitutionArrayEntry>();

            var version = dataStream.ReadByte();

            //TODO
            //can we not track these internally since they are just going to be in the Template itself anyways?
            dataStream.ReadInt32(); //TemplateId

            var templateOffset = dataStream.ReadInt32();

            dataStream.ReadInt32(); //NextTemplateOffset

            Template = chunk.GetTemplate(templateOffset);

            Size = Template.Size;
            if (templateOffset < recordPosition)
            {
                //the template has already been defined, so we need to back up before NextTemplateOffset
                dataStream.BaseStream.Seek(-4, SeekOrigin.Current);
            }
            else
            {
                //new template, so we have to slide forward a bit to get to beginning of template
                dataStream.BaseStream.Seek(0x14, SeekOrigin.Current);
                //we do not need to process the template bytes here since we did it once when the chunk was originally processed
                dataStream.BaseStream.Seek(Size, SeekOrigin.Current);
            }

            //Template contains the...TEMPLATE and it has a Nodes collection to use later. This, along with the substitution stuff coming next, are what is needed to build an event record.

            //substitution array starts here
            //first is 32 bit # with how many to expect
            //followed by that # of pairs of 16 bit numbers, first is length, second is type
            var substitutionArrayLen = dataStream.ReadInt32();

            l.Trace($"      Substitution length: 0x{substitutionArrayLen:X}");

            var totalSubstitutionSize = 0;
            for (var i = 0; i < substitutionArrayLen; i++)
            {
                var subSize = dataStream.ReadUInt16();
                var subType = dataStream.ReadUInt16();

                totalSubstitutionSize += subSize;

                l.Trace(
                    $"     Position: {i.ToString().PadRight(5)} Size: 0x{subSize.ToString("X").PadRight(5)} Type: {(TagBuilder.ValueType) subType}");

                SubstitutionEntries.Add(new SubstitutionArrayEntry(i, subSize, (TagBuilder.ValueType) subType));
            }

            l.Trace($"Substitution data length: 0x{totalSubstitutionSize:X}");
            //get the data into the substitution array entries
            foreach (var substitutionArrayEntry in SubstitutionEntries)
            {
                substitutionArrayEntry.DataBytes = dataStream.ReadBytes(substitutionArrayEntry.Size);
                l.Trace($"       {substitutionArrayEntry}");
            }
        }

        public Template Template { get; }

        public List<SubstitutionArrayEntry> SubstitutionEntries { get; }


        public long RecordPosition { get; }
        public long Size { get; }

        public string AsXml(List<SubstitutionArrayEntry> substitutionEntries, long parentOffset)
        {
            var sb = new StringBuilder();

            if (Template == null)
            {
                return "Template is null";
            }

            foreach (var templateNode in Template.Nodes)
            {
                switch (templateNode.TagType)
                {
                    case TagBuilder.BinaryTag.EndOfBXmlStream:
                        break;
                    case TagBuilder.BinaryTag.OpenStartElementTag:
                        sb.AppendLine(templateNode.AsXml(SubstitutionEntries,parentOffset));
                        break;
                    
                    case TagBuilder.BinaryTag.StartOfBXmlStream:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return sb.ToString();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.TemplateInstance;
     
    }
}