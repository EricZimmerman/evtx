using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
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

            var xml = ConvertPayloadToXml();

            xml = xml.Replace(" xmlns=\"http://schemas.microsoft.com/win/2004/08/events/event\"", "");

            using (var sr = new StringReader(xml))
            {
                var  docNav = new XPathDocument(sr);
                var nav = docNav.CreateNavigator();

                var computer = nav.SelectSingleNode(@"/Event/System/Computer");
                var channel = nav.SelectSingleNode(@"/Event/System/Channel");
                var eventId = nav.SelectSingleNode(@"/Event/System/EventID");
                var timeCreated = nav.SelectSingleNode(@"/Event/System/TimeCreated")?.GetAttribute("SystemTime","");
                var userId = nav.SelectSingleNode(@"/Event/System/Security")?.GetAttribute("UserID","");

                TimeCreated = DateTimeOffset.Parse(timeCreated,null,System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime();
                if (eventId != null)
                {
                    EventId = eventId.ValueAsInt;
                }

                var payloadNode = nav.SelectSingleNode(@"/Event/EventData");
                if (payloadNode == null)
                {
                    payloadNode = nav.SelectSingleNode(@"/Event/UserData");
                }
                var payloadXml = payloadNode?.OuterXml;

               ////child[@id='#grand']/@age
               //<Data Name="MajorVersion">10</Data>
              // var foo = payloadNode.SelectSingleNode(@"Data[@Name=""MajorVersion""]");
               var foo = nav.SelectSingleNode(@"/Event/EventData/Data[@Name=""MajorVersion""]");
               var foo2 = nav.SelectSingleNode(@"/Event/EventData/Data[@Name=""BuildVersion""]");

               if (foo != null)
               {
                   PayloadData1 = foo.Value;
               }

               if (foo2 != null)
               {
                   PayloadData2 = foo2.Value;
               }

                //sanity checks
                UserId = userId ?? string.Empty;
                Channel = channel?.Value ?? string.Empty;
                Computer = computer?.Value ?? string.Empty;
                PayloadXml = payloadXml ?? string.Empty;
            }
        }

        public string PayloadData1 { get; }
        public string PayloadData2 { get; }
        public string PayloadData3 { get; }
        public string PayloadData4 { get; }
        public string PayloadData5 { get; }
        public string PayloadData6 { get; }
        public string UserName { get; }
        public string RemoteHost { get; }
        public string Computer { get; }
        public string PayloadXml { get; }
        public string UserId { get; }
        public string Channel { get; }
        public int EventId { get; }
        /// <summary>
        /// This should match the Timestamp pulled from the data, but this one is explicitly from the XML via the substitution values
        /// </summary>
        public DateTimeOffset TimeCreated { get; }

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