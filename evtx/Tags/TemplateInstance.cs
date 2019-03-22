using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace evtx.Tags
{
    class TemplateInstance:IBinXml
    {
        public TemplateInstance(long chunkOffset, long recordPosition, int index, byte[] payload,Dictionary<int, Template> templates)
        {
            var l = LogManager.GetLogger("BuildTag");
            ChunkOffset = chunkOffset;
            RecordPosition = recordPosition;
            Size = 10 ; //default size until we know better

            var startPos = recordPosition + index;
            var origIndex = index;

            index += 1; //move past op code
            var version = payload[index];
            index += 1;

            TemplateId = BitConverter.ToInt32(payload, index);
            index += 4;

            TemplateOffset = BitConverter.ToInt32(payload, index);
            index += 4;

            NextTemplateOffset = BitConverter.ToInt32(payload, index);
            index += 4;

            if (templates.ContainsKey((int) (chunkOffset + TemplateOffset)) == false)
            {
                Template tt = null;

                try
                {
                    tt = GetTemplateFromPayload(origIndex,payload,chunkOffset+startPos);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    //tt = GetTemplateFromPayload(origIndex,payload,chunkOffset+startPos - 0x10000);

                    tt = templates[(int) (chunkOffset - 0x10000 + TemplateOffset)];
                }


               templates.Add((int) (chunkOffset + TemplateOffset),tt);
            }
            else
            {
                Template tt1 = null;

                    try
                    {
                        tt1 = GetTemplateFromPayload(origIndex,payload,chunkOffset+startPos);
                    }
                    catch (Exception )
                    {
                        
                    }

                    if (tt1 != null)
                    {
                        templates[(int) (chunkOffset + TemplateOffset)] = tt1;
                    }

            }
            
            
            Template = templates[(int) (chunkOffset + TemplateOffset)];
            Size = Template.Size + 0x22;
            if (TemplateOffset < startPos)
            {
                //the template has already been defined, so we just need to send back the placeholder size, which is 10
                Size = 10;
            }
            

        }

        private Template GetTemplateFromPayload(int index, byte[] payload, long absoluteOffset)
        {
           
            index += 1;

           var version = payload[index];
            index += 1;

            var templateId = BitConverter.ToInt32(payload, index);
            index += 4;

            var  templateOffset = BitConverter.ToInt32(payload, index);
            index += 4;

            var  nextTemplateOffset = BitConverter.ToInt32(payload, index);
            index += 4;

            var gb = new byte[16];
            Buffer.BlockCopy(payload,index,gb,0,16);
            index += 16;
            var g = new Guid(gb);

            var length = BitConverter.ToInt32(payload, index);
            index += 4;

            var templateBytes = new byte[length];
            Buffer.BlockCopy(payload,index,templateBytes,0,length);

           return  new Template(templateId,templateOffset,g,templateBytes,nextTemplateOffset, absoluteOffset);
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
