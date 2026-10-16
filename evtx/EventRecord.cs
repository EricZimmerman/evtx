using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using evtx.BinXml;
using Serilog;
using ServiceStack;

namespace evtx;

public class EventRecord
{
    private const int RecordHeaderSize = 0x18;

    private static readonly ConcurrentDictionary<string, XPathExpression> XPaths = new();
    private static readonly ConcurrentDictionary<string, Regex> Refines = new();

    private string _xml;
    private string _payloadJson;
    private string _payloadXml;
    private bool _payloadSet;
    private OutElement _payload;
    private OutElement _root;

    internal EventRecord(ChunkInfo chunk, BinXmlParser parser, Renderer renderer, int position, int size)
    {
        var b = chunk.ChunkBytes;
        if (size < RecordHeaderSize + 4 || position + size > b.Length)
        {
            throw new EventLogParseException($"Record size 0x{size:X} does not fit in the chunk", position, "EventRecord");
        }

        RecordPosition = position;
        ChunkNumber = chunk.ChunkNumber;
        Size = (uint)size;
        RecordNumber = BitConverter.ToInt64(b, position + 8);

        var timestamp = ValueFormatter.FileTimeValue(BitConverter.ToInt64(b, position + 16));
        Timestamp = timestamp ?? throw new EventLogParseException("Record timestamp is not a valid FILETIME", position + 16, "EventRecord");

        Log.Verbose("Record position: 0x{RecordPosition:X4} Record #: {RecordNumber} Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fffffff}", RecordPosition, RecordNumber, Timestamp);

        var end = position + size - 4;
        var nodes = parser.ParseFragment(position + RecordHeaderSize, end, false, 0, out var stop);
        FindHiddenRecord(b, stop, end);

        var output = new List<object>();
        renderer.RenderRecord(nodes, output);

        var root = output.OfType<OutElement>().FirstOrDefault();
        if (root == null)
        {
            throw new EventLogParseException("Record does not contain an event", position, "EventRecord");
        }

        //the XML is written when it is asked for
        _root = root;
        BuildProperties(root);
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

    public int ChunkNumber { get; }

    public string Computer { get; private set; }

    /// <summary>The EventData or UserData element (or the element after System when there is neither), as XML</summary>
    public string Payload
    {
        get
        {
            if (_payloadSet == false)
            {
                //line breaks as LF, as the XmlReader that used to extract it gave them
                var xml = ConvertPayloadToXml();
                _payloadXml = _payload == null ? string.Empty : xml.Substring(_payload.XmlStart, _payload.XmlEnd - _payload.XmlStart).Replace("\r\n", "\n");
                _payloadSet = true;
            }

            return _payloadXml;
        }
        set
        {
            _payloadXml = value;
            _payloadSet = true;
        }
    }

    public string UserId { get; private set; }
    public string Channel { get; private set; }
    public string Provider { get; private set; }
    public int EventId { get; private set; }
    public string EventRecordId { get; private set; }
    public int ProcessId { get; private set; }
    public int ThreadId { get; private set; }
    public string Level { get; private set; }
    public string Keywords { get; private set; }
    public string SourceFile { get; set; }

    /// <summary>Where a hidden record starts in this record's slack (0 for none)</summary>
    public long ExtraDataOffset { get; set; }

    public bool HiddenRecord { get; set; }

    /// <summary>
    ///     This should match the Timestamp pulled from the data, but this one is explicitly from the XML via the substitution
    ///     values
    /// </summary>
    public DateTimeOffset TimeCreated { get; private set; }

    [IgnoreDataMember] public int RecordPosition { get; }

    [IgnoreDataMember] public uint Size { get; }

    public long RecordNumber { get; }

    [IgnoreDataMember] public DateTimeOffset Timestamp { get; }

    /// <summary>
    ///     The RenderingInfo element of forwarded events (Windows Event Forwarding) and other rendered events, as JSON:
    ///     the message and the level, task, opcode, channel, provider and keyword names from the source computer
    /// </summary>
    public string RenderingInfo { get; private set; }

    /// <summary>
    ///     <see cref="Payload" /> as JSON (empty when there is none), written straight from the event: the same text
    ///     Json.NET's SerializeXmlNode gives for the payload XML
    /// </summary>
    public string PayloadAsJson()
    {
        return _payloadJson ??= _payload == null ? string.Empty : JsonOutput.Write(_payload, false);
    }

    /// <summary>The whole event as JSON (what Json.NET's SerializeXmlNode gives for <see cref="ConvertPayloadToXml" />)</summary>
    public string EventAsJson()
    {
        return JsonOutput.Write(_root, true);
    }

    /// <summary>The event as XML</summary>
    public string ConvertPayloadToXml()
    {
        if (_xml == null)
        {
            var sb = new StringBuilder((int)Size * 3);
            new XmlTextOutput(sb).Write(_root);
            _xml = sb.ToString();
        }

        return _xml;
    }

    /// <summary>
    ///     A record signature ("**") in the slack right after the event data: data left from another record, or a
    ///     record hidden there (e.g. by DanderSpritz's eventlogedit, which grows a record over the one it deletes)
    /// </summary>
    private void FindHiddenRecord(byte[] b, int start, int end)
    {
        //the first 0x2A within 16 bytes of the end of the event data, followed by another
        var last = Math.Min(start + 16, end - 1);
        for (var i = start; i < last; i++)
        {
            if (b[i] != 0x2A)
            {
                continue;
            }

            if (b[i + 1] == 0x2A)
            {
                ExtraDataOffset = i - RecordPosition;
            }

            return;
        }
    }

    /// <summary>A whole record at <see cref="ExtraDataOffset" /> (signature, size, size copy) that is not this one</summary>
    internal bool HasHiddenRecord(byte[] b)
    {
        if (ExtraDataOffset <= 0)
        {
            return false;
        }

        var i = RecordPosition + (int)ExtraDataOffset;
        var end = RecordPosition + (int)Size;
        if (i + RecordHeaderSize + 4 > end || BitConverter.ToUInt32(b, i) != 0x00002A2A)
        {
            return false;
        }

        var size = BitConverter.ToInt32(b, i + 4);
        return size >= RecordHeaderSize + 4 && i + (long)size <= end && BitConverter.ToInt64(b, i + 8) != RecordNumber;
    }

    private void BuildProperties(OutElement root)
    {
        var system = root.Child("System");
        if (system != null)
        {
            foreach (var c in system.Children)
            {
                if (c is not OutElement e)
                {
                    continue;
                }

                switch (e.Name)
                {
                    case "Provider":
                        Provider = e.Attribute("Name");
                        break;
                    case "EventID":
                        EventId = int.TryParse(e.Text().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;
                        break;
                    case "Level":
                        Level = LevelName(e.Text().Trim());
                        break;
                    case "Keywords":
                        Keywords = KeywordsName(e.Text());
                        break;
                    case "TimeCreated":
                        var st = e.Attribute("SystemTime");
                        if (st != null && DateTimeOffset.TryParse(st, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var tc))
                        {
                            TimeCreated = tc.ToUniversalTime();
                        }

                        break;
                    case "EventRecordID":
                        EventRecordId = e.Text();
                        break;
                    case "Execution":
                        if (int.TryParse(e.Attribute("ProcessID"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                        {
                            ProcessId = pid;
                        }

                        if (int.TryParse(e.Attribute("ThreadID"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tid))
                        {
                            ThreadId = tid;
                        }

                        break;
                    case "Channel":
                        Channel = e.Text();
                        break;
                    case "Computer":
                        Computer = e.Text();
                        break;
                    case "Security":
                        UserId = e.Attribute("UserID");
                        break;
                }
            }
        }

        OutElement payload = null;
        var afterSystem = false;
        OutElement firstAfterSystem = null;
        foreach (var c in root.Children)
        {
            if (c is not OutElement e)
            {
                continue;
            }

            if (e.Name == "EventData" || e.Name == "UserData")
            {
                payload = e;
            }
            else if (e.Name == "RenderingInfo")
            {
                RenderingInfo = RenderingInfoJson(e);
            }

            if (afterSystem && firstAfterSystem == null)
            {
                firstAfterSystem = e;
            }

            if (e == system)
            {
                afterSystem = true;
            }
        }

        payload ??= firstAfterSystem;
        _payload = payload;

        ApplyMap();
    }

    private static string LevelName(string level)
    {
        switch (level)
        {
            case "0":
                return "LogAlways";
            case "1":
                return "Critical";
            case "2":
                return "Error";
            case "3":
                return "Warning";
            case "4":
                return "Info";
            case "5":
                return "Verbose";
            case "8":
                return "Success";
            case "16":
                return "Failure";
            default:
                return level;
        }
    }

    private static string KeywordsName(string kw)
    {
        switch (kw)
        {
            case "0x8010000000000000":
                return "Audit failure";
            case "0x8020000000000000":
                return "Audit success";
            case "0x8000000000000010":
                return "Time";
            case "0x8000000000000080":
                return "State";
            case "0x8000000000000040":
                return "Reboot";
            case "0x8000000000000018":
                return "Installation";
            case "0x8000000000000014":
                return "Download";
            case "0x8080000000000000":
                return "Audit success, classic";
            case "0x8000000000000000":
                return "Classic";
            default:
                return kw;
        }
    }

    /// <summary>RenderingInfo's names and message (Keywords can be a list of Keyword elements or text)</summary>
    private static string RenderingInfoJson(OutElement e)
    {
        var info = new RenderingInfoData();
        foreach (var c in e.Children)
        {
            if (c is not OutElement child)
            {
                continue;
            }

            switch (child.Name)
            {
                case "Message":
                    info.Message = child.Text();
                    break;
                case "Level":
                    info.Level = child.Text();
                    break;
                case "Task":
                    info.Task = child.Text();
                    break;
                case "Opcode":
                    info.Opcode = child.Text();
                    break;
                case "Channel":
                    info.Channel = child.Text();
                    break;
                case "Provider":
                    info.Provider = child.Text();
                    break;
                case "Keywords":
                    var keywords = child.Children.OfType<OutElement>().Where(t => t.Name == "Keyword").Select(t => t.Text()).ToList();
                    if (keywords.Count == 0 && child.Text().Length > 0)
                    {
                        keywords.Add(child.Text());
                    }

                    info.Keywords = string.Join(",", keywords);
                    break;
            }
        }

        return info.ToJson();
    }

    private void ApplyMap()
    {
        if (EventLog.EventLogMaps.Count == 0 || Channel == null || Provider == null)
        {
            return;
        }

        if (EventLog.EventLogMaps.TryGetValue($"{EventId}-{Channel.ToUpperInvariant()}-{Provider.ToUpperInvariant()}", out var map) == false)
        {
            return;
        }

        var nav = MapNavigator();

        Log.Verbose("Found map for Event ID {EventId} with Channel {Channel} and Provider {Provider}!", EventId, Channel, Provider);

        MapDescription = map.Description;

        Log.Debug("Processing map with description {Description}, event id: {EventId}", map.Description, map.EventId);

        foreach (var mapEntry in map.Maps)
        {
            var valProps = new Dictionary<string, string>();

            foreach (var me in mapEntry.Values)
            {
                //compiled once; a clone per use, since chunks are parsed in parallel
                var expression = XPaths.GetOrAdd(me.Value, XPathExpression.Compile).Clone();
                var propValue = MapValue(nav, expression);
                if (propValue != null)
                {

                    if (me.Refine.IsNullOrEmpty() == false)
                    {
                        var regexObj = Refine(me.Refine);
                        if (regexObj != null)
                        {
                            var allMatchResults = regexObj.Matches(propValue);
                            if (allMatchResults.Count > 0)
                            {
                                propValue = string.Join(" | ", allMatchResults.Cast<Match>().Select(t => t.Value));
                            }
                        }
                    }

                    var lu = map.Lookups?.SingleOrDefault(t => t.Name.ToUpperInvariant() == me.Name.ToUpperInvariant());

                    if (lu != null)
                    {
                        if (lu.Values.TryGetValue(propValue, out var looked))
                        {
                            propValue = looked; //set it to lookup value
                        }
                        else
                        {
                            propValue = $"{lu.Default} ({propValue})"; //include the default and original value
                        }
                    }

                    valProps[me.Name] = propValue;
                }
                else
                {
                    valProps[me.Name] = string.Empty;
                    Log.Warning("Record # {RecordNumber} (Event Record Id: {EventRecordId}): In map for event {EventId}, Property {Value} not found! Replacing with empty string", RecordNumber, EventRecordId, map.EventId, me.Value);
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
                    Log.Warning("Unknown property name {PropertyToUpdate}! Dropping mapping value of {PropertyValue}", propertyToUpdate, propertyValue);
                    break;
            }
        }
    }

    /// <summary>
    ///     A map value: the first node a path selects, or what an expression evaluates to (issue #99), e.g.
    ///     <c>round(/Event/EventData/Data[@Name="RemainingCapacity"] div /Event/EventData/Data[@Name="FullChargeCapacity"] * 100)</c>.
    ///     Null when nothing is selected, or a number can't be calculated (a value is missing or not a number)
    /// </summary>
    internal static string MapValue(XPathNavigator nav, XPathExpression expression)
    {
        switch (expression.ReturnType)
        {
            case XPathResultType.NodeSet:
                return nav.SelectSingleNode(expression)?.Value;
            case XPathResultType.Number:
                var d = (double)nav.Evaluate(expression);
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    return null;
                }

                if (d == 0)
                {
                    return "0"; //not "-0"
                }

                //as XPath's string() writes a number: no exponent, and no decimals for whole numbers
                return Math.Floor(d) == d && Math.Abs(d) < 1e15
                    ? d.ToString("0", CultureInfo.InvariantCulture)
                    : d.ToString("0.###############", CultureInfo.InvariantCulture);
            case XPathResultType.Boolean:
                return (bool)nav.Evaluate(expression) ? "true" : "false";
            default:
                var result = nav.Evaluate(expression);
                if (result is XPathNodeIterator nodes)
                {
                    return nodes.MoveNext() ? nodes.Current?.Value : null;
                }

                return Convert.ToString(result, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>For map XPaths: the rendered event, or (UseXmlDocumentForMaps) the event XML parsed again</summary>
    internal XPathNavigator MapNavigator()
    {
        return UseXmlDocumentForMaps
            ? new XPathDocument(new StringReader(ConvertPayloadToXml())).CreateNavigator()
            : new OutNavigator(_root);
    }

    /// <summary>Evaluate map XPaths over an XPathDocument of the event XML (the old way, for comparisons)</summary>
    internal static bool UseXmlDocumentForMaps { get; set; }

    /// <summary>A map's Refine regex, compiled once (null when it is not a valid pattern)</summary>
    private static Regex Refine(string pattern)
    {
        return Refines.GetOrAdd(pattern, p =>
        {
            try
            {
                return new Regex(p, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                // Syntax error in the regular expression
                return null;
            }
        });
    }

    public override string ToString()
    {
        return
            $"Record position: 0x{RecordPosition:X4} Record #: {RecordNumber.ToString().PadRight(3)} Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fffffff} Event ID: {EventId}";
    }
}

/// <summary>The names and message in a RenderingInfo element (serialized as <see cref="EventRecord.RenderingInfo" />)</summary>
internal class RenderingInfoData
{
    public string Level { get; set; }
    public string Opcode { get; set; }
    public string Task { get; set; }
    public string Channel { get; set; }
    public string Provider { get; set; }
    public string Keywords { get; set; }
    public string Message { get; set; }
}
