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

            l.Debug($"recordPositionrecordPosition: 0x{recordPosition:X}");

            if (recordPosition == 0x7DB)
            {
                Debug.WriteLine(1);
            }

            RecordPosition = recordPosition;
            
            Nodes = new List<IBinXml>();
            Attributes = new List<Attribute>();
            
            SubstitutionSlot = dataStream.ReadInt16();
            
            Size = dataStream.ReadInt32();

            var startPos = dataStream.BaseStream.Position;
           
           
            var elementOffset = dataStream.ReadUInt32();

            var elementName = chunk.GetStringTableEntry(elementOffset);

            var subinfo = string.Empty;
            if (SubstitutionSlot > -1)
            {
                subinfo = $", SubstitutionSlot: {SubstitutionSlot}";
            }

            if (elementOffset > recordPosition+startPos )
            {
                dataStream.BaseStream.Seek(elementName.Size, SeekOrigin.Current);    
            }
          
            if (hasAttribute)
            {
                var attrSize = dataStream.ReadInt32();
                var attrStartPos = dataStream.BaseStream.Position;

                while (dataStream.BaseStream.Position < attrStartPos+attrSize)
                {
                    var attrTag = TagBuilder.BuildTag( recordPosition, dataStream, chunk);

                    if (attrTag is Attribute attrib)
                    {
                        Attributes.Add(attrib);
                    }
                }
            }

            var i = TagBuilder.BuildTag( recordPosition, dataStream, chunk);
      
            Trace.Assert(i is CloseStartElementTag, "I didnt get a CloseStartElementTag");

            Nodes.Add(i);

            var att = string.Empty;
            if (Attributes.Count>0)
            {
                att = $", attributes: {string.Join(" | ",Attributes)}";
            }

            l.Debug($"Name: {elementName.Value}{subinfo}{att}");

            while (dataStream.BaseStream.Position<startPos+Size)
            {
                var n = TagBuilder.BuildTag( recordPosition, dataStream, chunk);


                Nodes.Add(n);

//                                        if (n is EndOfBXmlStream)
//                        {
//                            break;
//                        }

            }

            

            


        }

        public List<Attribute> Attributes { get; set; }
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