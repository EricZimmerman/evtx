using System;
using System.Diagnostics;
using System.IO;
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

            if (nameOffset > recordPosition)
            {
                dataStream.BaseStream.Seek(nameElement.Size, SeekOrigin.Current);
            }

            AttributeInfo = TagBuilder.BuildTag(recordPosition, dataStream, chunk);

            switch (AttributeInfo)
            {
                case OptionalSubstitution osv:
                    //this will be substituted when actually populating the record with record data
                    break;
                case Value vv:
                    Value = vv.ValueData;
                    break;
                default:
                    throw new Exception("Unknown attribute info! Please send the file to saericzimmerman@gmail.com");
            }

            l.Trace(this);
        }

        public IBinXml AttributeInfo { get; }

        public string Name { get; }
        public string Value { get; }

        public long RecordPosition { get; }
        public long Size { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.Attribute;

        public override string ToString()
        {
            return $"Attribute {AttributeInfo}";
        }
    }
}