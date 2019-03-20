using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace evtx.Tags
{
public    class Template: IBinXml
    {
        public Template(int chunkOffset, int recordPosition, byte[] templateBytes, int templateOffset)
        {
            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = templateBytes.Length;
            Nodes = new List<IBinXml>();

            TemplateOffset = templateOffset;

//            index += 1; //unknown, version?
//            var templateId = BitConverter.ToInt32(PayloadBytes, index);
//            index += 4;
//            var templateOffset = BitConverter.ToInt32(PayloadBytes, index);
//            index += 4;
//            l.Debug($"Template ID: 0x{templateId:X} Template recordPosition: 0x{templateOffset:X}");
//
//            var templateOffset2 = BitConverter.ToInt32(PayloadBytes, index);
//                       
//            index += 4;
//            var templateId2 = BitConverter.ToInt32(PayloadBytes, index);
//            //normally we would slide up the size of the byts used, but...
//            //do not increase it as the GUID overlaps it. makes sense right?
//
//            l.Debug($"Template ID2: 0x{templateId2:X} Template offset2: 0x{templateOffset2:X}");
//                        
//            var gb = new byte[16];
//            Buffer.BlockCopy(PayloadBytes,index,gb,0,16);
//            index += 16;
//            var g = new Guid(gb);
//            l.Debug($"Guid: {g}");
//
//            var length = BitConverter.ToInt32(PayloadBytes, index);
//            index += 4;
//            l.Debug($"length: 0x{length:X}");

        }

        public List<IBinXml> Nodes { get; set; }

        public int ChunkOffset { get; }
        public int RecordPosition { get; }
        public int Size { get; }

        public int TemplateOffset { get; }
    }
}
