using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using evtx.Tags;
using NLog;
using ServiceStack;

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


            using (var sr = new StringReader(xml))
            {
                var docNav = new XPathDocument(sr);
                var nav = docNav.CreateNavigator();

                var computer = nav.SelectSingleNode(@"/Event/System/Computer");
                var channel = nav.SelectSingleNode(@"/Event/System/Channel");
                var eventRecordId = nav.SelectSingleNode(@"/Event/System/EventRecordID");
                var eventId = nav.SelectSingleNode(@"/Event/System/EventID");
                var level = nav.SelectSingleNode(@"/Event/System/Level");
                var timeCreated = nav.SelectSingleNode(@"/Event/System/TimeCreated")?.GetAttribute("SystemTime", "");
                var provider = nav.SelectSingleNode(@"/Event/System/Provider")?.GetAttribute("Name", "");
                var processId = nav.SelectSingleNode(@"/Event/System/Execution")?.GetAttribute("ProcessID", "");
                var threadId = nav.SelectSingleNode(@"/Event/System/Execution")?.GetAttribute("ThreadID", "");
                var userId = nav.SelectSingleNode(@"/Event/System/Security")?.GetAttribute("UserID", "");

                TimeCreated = DateTimeOffset.Parse(timeCreated, null, DateTimeStyles.AssumeUniversal).ToUniversalTime();
                if (eventId != null)
                {
                    EventId = eventId.ValueAsInt;
                }

                if (level != null)
                {
                    Level = level.ValueAsInt;
                }

                if (eventRecordId != null)
                {
                    EventRecordId = eventRecordId.Value;
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


                if (EventLog.EventLogMaps.ContainsKey($"{EventId}-{channel.ToString().ToUpperInvariant()}"))
                {
                    l.Trace($"Found map for event id {EventId} with Channel '{channel}'!");
                    var map = EventLog.EventLogMaps[$"{EventId}-{channel.ToString().ToUpperInvariant()}"];

                    MapDescription = map.Description;

                    foreach (var mapEntry in map.Maps)
                    {
                        var valProps = new Dictionary<string, string>();

                        foreach (var me in mapEntry.Values)
                        {
                            //xpath out variables
                            var propVal = nav.SelectSingleNode(me.Value);
                            if (propVal != null)
                            {
                                var propValue = propVal.Value;

                                if (me.Refine.IsNullOrEmpty() == false)
                                {
                                    var hits = new List<string>();

                                    //regex time
                                    MatchCollection allMatchResults = null;
                                    try {
                                        Regex regexObj = new Regex(me.Refine, RegexOptions.IgnoreCase);
                                        allMatchResults = regexObj.Matches(propValue);
                                        if (allMatchResults.Count > 0) {
                                            // Access individual matches using allMatchResults.Item[]

                                            foreach (Match allMatchResult in allMatchResults)
                                            {
                                                hits.Add(allMatchResult.Value);
                                            }

                                            propValue = string.Join(" | ", hits);

                                        } 

                                    } catch (ArgumentException ) {
                                        // Syntax error in the regular expression
                                    }

                                }

                                valProps.Add(me.Name, propValue);
                            }
                            else
                            {
                                valProps.Add(me.Name, string.Empty);
                                l.Warn(
                                    $"Record # {RecordNumber} (Event Record Id: {EventRecordId}): In map for event '{map.EventId}', Property '{me.Value}' not found! Replacing with empty string");
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
                                l.Warn(
                                    $"Unknown property name '{propertyToUpdate}'! Dropping mapping value of '{propertyValue}'");
                                break;
                        }
                    }
                }

                //sanity checks
                UserId = userId ?? string.Empty;
                Provider = provider ?? string.Empty;
                Channel = channel?.Value ?? string.Empty;
                Computer = computer?.Value ?? string.Empty;
                Payload = payloadXml ?? string.Empty;
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
        public string MapDescription { get; }


        public string Computer { get; }

         public string Payload { get; set; }

        public string UserId { get; }
        public string Channel { get; }
        public string Provider { get; }
        public int EventId { get; }
        public string EventRecordId { get; }
        public int ProcessId { get; }
        public int ThreadId { get; }
        public int Level { get; }
        public string SourceFile { get; set; }

        /// <summary>
        ///     This should match the Timestamp pulled from the data, but this one is explicitly from the XML via the substitution
        ///     values
        /// </summary>

        public DateTimeOffset TimeCreated { get; }

        [IgnoreDataMember] public List<IBinXml> Nodes { get; set; }

        [IgnoreDataMember] public int RecordPosition { get; }

        [IgnoreDataMember] public uint Size { get; }

        public long RecordNumber { get; }

        [IgnoreDataMember] public DateTimeOffset Timestamp { get; }

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

            return  Regex.Replace(xmld.Beautify(), " xmlns.+\"", "",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public override string ToString()
        {
            return
                $"Record position: 0x{RecordPosition:X4} Record #: {RecordNumber.ToString().PadRight(3)} Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fffffff} Event Id: {EventId}";
        }
    }
}