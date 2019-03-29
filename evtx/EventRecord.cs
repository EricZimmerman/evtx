using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using evtx.Tags;
using NLog;

namespace evtx
{
    public class EventRecord
    {
        private Template _template;

        public EventRecord(BinaryReader recordData, int recordPosition, long chunkOffset, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("EventRecord");

            RecordPosition = recordPosition;
            ChunkOffset = chunkOffset;

            recordData.ReadInt32(); //signature

            Size = recordData.ReadUInt32();
            RecordNumber = recordData.ReadInt64(); // .ToInt64(recordBytes, 8);
            Timestamp = DateTimeOffset.FromFileTime(recordData.ReadInt64()).ToUniversalTime();

            if (recordData.PeekChar() != 0xf)
            {
                throw new Exception("Payload does not start with 0x1f!");
            }

            l.Debug(
                $"Chunk: 0x{ChunkOffset:X8} Record position: 0x{RecordPosition:X4} Record #: {RecordNumber.ToString().PadRight(3)} Timestamp: {Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}");

            var index = 0;
            var inStream = true;

//            while (inStream && index < PayloadBytes.Length)
//            {
//                var op = PayloadBytes[index];
//
//                op = (byte) (op & 0x0f);
//
//                var opCode = (TagBuilder.BinaryTag) op;
//
//                l.Trace($"     Opcode: {opCode} at absolute offset:  0x {chunkOffset + recordPosition + index + 24:X}");
//
//                switch (opCode)
//                {
////                    case BinaryTag.TokenEntityRef:
////                    case BinaryTag.EndElementTag:
////                    case BinaryTag.TokenPITarget:
////                    case BinaryTag.CloseEmptyElementTag:
////                    case BinaryTag.CDataSection:
////                    case BinaryTag.TokenPIData:
////                    case BinaryTag.OpenStartElementTag:
////                        index = PayloadBytes.Length; 
////                        break;
////                    case BinaryTag.OpenStartElementTag2:
////
////                        break;
////
//
//                  //  case TagBuilder.BinaryTag.TokenPIData:
//
//                    case TagBuilder.BinaryTag.EndOfBXmlStream:
//                        var endStrean =
//                            TagBuilder.BuildTag(chunkOffset, recordPosition, PayloadBytes, index, templates);
//
//                        index += endStrean.Size;
//
//                        inStream = false; //done working
//
//                        break;
//                    case TagBuilder.BinaryTag.StartOfBXmlStream:
//
//                        var startStream =
//                            TagBuilder.BuildTag(chunkOffset, recordPosition, PayloadBytes, index, templates);
//
//                        index += startStream.Size;
//
//                        break;
//                    case TagBuilder.BinaryTag.TemplateInstance:
//                        var ti = (TemplateInstance) TagBuilder.BuildTag(chunkOffset, recordPosition, PayloadBytes, index, templates);
//
//                        index += ti.Size;
//
//                        l.Trace($"Post template index: 0x {index:X}");
//
////                        //substitution array starts here
////                        //first is 32 bit # with how many to expect
////                        //followed by that # of pairs of 16 bit numbers, first is length, second is type
//                        var substitutionArrayLen = BitConverter.ToInt32(PayloadBytes, index);
//                        index += 4;
//                        l.Trace($"      Substitution len: 0x{substitutionArrayLen:X}");
////
//                        var totalSubsize = 0;
//                        for (var i = 0; i < substitutionArrayLen; i++)
//                        {
//                            var subSize = BitConverter.ToUInt16(PayloadBytes, index);
//                            index += 2;
//                            var subType = BitConverter.ToUInt16(PayloadBytes, index);
//                            index += 2;
//
//                            totalSubsize += subSize;
//
//                            l.Trace(
//                                $"     Position: {i.ToString().PadRight(5)} Size: 0x{subSize.ToString("X").PadRight(5)} Type: {(TagBuilder.ValueType) subType}");
//
//                            ti.SubstitutionEntries.Add(new SubstitutionArrayEntry(i, subSize, (TagBuilder.ValueType) subType));
//                        }
//
//                        l.Trace($"     Index post sub array is 0x{index:X}");
//
//                        l.Debug($"Substitution data. Data length: 0x{totalSubsize:X}");
//                        //get the data into the substitution array entries
//                        foreach (var substitutionArrayEntry in ti.SubstitutionEntries)
//                        {
//                            var data = new byte[substitutionArrayEntry.Size];
//
//                            Buffer.BlockCopy(PayloadBytes, index, data, 0, substitutionArrayEntry.Size);
//                            index += substitutionArrayEntry.Size;
//                            substitutionArrayEntry.DataBytes = data;
//
//                            l.Debug($"       {substitutionArrayEntry}");
//                        }
//
//
//                        break;
//
//                    default:
//                        throw new Exception(
//                            $"Unknown opcode: {opCode} ({opCode:X}) index: 0x{index:X} chunkoffset: 0x{chunkOffset:X} abs offset: 0x {chunkOffset + recordPosition + index + 24:X}");
//                }
//            }
        }


        public List<IBinXml> Nodes { get; set; }


        public int RecordPosition { get; }
        public long ChunkOffset { get; }

        public uint Size { get; }
        public long RecordNumber { get; }
        public DateTimeOffset Timestamp { get; }

        public string ConvertPayloadToXml()
        {
            var sb = new StringBuilder();

            return sb.ToString();
        }

        public override string ToString()
        {
            return
                $"Chunk: 0x{ChunkOffset:X8} Record position: 0x{RecordPosition:X4} Record #: {RecordNumber.ToString().PadRight(3)} Timestamp: {Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}";
        }
    }
}