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
        public enum BinaryTag
        {
            EndOfBXmlStream = 0x0,
            OpenStartElementTag = 0x1, //< <name >
            OpenStartElementTag2 = 0x41, //< <name >
            CloseStartElementTag = 0x2, 
            CloseEmptyElementTag = 0x3,//< name /> 
            EndElementTag = 0x4, //</ name > 
            Value = 0x5, //attribute = “value” <-- right side
            Value2 = 0x45, //attribute = “value” <-- right side
            Attribute = 0x6, // left side --> attribute = “value”
            Attribute2 = 0x46, // left side --> attribute = “value”


            CDataSection = 0x7,
            CDataSection2 = 0x47,

            TokenCharRef = 0x8,
            TokenCharRef2 = 0x48,

            TokenEntityRef = 0x9,
            TokenEntityRef2 = 0x49,

            TokenPITarget = 0xa,
            TokenPIData = 0xb,
            
            TemplateInstance = 0xc,
            NormalSubstitution = 0xd,
            OptionalSubstitution = 0xe,
            StartOfBXmlStream = 0xf
        }

        public enum ValueType
        {
            NullType = 0x0,
            StringType = 0x1,
            AnsiStringType = 0x2, 
            Int8Type = 0x3,
            UInt8Type = 0x4,
            Int16Type = 0x5, 
            UInt16Type = 0x6, 
            Int32Type = 0x7,
            UInt32Type = 0x8,
            Int64Type = 0x9,
            UInt64Type = 0xa,
            Real32Type = 0xb,
            Real64Type = 0xc,
            BoolType = 0xd, //32-bit integer that MUST be 0x00 or 0x01 (mapping to true or false)
            BinaryType = 0xe,
            GuidType = 0xf, //little endian
            SizeTType = 0x10,
            FileTimeType=0x11,
            SysTimeType = 0x12,
            SidType = 0x13,
            HexInt32Type = 0x14,
            HexInt64Type = 0x15,
            EvtHandle = 0x20,
            BinXmlType = 0x21,
            EvtXml = 0x23,
            ArrayUnicodeString = 0x81,
            ArrayAsciiString = 0x82,
            Array8BitIntSigned = 0x83,
            Array8BitIntUnsigned = 0x84,
            Array16BitIntSigned = 0x85,
            Array16BitIntUnsigned = 0x86,
            Array32BitIntSigned = 0x87,
            Array32BitIntUnsigned = 0x88,
            Array64BitIntSigned = 0x89,
            Array64BitIntUnsigned = 0x8a,
            ArrayFloat32Bit = 0x8b,
            ArrayFloat64Bit = 0x8c,
            ArrayBool = 0x8d,
            ArrayGuid = 0x8f, // or e?
            ArraySizeType = 0x90,
            ArrayFileTime = 0x91,
            ArraySystemTime = 0x92, //Every 16 bytes are an individual value in little-endian
            ArraySids = 0x93,
            Array32BitHex = 0x94,
            Array64BitHex = 0x95,


        }
        


        public string ConvertPayloadToXml()
        {
            var sb = new StringBuilder();

            return sb.ToString();
        }

        public static List<Template> Templates = new List<Template>();

        public List<IBinXml> Nodes { get; set; }

        private Template _template;

        public EventRecord(byte[] recordBytes, int recordPosition, long chunkOffset)
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

         l.Debug($"Chunk: 0x{ChunkOffset:X} Record position: 0x{RecordPosition:X} Record #: {RecordNumber}");

            var index = 0;
            var inStream = true;

            if (RecordNumber == 4)
            {
                Debug.WriteLine(1);
            }

            while (inStream && index<PayloadBytes.Length)
            {
                var op = (byte)PayloadBytes[index];

                op = (byte)(op & 0x0f);

                var opCode = (BinaryTag)op;

                l.Debug($"     Opcode: {opCode} while Index is 0x{index:X} Rec pos plus index: 0x{(recordPosition+index):X} abs off:  0x{(chunkOffset+ recordPosition+index + 24):X} chunkOffset : 0x{chunkOffset:X}");

                switch (opCode)
                {
                    //TODO THIS NEEDS FIXED
                    case BinaryTag.TokenEntityRef:
                    case BinaryTag.EndElementTag:
                    case BinaryTag.TokenPITarget:
                    case BinaryTag.CloseEmptyElementTag:
                    case BinaryTag.CDataSection:
                    case BinaryTag.TokenPIData:
                    case BinaryTag.OpenStartElementTag:
                        index = PayloadBytes.Length; 
                        break;



                    case BinaryTag.EndOfBXmlStream:
                        index += 1;
                        inStream = false;

                        break;
                    case BinaryTag.StartOfBXmlStream:
                        index += 1;
                        var majorVer = PayloadBytes[index];
                        index += 1;
                        var minorVer = PayloadBytes[index];
                        index += 1;
                        var flags = PayloadBytes[index];
                        index += 1;

                        l.Trace($"Major: {majorVer} Minor: {minorVer} Flags: {flags}");
                        break;
                    case BinaryTag.TemplateInstance:

                        var templateOffset = BitConverter.ToInt32(PayloadBytes, 10);

                        //length lives 30 bytes away from where we are
                        var templateSize = BitConverter.ToInt32(PayloadBytes, index + 30);

                        templateSize += 0x22; //0x21 is the beginning part of the template info. the template size is for the nodes that make up the template

                        if (Templates.SingleOrDefault(t1 => t1.TemplateOffset == templateOffset) == null)
                        {
                            var templateBuffer = new byte[templateSize]; 
                            Buffer.BlockCopy(PayloadBytes,index,templateBuffer,0,templateSize);
                            index += templateSize;

                            var t = new Template((int) chunkOffset,recordPosition, templateBuffer,templateOffset);
                            Templates.Add(t);

                            _template = t;

                            l.Debug($"     template Index is 0x{index:X}");
                            
                        }
                        else
                        {
                            l.Info($"Found template with offset 0x{templateOffset:X}");
                            index += 10;
                        }

                        //substitution array starts here
                        //first is 32 bit # with how many to expect
                        //followed by that # of pairs of 16 bit numbers, first is length, second is type

                        var substitutionArrayLen = BitConverter.ToInt32(PayloadBytes, index);
                        index += 4;

                        l.Info($"      Substitution len: 0x{substitutionArrayLen:X}");

                        var subList = new List<SubstitutionArrayEntry>();

                        var totalSubsize = 0;
                        for (var i = 0; i < substitutionArrayLen; i++)
                        {
                            //TODO FIX THIS
                            if (index >= PayloadBytes.Length-2)
                            {
                                index = PayloadBytes.Length;
                                break;
                            }
                            var subSize = BitConverter.ToInt16(PayloadBytes, index);
                            index += 2;
                            var subType = BitConverter.ToInt16(PayloadBytes, index);
                            index += 2;

                            totalSubsize += subSize;

                            l.Info($"    Position: {i} Size: 0x{subSize:X} Type: {(ValueType)subType}");

                            subList.Add(new SubstitutionArrayEntry(i, subSize,(ValueType)subType));
                        }

                        l.Debug($"     template Index post sub array is 0x{index:X} totalSubsize: 0x{totalSubsize:X}");

                        //TODO FIX THIS
                        if (index >= PayloadBytes.Length-2)
                        {
                            index = PayloadBytes.Length;
                            break;
                        }

                        //get the data into the substitution array entries
                        foreach (var substitutionArrayEntry in subList)
                        {
                            var data = new byte[substitutionArrayEntry.Size];

                            Buffer.BlockCopy(PayloadBytes,index,data,0,substitutionArrayEntry.Size);
                            index += substitutionArrayEntry.Size;
                            substitutionArrayEntry.DataBytes = data;

                            l.Info($"   Type: {substitutionArrayEntry.ValType} Data bytes: {BitConverter.ToString(data)}");
                        }


                        break;
//                    case BinaryTag.OpenStartElementTag2:
//
//                        break;
                    default:
                        throw new Exception($"Unknown opcode: {opCode} ({opCode:X}) index: 0x{index:X} chunkoffset: 0x{chunkOffset:X} abs offset: 0x{(chunkOffset+ recordPosition+index + 24):X}");
                }


            }

            l.Debug($"     Post WHILE Loop index: 0x{index:X} Payload Size: 0x{PayloadBytes.Length:X}");

          


        }

        public class SubstitutionArrayEntry
        {
            public SubstitutionArrayEntry(int position, int size, ValueType valType)
            {
                Position = position;
                Size = size;
                ValType = valType;
            }

            public int Position { get; }
            public int Size { get; }
            public ValueType ValType { get; }

            public byte[] DataBytes { get; set; }
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
