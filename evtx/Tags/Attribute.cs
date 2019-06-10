using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NLog;

namespace evtx.Tags
{
    public class Attribute : IBinXml
    {
        public Attribute(long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("Attribute");

            RecordPosition = recordPosition;

            //we need the size of the attribute
            dataStream.BaseStream.Seek(-5, SeekOrigin.Current);
            Size = dataStream.ReadInt32();

            var op = dataStream.ReadByte();
            Trace.Assert(op == 6 || op == 0x46, $"op is 0x{op:X}");

            var nameOffset = dataStream.ReadUInt32();
            var nameElement = chunk.GetStringTableEntry(nameOffset);

            Name = nameElement.Value;

            if (nameOffset > recordPosition && Size>9) //if size == 9 then it cannot contain a name
            {
                dataStream.BaseStream.Seek(nameElement.Size, SeekOrigin.Current);
            }

            AttributeInfo = TagBuilder.BuildTag(recordPosition, dataStream, chunk);

            switch (AttributeInfo)
            {
                case NormalSubstitution nsv:
                case OptionalSubstitution osv:
                    //this will be substituted when actually populating the record with record data
                    break;
                case Value vv:
                    Value = vv.ValueData;
                    break;
 
                default:
                    throw new Exception($"Unknown attribute info ({AttributeInfo.GetType()})! Please send the file to saericzimmerman@gmail.com");
                  
            }

            l.Trace(this);
        }

        public IBinXml AttributeInfo { get; }

        public string Name { get; }
        public string Value { get; }

        public long RecordPosition { get; }
        public long Size { get; }


        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.Attribute;

        public string AsXml(List<SubstitutionArrayEntry> substitutionEntries, long parentOffset)
        {
            string val;
            if (AttributeInfo is Value)
            {
                val = Value;
            }
            else if (AttributeInfo is OptionalSubstitution os)
            {
                var se = substitutionEntries.Single(t => t.Position == os.SubstitutionId);

                if (se.ValType == TagBuilder.ValueType.NullType)
                {
                    return "";
                }

                val = substitutionEntries.Single(t => t.Position == os.SubstitutionId).GetDataAsString();
            }
            else
            {
                var ns = (NormalSubstitution) AttributeInfo;
                val = substitutionEntries.Single(t => t.Position == ns.SubstitutionId).GetDataAsString();
            }

            return $"{Name}=\"{val}\"";
        }

        public override string ToString()
        {
            return $"Attribute {AttributeInfo}";
        }
    }
}