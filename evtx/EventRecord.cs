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

            BuildProperties();
        }

        public string PayloadData1 { get; private set; }
        public string PayloadData2 { get; private set; }
        public string PayloadData3 { get; private set; }
        public string PayloadData4 { get; private set; }
        public string PayloadData5 { get; private set; }
        public string PayloadData6 { get; private set; }
        public string UserName { get; private set; }
        public string RemoteHost { get; private set; }
        public string ExecutableInfo { get; private set; }
        public string MapDescription { get; private set; }


        public string Computer { get; private set; }

        public string Payload { get; set; }

        public string UserId { get; private set; }
        public string Channel { get; private set; }
        public string Provider { get; private set; }
        public int EventId { get; private set; }
        public string EventRecordId { get; private set; }
        public int ProcessId { get; private set; }
        public int ThreadId { get; private set; }
        public int Level { get; private set; }
        public string SourceFile { get; set; }

        /// <summary>
        ///     This should match the Timestamp pulled from the data, but this one is explicitly from the XML via the substitution
        ///     values
        /// </summary>

        public DateTimeOffset TimeCreated { get; private set; }

        [IgnoreDataMember] public List<IBinXml> Nodes { get; set; }

        [IgnoreDataMember] public int RecordPosition { get; }

        [IgnoreDataMember] public uint Size { get; }

        public long RecordNumber { get; }

        [IgnoreDataMember] public DateTimeOffset Timestamp { get; }

        public void BuildProperties()
        {
            var l = LogManager.GetLogger("EventRecord");
            var xml = ConvertPayloadToXml();
            
            var reader = XmlReader.Create(new StringReader(xml));
            reader.MoveToContent();
            // Parse the file and display each of the nodes.
            while (reader.Read())
            {
                if (reader.IsStartElement())
                {
                    switch (reader.Name)
                    {
                        case "Computer":
                            reader.Read();
                            Computer = reader.Value;
                            break;
                        case "Channel":
                            reader.Read();
                            Channel = reader.Value;
                            break;
                        case "EventRecordID":
                            EventRecordId = reader.ReadElementContentAsString();
                            break;
                        case "EventID":
                            EventId = reader.ReadElementContentAsInt();
                            break;
                        case "Level":
                            Level = reader.ReadElementContentAsInt();
                            break;
                        case "TimeCreated":
                            var st = reader.GetAttribute("SystemTime");
                            TimeCreated = DateTimeOffset.Parse(st, null, DateTimeStyles.AssumeUniversal).ToUniversalTime();
                            break;
                        case "Provider":
                            Provider = reader.GetAttribute("Name");
                            break;
                        case "Execution":
                            ProcessId = int.Parse(reader.GetAttribute("ProcessID"));
                            ThreadId = int.Parse(reader.GetAttribute("ThreadID"));
                            break;
                        case "Security":
                            UserId = reader.GetAttribute("UserID");
                            break;
                        case "EventData":
                        case "UserData":
                            Payload = reader.ReadOuterXml();
                            break;
                    }
                }
            }

       

            var docNav = new XPathDocument(new StringReader(Payload));
            var nav = docNav.CreateNavigator();

            if (EventLog.EventLogMaps.ContainsKey($"{EventId}-{Channel.ToString().ToUpperInvariant()}"))
            {
                l.Trace($"Found map for event id {EventId} with Channel '{Channel}'!");
                var map = EventLog.EventLogMaps[$"{EventId}-{Channel.ToString().ToUpperInvariant()}"];

                MapDescription = map.Description;

                foreach (var mapEntry in map.Maps)
                {
                    var valProps = new Dictionary<string, string>();

                    foreach (var me in mapEntry.Values)
                    {
                        //xpath out variables
                        var propVal = nav.SelectSingleNode(me.Value.Replace("/Event/","/")); //strip this off since its now missing from the xml we need to search
                        if (propVal != null)
                        {
                            var propValue = propVal.Value;

                            if (me.Refine.IsNullOrEmpty() == false)
                            {
                                var hits = new List<string>();

                                //regex time
                                MatchCollection allMatchResults = null;
                                try
                                {
                                    var regexObj = new Regex(me.Refine, RegexOptions.IgnoreCase);
                                    allMatchResults = regexObj.Matches(propValue);
                                    if (allMatchResults.Count > 0)
                                    {
                                        // Access individual matches using allMatchResults.Item[]

                                        foreach (Match allMatchResult in allMatchResults)
                                        {
                                            hits.Add(allMatchResult.Value);
                                        }

                                        propValue = string.Join(" | ", hits);
                                    }
                                }
                                catch (ArgumentException)
                                {
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
        }

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

            return Regex.Replace(xmld.Beautify(), " xmlns.+\"", "",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public override string ToString()
        {
            return
                $"Record position: 0x{RecordPosition:X4} Record #: {RecordNumber.ToString().PadRight(3)} Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fffffff} Event Id: {EventId}";
        }
    }
}