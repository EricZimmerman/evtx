using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NLog;

namespace evtx.Tags
{
    public class OpenStartElementTag : IBinXml
    {
        public OpenStartElementTag( long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("BuildTag");

            RecordPosition = recordPosition;

            

            Nodes = new List<IBinXml>();

            //l.Debug($"stream pos at start: 0x{dataStream.BaseStream.Position:X}");

            //we need to know if this was a 1 vs a 41 so we know if there are attributes
            dataStream.BaseStream.Seek(-1, SeekOrigin.Current);
            var opTag = (TagBuilder.BinaryTag)dataStream.ReadByte();
            //l.Debug($"opTag: {opTag}");

            Dependency = dataStream.ReadInt16();

            

            //l.Debug($"dependencyId: 0x{dependencyId:X}");

            Size = dataStream.ReadInt32();
           // l.Debug($"OpenStartElementTag size: 0x{Size:X}");

           //hack

           dataStream.BaseStream.Seek(Size, SeekOrigin.Current);
           return;


            var startPos = dataStream.BaseStream.Position;
            var elementOffset = dataStream.ReadUInt32();

            var knownString = chunk.StringTableContainsOffset(elementOffset);

            var elementName = chunk.GetStringTableEntry(elementOffset);

            l.Debug($"Name: {elementName.Value}, Dependency: {Dependency}");

            if (Dependency == 19)
            {
                Debug.WriteLine(1);
            }



      

             dataStream.BaseStream.Seek(elementName.Size, SeekOrigin.Current); 
       
            

            if (opTag == TagBuilder.BinaryTag.OpenStartElementTag2)
            {
                var attrSize = dataStream.ReadInt32();
                var a = TagBuilder.BuildTag( recordPosition, dataStream, chunk);
            }

            var i = TagBuilder.BuildTag( recordPosition, dataStream, chunk);

          

            while (dataStream.BaseStream.Position<startPos+Size)
            {
                var n = TagBuilder.BuildTag( recordPosition, dataStream, chunk);

              //  l.Debug($"Dumping rest: {n}");

                Nodes.Add(n);

            }

            

            


        }

        public List<IBinXml> Nodes { get; set; }

        public long RecordPosition { get; }
        public long Size { get; }
        public int Dependency { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.OpenStartElementTag;
    }
}