using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using evtx.Tags;
using NLog;
using ServiceStack.Text;

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
                var docNav = new XPathDocument(sr);
                var nav = docNav.CreateNavigator();

                var computer = nav.SelectSingleNode(@"/Event/System/Computer");
                var channel = nav.SelectSingleNode(@"/Event/System/Channel");
                var eventId = nav.SelectSingleNode(@"/Event/System/EventID");
                var level = nav.SelectSingleNode(@"/Event/System/Level");
                var timeCreated = nav.SelectSingleNode(@"/Event/System/TimeCreated")?.GetAttribute("SystemTime", "");
                var provider = nav.SelectSingleNode(@"/Event/System/Provider")?.GetAttribute("Name", "");
                var processId = nav.SelectSingleNode(@"/Event/System/Execution")?.GetAttribute("ProcessID", "");
                var threadId = nav.SelectSingleNode(@"/Event/System/Execution")?.GetAttribute("ThreadID", "");
                var userId = nav.SelectSingleNode(@"/Event/System/Security")?.GetAttribute("UserID", "");
                var eventGuid = nav.SelectSingleNode(@"/Event/System/Provider")?.GetAttribute("Guid", "");

                TimeCreated = DateTimeOffset.Parse(timeCreated, null, DateTimeStyles.AssumeUniversal).ToUniversalTime();
                if (eventId != null)
                {
                    EventId = eventId.ValueAsInt;
                }

                if (EventId == 4672)
                {
                    Debug.WriteLine(1);
                }

                if (level != null)
                {
                    Level = level.ValueAsInt;
                }

                if (processId != null)
                {
                    ProcessId = int.Parse(processId);
                }

                if (threadId != null)
                {
                    ThreadId = int.Parse(threadId);
                }

                var payloadNode = nav.SelectSingleNode(@"/Event/EventData");
                if (payloadNode == null)
                {
                    payloadNode = nav.SelectSingleNode(@"/Event/UserData");
                }

                var payloadXml = payloadNode?.OuterXml;

                
                if (EventLog.EventLogMaps.ContainsKey($"{EventId}-{eventGuid}"))
                {
                    l.Trace($"Found map for event id {EventId} with Guid '{eventGuid}'!");
                    var map = EventLog.EventLogMaps[$"{EventId}-{eventGuid}"];

                    foreach (var mapEntry in map.Maps)
                    {
                        var valProps = new Dictionary<string,string>();

                        foreach (var me in mapEntry.Values)
                        {
                            //xpath out variables
                            var propVal = nav.SelectSingleNode(me.Value);
                            if (propVal != null)
                            {
                                valProps.Add(me.Name,propVal.Value);
                            }
                            else
                            {
                                l.Warn($"In map for event '{map.EventId}', Property '{me.Value}' not found! It will not be substituted.");
                            }

                        }

                        //we have the values, now substitute
                        var propertyValue = mapEntry.PropertyValue;
                        foreach (var valProp in valProps)
                        {
                            propertyValue = propertyValue.Replace($"%{valProp.Key}%", valProp.Value);
                        }

                        var propertyToUpdate = mapEntry.Property.ToUpperInvariant();

                        if (valProps.Count == 0)
                        {
                            propertyToUpdate = "NOMATCH"; //prevents variables from showing up in the CSV
                        }

                        //we should now have our new value, so stick it in its place
                     switch (propertyToUpdate)
                        {
                            case "USERNAME":
                                UserName = propertyValue;
                                break;
                            case "REMOTEHOST":
                                RemoteHost = propertyValue;
                                break;
                            case "EXECUTABLEINFO":
                                ExecutableInfo = propertyValue;
                                break;
                            case "PAYLOADDATA1":
                                PayloadData1 = propertyValue;
                                break;
                            case "PAYLOADDATA2":
                                PayloadData2 = propertyValue;
                                break;
                            case "PAYLOADDATA3":
                                PayloadData3 = propertyValue;
                                break;
                            case "PAYLOADDATA4":
                                PayloadData4 = propertyValue;
                                break;
                            case "PAYLOADDATA5":
                                PayloadData5 = propertyValue;
                                break;
                            case "PAYLOADDATA6":
                                PayloadData6 = propertyValue;
                                break;
                            case "NOMATCH":
                                //when a property was not found.
                                break;
                            default:
                                l.Warn($"Unknown property name '{propertyToUpdate}'! Dropping mapping value of '{propertyValue}'");
                                break;
                        }

                    }
                    
                }

                //sanity checks
                UserId = userId ?? string.Empty;
                Provider = provider ?? string.Empty;
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
        public string ExecutableInfo { get; }



        public string Computer { get; }
        public string PayloadXml { get; }
        public string UserId { get; }
        public string Channel { get; }
        public string Provider { get; }
        public int EventId { get; }
        public int ProcessId { get; }
        public int ThreadId { get; }
        public int Level { get; }
        public string SourceFile { get; set; }

        /// <summary>
        ///     This should match the Timestamp pulled from the data, but this one is explicitly from the XML via the substitution
        ///     values
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

            return xmld.Beautify().Replace(" xmlns=\"http://schemas.microsoft.com/win/2004/08/events/event\"", "");
        }

        public override string ToString()
        {
            return
                $"Record position: 0x{RecordPosition:X4} Record #: {RecordNumber.ToString().PadRight(3)} Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fffffff}";
        }
    }
}