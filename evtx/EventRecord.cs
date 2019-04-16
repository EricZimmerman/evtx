using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using evtx.Tags;
using NLog;

namespace evtx
{
    public class EventRecord
    {
        public EventRecord(BinaryReader recordData, int recordPosition, ChunkInfo chunk)
        {
            var l = LogManager.GetLogger("EventRecord");

            RecordPosition = recordPosition;

            recordData.ReadInt32(); //signature

            Size = recordData.ReadUInt32();
            RecordNumber = recordData.ReadInt64();
            Timestamp = DateTimeOffset.FromFileTime(recordData.ReadInt64()).ToUniversalTime();

            if (recordData.PeekChar() != 0xf)
            {
                throw new Exception("Payload does not start with 0x1f!");
            }

            l.Trace(
                $"Record position: 0x{RecordPosition:X4} Record #: {RecordNumber.ToString().PadRight(3)} Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fffffff}");

            Nodes = new List<IBinXml>();

            var eof = false;

            while (eof == false)
            {
                var nextTag = TagBuilder.BuildTag(recordPosition, recordData, chunk);
                Nodes.Add(nextTag);

                if (nextTag is EndOfBXmlStream)
                    //nothing left to do, so exit
                {
                    eof = true;
                }
            }
        }

        public List<IBinXml> Nodes { get; set; }

        public int RecordPosition { get; }

        public uint Size { get; }
        public long RecordNumber { get; }
        public DateTimeOffset Timestamp { get; }

        public string ConvertPayloadToXml()
        {
            var ti = Nodes.SingleOrDefault(t => t.TagType == TagBuilder.BinaryTag.TemplateInstance);

            if (ti == null)
            {
                return "Record does not contain a template instance!";
            }

            ti = (TemplateInstance) ti;

            var xmld = new XmlDocument();
            var rawXml = ti.AsXml(null, RecordPosition).Replace("&", "&amp;");

            xmld.LoadXml(rawXml);
            return xmld.Beautify();
        }

        public override string ToString()
        {
            return
                $"Record position: 0x{RecordPosition:X4} Record #: {RecordNumber.ToString().PadRight(3)} Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fffffff}";
        }
    }
}