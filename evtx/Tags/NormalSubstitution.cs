using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace evtx.Tags
{
    class NormalSubstitution:IBinXml
    {
        public short SubstitutionId { get; }
        public ValueType ValueType { get; }

        public NormalSubstitution( long recordPosition, BinaryReader dataStream, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("BuildTag");
            RecordPosition = recordPosition;
            Size = 4;

            SubstitutionId = dataStream.ReadInt16();
            ValueType = (TagBuilder.ValueType) dataStream.ReadByte();

            //  l.Debug($"sub id: 0x{subId} val type: {valType}");
        }

        public long RecordPosition { get; }
        public long Size { get; }
        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"Normal substitution. Id: {SubstitutionId} Value type: {ValueType}";
        }

        public TagBuilder.BinaryTag TagType { get; }
    }
}