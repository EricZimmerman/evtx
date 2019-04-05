using System;
using System.IO;
using NLog;

namespace evtx.Tags
{
    public class OptionalSubstitution : IBinXml
    {
        public OptionalSubstitution(long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("BuildTag");
            RecordPosition = recordPosition;
            Size = 4;

            SubstitutionId = dataStream.ReadInt16();
            ValueType = (TagBuilder.ValueType) dataStream.ReadByte();

            l.Trace(this);
        }

        public short SubstitutionId { get; }
        public ValueType ValueType { get; }

        public long RecordPosition { get; }
        public long Size { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.OptionalSubstitution;

        public override string ToString()
        {
            return $"Optional substitution. Id: {SubstitutionId} Value type: {ValueType}";
        }
    }
}