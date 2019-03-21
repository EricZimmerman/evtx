using System;
using System.Collections.Generic;
using NLog;

namespace evtx.Tags
{
    public static class TagBuilder
    {
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

        public static IBinXml BuildTag(long chunkOffset, int recordPosition, byte[] payload, int index,List<Template> templates)
        {
            var l = LogManager.GetLogger("BuildTag");
            //index will start at the opcode itself, so account for that
            var op = payload[index];

            op = (byte) (op & 0x0f);

            var opCode = (BinaryTag) op;

            var subPayload = new byte[0];

            switch (opCode)
            {
                case BinaryTag.TemplateInstance:
                    return new TemplateInstance(chunkOffset, recordPosition, index, payload,templates);

                case BinaryTag.StartOfBXmlStream:

                    subPayload = new byte[4];
                    Buffer.BlockCopy(payload, index, subPayload, 0, subPayload.Length);

                    return new StartOfBXmlStream(chunkOffset, recordPosition, 4, subPayload);

                case BinaryTag.EndOfBXmlStream:

                    return new EndOfBXmlStream(chunkOffset, recordPosition);

                case BinaryTag.OpenStartElementTag:
                    

                    return new OpenStartElementTag(chunkOffset,recordPosition,0,payload);

                default:
                    throw new Exception($"unknown tag to build for opCode: {opCode}");
            }
        }
    }
}