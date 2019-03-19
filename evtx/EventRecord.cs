using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace evtx
{
   public class EventRecord
    {
        public enum BinaryTag
        {
            EndOfBXmlStream = 0x0,
            OpenStartElementTag = 0x1, //< <name >
            CloseStartElementTag = 0x2, 
            CloseEmptyElementTag = 0x3,//< name /> 
            EndElementTag = 0x4, //</ name > 
            Value = 0x5, //attribute = “value” <-- right side
            Attribute = 0x6, // left side --> attribute = “value”
            TemplateInstance = 0xc,
            NormalSubstitution = 0xd,
            OptionalSubstitution = 0xe,
            StartOfBXmlStream = 0xf
        }

        public string ConvertPayloadToXml()
        {
            var sb = new StringBuilder();

            var templateId = BitConverter.ToInt32(PayloadBytes, 0x6);
            var templateOffset = BitConverter.ToInt32(PayloadBytes, 0xA);

            var l = LogManager.GetLogger("asd");
            sb.AppendLine($"Template id: 0x{templateId:X} template offset 0x{templateOffset:X}");

          
            sb.AppendLine("asdas");


            return sb.ToString();
        }


        public EventRecord(byte[] recordBytes)
        {
            Size = BitConverter.ToUInt32(recordBytes, 4);
            RecordNumber = BitConverter.ToInt64(recordBytes, 8);
            Timestamp = DateTimeOffset.FromFileTime(BitConverter.ToInt64(recordBytes, 0x10)).ToUniversalTime();

            PayloadBytes = new byte[recordBytes.Length - 28]; //4 for signature, 4 for first size, 8 for record #, 8 for timestamp, 4 for last size
            Buffer.BlockCopy(recordBytes,0x18,PayloadBytes,0,PayloadBytes.Length);

         //   File.WriteAllBytes(@"C:\temp\PayloadBytes.bin",PayloadBytes);
        }


        public uint Size { get; }
        public long RecordNumber { get; }
        public DateTimeOffset Timestamp { get; }

        public byte[] PayloadBytes { get; }

        public override string ToString()
        {
            return $"RecordNumber: 0x{RecordNumber:X} ({RecordNumber}) Timestamp: {Timestamp}";
        }
    }
}
