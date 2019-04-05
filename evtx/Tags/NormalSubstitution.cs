using System;
using System.IO;
using NLog;

namespace evtx.Tags
{
    public class NormalSubstitution : IBinXml
    {
        public NormalSubstitution(long recordPosition, BinaryReader dataStream)
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

        public TagBuilder.BinaryTag TagType => TagBuilder.BinaryTag.NormalSubstitution;

        public override string ToString()
        {
            return $"Normal substitution. Id: {SubstitutionId} Value type: {ValueType}";
        }
    }
}