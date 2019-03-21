using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace evtx.Tags
{
    class TemplateInstance:IBinXml
    {
        public TemplateInstance(long chunkOffset, long recordPosition, int index, byte[] payload)
        {
            var l = LogManager.GetLogger("BuildTag");
            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = 10 ; //default size until we know better

            index += 1; //move past op code
            var version = payload[index];
            index += 1;

            TemplateId = BitConverter.ToInt32(payload, index);
            index += 4;

            TemplateOffset = BitConverter.ToInt32(payload, index);
            index += 4;

            NextTemplateOffset = BitConverter.ToInt32(payload, index);
            index += 4;

            l.Debug($"TemplateOffset: 0x{TemplateOffset:X} NextTemplateOffset: 0x{NextTemplateOffset:X}");
//
//            l.Debug($"Template offsets: {string.Join(" | ", templates)}");
//
//            var existingTemplate = templates.SingleOrDefault(t => t.TemplateOffset == TemplateOffset);
// 
//            if (NextTemplateOffset > 0 && NextTemplateOffset>0xff)
//            {
//                existingTemplate = templates.SingleOrDefault(t => t.TemplateOffset == NextTemplateOffset);
//                if (existingTemplate != null)
//                {
//                    l.Debug($"when looking at NextTemplateOffset, found existing template with offset 0x{TemplateOffset:X}");
//                    //there is an existing one
//                    return;
//                }
//                
//
//                l.Debug($"NextTemplateOffset is greater than 0! Dealing with new template");
//                //since its not 0, another template follows
//                var nextGuid = new byte[16];
//                Buffer.BlockCopy(payload,index,nextGuid,0,16);
//                index += 16;
//                var ng = new Guid(nextGuid);
//                l.Debug($"Next Guid: {ng}");
//
//                var nextSize = BitConverter.ToInt32(payload,index);
//                index += 4;
//
//                var nextTemplatePayload = new byte[nextSize];
//                Buffer.BlockCopy(payload,index,nextTemplatePayload,0,nextSize);
//                index += nextSize;
//
//                var newT = new Template(-1,NextTemplateOffset,ng,nextTemplatePayload);
//
//                templates.Add(newT);
//                existingTemplate = newT;
//                Size= newT.Size;
//            }
//
//            if (existingTemplate != null)
//            {
//                l.Debug($"Found existing template with offset 0x{TemplateOffset:X}");
//                //there is an existing one
//                return;
//            }
//            
//            //its a new template, so process
//            l.Debug("---------------------------------------------------------------------------");
//              
//            var gb = new byte[16];
//            Buffer.BlockCopy(payload,index,gb,0,16);
//            index += 16;
//            var g = new Guid(gb);
//            l.Debug($"Guid: {g}");
//
//            var length = BitConverter.ToInt32(payload, index);
//            index += 4;
//            l.Debug($"length: 0x{length:X}");
//
//            var templateBytes = new byte[length];
//            Buffer.BlockCopy(payload,index,templateBytes,0,length);
//            Size = length + 0x22; //template length + header when its a new template
//
//            var te = new Template(TemplateId,TemplateOffset,g,templateBytes);
//
//            templates.Add(te);
//
        }

        public long ChunkOffset { get; }
        public long RecordPosition { get; }
        public int Size { get; }

        public Template Template { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }



        public int TemplateOffset { get; }
        public int NextTemplateOffset{ get; }
        public int TemplateId { get; }
    }
}
