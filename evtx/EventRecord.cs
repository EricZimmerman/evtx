using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using evtx.Tags;
using NLog;

namespace evtx
{
   public class EventRecord
    {
    
        


        public string ConvertPayloadToXml()
        {
            var sb = new StringBuilder();

            return sb.ToString();
        }

        

        public List<IBinXml> Nodes { get; set; }

        private Template _template;

        public EventRecord(byte[] recordBytes, int recordPosition, long chunkOffset,List<Template> templates)
        {
            var l = LogManager.GetLogger("EventRecord");

            RecordPosition = recordPosition;
            ChunkOffset = chunkOffset;

            Size = BitConverter.ToUInt32(recordBytes, 4);
            RecordNumber = BitConverter.ToInt64(recordBytes, 8);
            Timestamp = DateTimeOffset.FromFileTime(BitConverter.ToInt64(recordBytes, 0x10)).ToUniversalTime();

            PayloadBytes = new byte[recordBytes.Length - 28]; //4 for signature, 4 for first size, 8 for record #, 8 for timestamp, 4 for last size
            Buffer.BlockCopy(recordBytes,0x18,PayloadBytes,0,PayloadBytes.Length);

             if (PayloadBytes[0] != 0xf)
            {
                throw new Exception("Payload does not start with 0x1f!");
            }

         l.Debug($"\r\nChunk: 0x{ChunkOffset:X} Record position: 0x{RecordPosition:X} Record #: {RecordNumber}");

            var index = 0;
            var inStream = true;

            while (inStream && index<PayloadBytes.Length)
            {
                var op = (byte)PayloadBytes[index];

                op = (byte)(op & 0x0f);

                var opCode = (TagBuilder.BinaryTag)op;

                l.Debug($"     Opcode: {opCode} at absolute offset:  0x {(chunkOffset+ recordPosition+index + 24):X}");

                switch (opCode)
                {

//                    case BinaryTag.TokenEntityRef:
//                    case BinaryTag.EndElementTag:
//                    case BinaryTag.TokenPITarget:
//                    case BinaryTag.CloseEmptyElementTag:
//                    case BinaryTag.CDataSection:
//                    case BinaryTag.TokenPIData:
//                    case BinaryTag.OpenStartElementTag:
//                        index = PayloadBytes.Length; 
//                        break;
//                    case BinaryTag.OpenStartElementTag2:
//
//                        break;
//

                    case TagBuilder.BinaryTag.TokenPIData:

                        inStream = false; //not handled yet
                        continue;
                      


                    case TagBuilder.BinaryTag.EndOfBXmlStream:
                        var endStrean = TagBuilder.BuildTag(chunkOffset,recordPosition, PayloadBytes, index,templates);

                        index += endStrean.Size;

                        break;
                    case TagBuilder.BinaryTag.StartOfBXmlStream:

                        var startStream = TagBuilder.BuildTag(chunkOffset,recordPosition, PayloadBytes, index,templates);

                        index += startStream.Size;
                      
                        break;
                    case TagBuilder.BinaryTag.TemplateInstance:

                        var tsss = TagBuilder.BuildTag(chunkOffset,recordPosition,PayloadBytes, index,templates);

                        index += tsss.Size;

                        l.Debug($"Post template index: 0x {index:X}");

//                        //substitution array starts here
//                        //first is 32 bit # with how many to expect
//                        //followed by that # of pairs of 16 bit numbers, first is length, second is type
//
//                        //set index to where value spec lives
//                        index = valueSpecOffset;
//                        l.Trace($"      valueSpecOffset: 0x{valueSpecOffset:X}");
//
                        var substitutionArrayLen = BitConverter.ToInt32(PayloadBytes, index);
                        index += 4;
//
                        l.Debug($"      Substitution len: 0x{substitutionArrayLen:X}");
//
//
                        var subList = new List<SubstitutionArrayEntry>();
//
                        var totalSubsize = 0;
                        for (var i = 0; i < substitutionArrayLen; i++)
                        {
                   
                            var subSize = BitConverter.ToInt16(PayloadBytes, index);
                            index += 2;
                            var subType = BitConverter.ToInt16(PayloadBytes, index);
                            index += 2;

                            totalSubsize += subSize;

                            l.Trace($"     Position: {i.ToString().PadRight(5)} Size: 0x{subSize.ToString("X").PadRight(5)} Type: {(TagBuilder.ValueType)subType}");

                            subList.Add(new SubstitutionArrayEntry(i, subSize,(TagBuilder.ValueType)subType));
                        }

                        l.Trace($"     Index post sub array is 0x{index:X} totalSubsize: 0x{totalSubsize:X}");
                        
                        l.Trace("Substitution data");
                        //get the data into the substitution array entries
                        foreach (var substitutionArrayEntry in subList)
                        {
                            var data = new byte[substitutionArrayEntry.Size];

                            Buffer.BlockCopy(PayloadBytes,index,data,0,substitutionArrayEntry.Size);
                            index += substitutionArrayEntry.Size;
                            substitutionArrayEntry.DataBytes = data;

                            l.Debug($"       {substitutionArrayEntry}");
                        }

                        inStream = false; //done working


                        break;

                    default:
                        throw new Exception($"Unknown opcode: {opCode} ({opCode:X}) index: 0x{index:X} chunkoffset: 0x{chunkOffset:X} abs offset: 0x {(chunkOffset+ recordPosition+index + 24):X}");
                }


            }
            

        }



        public int RecordPosition { get;  }
        public long ChunkOffset { get;  }
        
        public uint Size { get; }
        public long RecordNumber { get; }
        public DateTimeOffset Timestamp { get; }

        public byte[] PayloadBytes { get; }

        public override string ToString()
        {
            return $"ChunkOffset: 0x{ChunkOffset:X} RecordPosition: 0x{RecordPosition:X} RecordNumber: 0x{RecordNumber:X} ({RecordNumber}) Timestamp: {Timestamp}";
        }
    }
}
