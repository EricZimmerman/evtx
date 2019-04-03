using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace evtx.Tags
{
    class Attribute:IBinXml
    {
        public Attribute( long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("Attribute");

            RecordPosition = recordPosition;

            //we need the size of the attribute
            dataStream.BaseStream.Seek(-5, SeekOrigin.Current);
            Size = dataStream.ReadInt32();

           // l.Debug($"Attr size is: 0x{Size:X}");

            var op = dataStream.ReadByte();
            Trace.Assert(op==6);

            var nameOffset = dataStream.ReadUInt32();
            var nameElement = chunk.GetStringTableEntry(nameOffset);

            

            dataStream.BaseStream.Seek(nameElement.Size, SeekOrigin.Current);

            var v = TagBuilder.BuildTag( recordPosition, dataStream, chunk);

            if (v is OptionalSubstitution osv)
            {
                l.Debug($"optional attribute {nameElement.Value} --> {osv.SubstitutionId} ({osv.ValueType})");
            }
            else if (v is Value vv)
            {
                l.Debug($"attribute {nameElement.Value} --> {vv.ValueData}");
            }
            else
            {
                l.Debug($"attribute {nameElement.Value} --> {v}");    
            }
            
            

        }

        public long RecordPosition { get; }
        public long Size { get; }
        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.Attribute;
    }
}
