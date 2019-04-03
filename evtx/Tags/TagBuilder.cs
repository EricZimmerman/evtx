using System;
using System.IO;
using NLog;

namespace evtx.Tags
{
    public static class TagBuilder
    {
        public static string GetKeywordDescription(UInt64 keywordValue)
        {
            switch (keywordValue)
            {
                case 0x0000000000000000:
                    return "AnyKeyword";
                    
                case 0x0000000000010000:
                    return "Shell";
                    
                case 0x0000000000020000:

                    return "Properties";
                case 0x0000000000040000:

                    return "FileClassStoreAndIconCache";
                case 0x0000000000080000:

                    return "Controls";
                case 0x0000000000100000:

                    return "APICalls";
                case 0x0000000000200000:

                    return "InternetExplorer";
                case 0x0000000000400000:

                    return "ShutdownUX";
                case 0x0000000000800000:

                    return "CopyEngine";
                case 0x0000000001000000:

                    return "Tasks";
                case 0x0000000002000000:

                    return "WDI";
                case 0x0000000004000000:

                    return "StartupPerf";
                case 0x0000000008000000:

                    return "StructuredQuery";
                case 0x0001000000000000:

                    return "Reserved";
                case 0x0002000000000000:

                    return "WDIContext";
                case 0x0004000000000000:

                    return "WDIDiag";
                case 0x0008000000000000:

                    return "SQM";
                case 0x0010000000000000:

                    return "AuditFailure";
                case 0x0020000000000000:

                    return "AuditSuccess";
                case 0x0040000000000000:

                    return "CorrelationHint";
                case 0x0080000000000000:

                    return "EventlogClassic";
                case 0x0100000000000000:

                    return "ReservedKeyword56";
                case 0x0200000000000000:

                    return "ReservedKeyword57";
                case 0x0400000000000000:

                    return "ReservedKeyword58";
                case 0x0800000000000000:

                    return "ReservedKeyword59";
                case 0x1000000000000000:

                    return "ReservedKeyword60";
                case 0x2000000000000000:

                    return "ReservedKeyword61";
                case 0x4000000000000000:

                    return "ReservedKeyword62";
                case 0x8000000000000000:

                    return "Microsoft-Windows-Shell-Core_Diagnostic";
            }

            return "Unknown";
        }

        public enum BinaryTag
        {
            EndOfBXmlStream = 0x0,
            OpenStartElementTag = 0x1, //< <name >
          OpenStartElementTag2 = 0x41, //< <name >
            CloseStartElementTag = 0x2,
            CloseEmptyElementTag = 0x3, //< name /> 
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
            FileTimeType = 0x11,
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
            Array64BitHex = 0x95
        }


        
        //record position == absolute offset for where record was found
        //datastream are the bytes
        //chunk should be a reference to the chunk where this data is so it can get strings, templates, etc.

        public static IBinXml BuildTag(long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("BuildTag");
            //op code is pulled from stream, so account for that
            var opOrg = dataStream.ReadByte();

            var op = (byte) opOrg;// (opOrg & 0x0f);0.

            var opCode = (BinaryTag) op;

            if (opCode != BinaryTag.StartOfBXmlStream && opCode != BinaryTag.EndOfBXmlStream)
            {
                l.Debug($"     BuildTag: opOrg: {opOrg:X} opCode: {opCode} recordPosition: 0x{recordPosition:X} dataStream position: 0x{(recordPosition+dataStream.BaseStream.Position):X}");
            }
            

            switch (opCode)
            {
                case BinaryTag.TemplateInstance:
                    return new TemplateInstance( recordPosition, dataStream, chunk);

                case BinaryTag.StartOfBXmlStream:
                    return new StartOfBXmlStream( recordPosition, dataStream);

                case BinaryTag.EndOfBXmlStream:
                    return new EndOfBXmlStream( recordPosition);

                case BinaryTag.OpenStartElementTag:
                    return new OpenStartElementTag( recordPosition, dataStream, chunk);

                case BinaryTag.Attribute:
                    return new Attribute( recordPosition, dataStream, chunk);

                case BinaryTag.Value:
                    return new Value( recordPosition, dataStream, chunk);

                case BinaryTag.CloseStartElementTag:
                    return new CloseStartElementTag( recordPosition);
                case BinaryTag.CloseEmptyElementTag:
                    return new CloseStartElementTag( recordPosition);

                case BinaryTag.OptionalSubstitution:
                    return new OptionalSubstitution( recordPosition, dataStream, chunk);

                case BinaryTag.EndElementTag:
                    return new EndElementTag( recordPosition);

                default:
                    throw new Exception($"unknown tag to build for opCode: {opCode} at position 0x{dataStream.BaseStream.Position:X}");
            }
        }
    }
}