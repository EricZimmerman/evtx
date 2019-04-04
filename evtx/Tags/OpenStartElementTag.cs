using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NLog;

namespace evtx.Tags
{
    public class OpenStartElementTag : IBinXml
    {
        public OpenStartElementTag( long recordPosition, BinaryReader dataStream, ChunkInfo chunk, bool hasAttribute)
        {
            var l = LogManager.GetLogger("BuildTag");

            RecordPosition = recordPosition;
            
            Nodes = new List<IBinXml>();
            
            SubstitutionSlot = dataStream.ReadInt16();
            
            Size = dataStream.ReadInt32();

            var startPos = dataStream.BaseStream.Position;
           
           
            var elementOffset = dataStream.ReadUInt32();

          // var knownString = chunk.StringTableContainsOffset(elementOffset);

            var elementName = chunk.GetStringTableEntry(elementOffset);

            var subinfo = string.Empty;
            if (SubstitutionSlot > -1)
            {
                subinfo = $", SubstitutionSlot: {SubstitutionSlot}";
            }


            if (elementOffset < recordPosition )
            {
                Debug.WriteLine(1);
            }
            else
            {
                dataStream.BaseStream.Seek(elementName.Size, SeekOrigin.Current);    
            }
          
            
        
                
            
            

            Attribute attribute = null;

            if (hasAttribute)
            {
                var attrSize = dataStream.ReadInt32();
                if (attrSize == 0x15)
                {
                    Debug.WriteLine(1);
                }
                attribute =(Attribute) TagBuilder.BuildTag( recordPosition, dataStream, chunk);
            }

            var i = TagBuilder.BuildTag( recordPosition, dataStream, chunk);
            
            Trace.Assert(i is CloseStartElementTag);

            Nodes.Add(i);
            

            var att = string.Empty;
            if (attribute != null)
            {
                att = $", attribute: {attribute}";
            }

            l.Debug($"Name: {elementName.Value}{subinfo}{att}");

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
        public int SubstitutionSlot { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.OpenStartElementTag;
    }
}