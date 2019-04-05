using System;
using System.Collections.Generic;
using System.IO;
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

            var startPos = recordPosition + dataStream.BaseStream.Position;
            var origIndex = dataStream.BaseStream.Position;

            var version = dataStream.ReadByte();

            //TODO
            //can we not tract these internally since they are just going to be in the Template itself anyways?
            TemplateId = dataStream.ReadInt32();

            TemplateOffset = dataStream.ReadInt32();

            NextTemplateOffset = dataStream.ReadInt32();

            Template = chunk.GetTemplate(TemplateOffset);

            Size = Template.Size;
            if (TemplateOffset <  chunk.AbsoluteOffset - recordPosition)
            {
                //the template has already been defined, so we need to back up before NextTemplateOffset
                dataStream.BaseStream.Seek(-4, SeekOrigin.Current);
            }
            else
            {
                //new template, so we have to slide forward a bit to get to beginning of template
                dataStream.BaseStream.Seek(0x14, SeekOrigin.Current);
                //fake for now
                dataStream.BaseStream.Seek(Size, SeekOrigin.Current);
            }

            //Template contains the...TEMPLATE and it has a Nodes collection to use later. This, along with the substitution stuff coming next, are what is needed to build an event record.
            
            l.Trace($"     dataStream.BaseStream post template is 0x{dataStream.BaseStream.Position:X}");

            //substitution array starts here
            //first is 32 bit # with how many to expect
            //followed by that # of pairs of 16 bit numbers, first is length, second is type
            var substitutionArrayLen = dataStream.ReadInt32();
            
            l.Trace($"      Substitution length: 0x{substitutionArrayLen:X}");

            var totalSubsize = 0;
            for (var i = 0; i < substitutionArrayLen; i++)
            {
                var subSize = dataStream.ReadUInt16();
                var subType = dataStream.ReadUInt16();

                totalSubsize += subSize;

                l.Trace(
                    $"     Position: {i.ToString().PadRight(5)} Size: 0x{subSize.ToString("X").PadRight(5)} Type: {(TagBuilder.ValueType) subType}");

                SubstitutionEntries.Add(new SubstitutionArrayEntry(i, subSize, (TagBuilder.ValueType) subType));
            }

            l.Trace($"     dataStream.BaseStream post sub array is 0x{dataStream.BaseStream.Position:X}");

            l.Trace($"Substitution data length: 0x{totalSubsize:X}");
            //get the data into the substitution array entries
            foreach (var substitutionArrayEntry in SubstitutionEntries)
            {
                substitutionArrayEntry.DataBytes = dataStream.ReadBytes(substitutionArrayEntry.Size); 
                l.Trace($"       {substitutionArrayEntry}");
            }
        }

        public Template Template { get; }

        public List<SubstitutionArrayEntry> SubstitutionEntries { get; }

        public int TemplateOffset { get; }
        public int NextTemplateOffset { get; }
        public int TemplateId { get; }

        public long RecordPosition { get; }
        public long Size { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.TemplateInstance;
    }
}