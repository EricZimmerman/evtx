using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;
using FluentValidation.Results;
using Force.Crc32;
using Serilog;
using Serilog.Events;
using ServiceStack;
using ServiceStack.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

//TODO rename project to EventLog?
namespace evtx;

public class EventLog
{
    [Flags]
    public enum EventLogFlag
    {
        None = 0x0,
        IsDirty = 0x1,
        IsFull = 0x2
    }

    private const long EventSignature = 0x00656c6946666c45;
    private const long ChunkSignature = 0x006B6E6843666C45; //ElfChnk\0

    public static long LastSeenTicks;

    /// <summary>
    ///     The number of seconds to use when comparing timestamps. Only event records greater than this value from the
    ///     previous record will be reported as a timestamp anomaly.
    /// </summary>
    public static int TimeDiscrepancyThreshold = 1;

    private readonly Stream _stream;

    public Dictionary<long, int> EventIdMetrics;

    public EventLog(Stream fileStream)
    {
        _stream = fileStream;

        var headerBytes = new byte[HeaderBlockSize];
        var read = ReadFully(fileStream, headerBytes);

        if (read < 0x80 || BitConverter.ToInt64(headerBytes, 0) != EventSignature)
        {
            throw new EventLogParseException("Invalid signature! Expected 'ElfFile'", 0, "FileHeader");
        }

        FirstChunkNumber = BitConverter.ToInt64(headerBytes, 0x8);
        LastChunkNumber = BitConverter.ToInt64(headerBytes, 0x10);

        NextRecordId = BitConverter.ToInt64(headerBytes, 0x18);
        HeaderSize = BitConverter.ToInt32(headerBytes, 0x20);

        MinorVersion = BitConverter.ToInt16(headerBytes, 0x24);
        MajorVersion = BitConverter.ToInt16(headerBytes, 0x26);

        HeaderBlockLength = BitConverter.ToUInt16(headerBytes, 0x28);
        ChunkCount = BitConverter.ToUInt16(headerBytes, 0x2A);

        Flags = (EventLogFlag) BitConverter.ToInt32(headerBytes, 0x78);

        //the CRC covers the first 0x78 bytes
        Crc = BitConverter.ToInt32(headerBytes, 0x7C);
        CalculatedCrc = (int) Crc32Algorithm.Compute(headerBytes, 0, 0x78);

        ErrorRecords = new Dictionary<long, string>();
    }

    private const int HeaderBlockSize = 0x1000;

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = stream.Read(buffer, read, buffer.Length - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        return read;
    }

    /// <summary>0x80</summary>
    public int HeaderSize { get; }

    /// <summary>0x1000: the header block chunks start after</summary>
    public ushort HeaderBlockLength { get; }

    public DateTimeOffset? EarliestTimestamp { get; private set; }
    public DateTimeOffset? LatestTimestamp { get; private set; }


    public static Dictionary<string, EventLogMap> EventLogMaps { get; private set; } =
        new Dictionary<string, EventLogMap>();

    public int TotalEventLogs { get; private set; }

    public long NextRecordId { get; }

    public long FirstChunkNumber { get; }
    public long LastChunkNumber { get; }

    public ushort ChunkCount { get; }

    public int Crc { get; }
    public int CalculatedCrc { get; }

    public EventLogFlag Flags { get; }

    public short MinorVersion { get; }
    public short MajorVersion { get; }

    public Dictionary<long, string> ErrorRecords { get; }

    /// <summary>Loads every *.map in <paramref name="mapPath" /> into <see cref="EventLogMaps" /></summary>
    /// <returns>True when one or more maps had errors (they are logged and skipped)</returns>
    public static bool LoadMaps(string mapPath)
    {
        EventLogMaps = new Dictionary<string, EventLogMap>();

        var mapFiles =
            Directory.EnumerateFileSystemEntries(mapPath, "*.map").ToList();


        var deserializer = new DeserializerBuilder()
            .Build();

        var validator = new EventLogMapValidator();

        var errorMaps = new List<string>();

        //deserializing is most of the time a run of EvtxECmd on a small log takes, so the maps are read in parallel,
        //then checked and added in name order as before
        mapFiles = mapFiles.OrderBy(t => t).ToList();
        var parsed = new (EventLogMap Map, ExceptionDispatchInfo Error)[mapFiles.Count];
        Parallel.For(0, mapFiles.Count, i =>
        {
            try
            {
                parsed[i] = (deserializer.Deserialize<EventLogMap>(File.ReadAllText(mapFiles[i])), null);
            }
            catch (Exception e)
            {
                parsed[i] = (null, ExceptionDispatchInfo.Capture(e));
            }
        });

        for (var i = 0; i < mapFiles.Count; i++)
        {
            var mapFile = mapFiles[i];
            try
            {
                parsed[i].Error?.Throw();
                var eventMapFile = parsed[i].Map;

                if (Log.IsEnabled(LogEventLevel.Verbose))
                {
                    //Dump is slow, so only when it is logged
                    Log.Verbose("{Map}",eventMapFile.Dump());
                }

                var validate = validator.Validate(eventMapFile);

                if (DisplayValidationResults(validate, mapFile) && ValidXPaths(eventMapFile, mapFile))
                {
                    //the same key as records look maps up by (the check used to leave out the provider, so a second
                    //map for the same event threw instead of being skipped; issue #241)
                    var mapKey = $"{eventMapFile.EventId}-{eventMapFile.Channel.ToUpperInvariant()}-{eventMapFile.Provider.ToUpperInvariant()}";
                    if (EventLogMaps.ContainsKey(mapKey) == false)
                    {
                        Log.Debug("{Path} is valid. Adding to maps...",Path.GetFileName(mapFile));
                        EventLogMaps.Add(mapKey, eventMapFile);
                    }
                    else
                    {
                        Log.Warning(
                            "A map for event id {EventId} with Channel {Channel} and Provider {Provider} already exists. Map {Path} will be skipped",eventMapFile.EventId,eventMapFile.Channel,eventMapFile.Provider,Path.GetFileName(mapFile));
                    }
                }
                else
                {
                    errorMaps.Add(Path.GetFileName(mapFile));
                }
            }
            catch (SyntaxErrorException se)
            {
                errorMaps.Add(Path.GetFileName(mapFile));

                Console.WriteLine();
                Log.Warning("Syntax error in {MapFile}",mapFile);
                Log.Fatal(se,"{Message}",se.Message);

                var lines = File.ReadLines(mapFile).ToList();
                var fileContents = mapFile.ReadAllText();

                var badLine = lines[(int)(se.Start.Line - 1)];
                Console.WriteLine();
                Log.Fatal("Bad line (or close to it) {BadLine} has invalid data at column {Column}",badLine,se.Start.Column);

                if (fileContents.Contains('\t'))
                {
                    Console.WriteLine();
                    Log.Error("Bad line contains one or more tab characters. Replace them with spaces");
                    Console.WriteLine();
                    Log.Information("{Contents}",fileContents.Replace("\t", "<TAB>"));
                }
            }
            catch (YamlException ye)
            {
                errorMaps.Add(Path.GetFileName(mapFile));

                Console.WriteLine();
                Log.Warning("Syntax error in {File}",mapFile);
                Log.Information("Message: {Message}",ye.Message);
                Console.WriteLine();

                var fileContents = mapFile.ReadAllText();

                Log.Information("{Contents}",fileContents);

                if (ye.InnerException != null)
                {
                    Log.Fatal("{Message}",ye.InnerException.Message);
                }

                Console.WriteLine();
                Log.Fatal("Verify all properties against example files or manual and try again");
            }
            catch (Exception e)
            {
                Log.Error(e,"Error loading map file {File}: {Message}",mapFile,e.Message);
            }
        }

        if (errorMaps.Count > 0)
        {
            Console.WriteLine();
            Log.Error("The following maps had errors. Scroll up to review errors, correct them, and try again");
            foreach (var errorMap in errorMaps)
            {
                Log.Information("{Map}",errorMap);
            }

            Console.WriteLine();
        }

        return errorMaps.Count > 0;
    }

    /// <summary>Every map Value must compile as XPath (a path or an expression); a bad one used to fail each record</summary>
    private static bool ValidXPaths(EventLogMap map, string source)
    {
        var valid = true;
        foreach (var value in map.Maps?.Where(t => t?.Values != null).SelectMany(t => t.Values) ?? Enumerable.Empty<ValueEntry>())
        {
            if (value == null)
            {
                continue;
            }

            try
            {
                XPathExpression.Compile(value.Value);
            }
            catch (XPathException e)
            {
                Console.WriteLine();
                Log.Error("{Source}: Value {Name} is not a valid XPath ({Value}): {Message}", source, value.Name, value.Value, e.Message);
                valid = false;
            }
        }

        return valid;
    }

    private static bool DisplayValidationResults(ValidationResult result, string source)
    {
        if (Log.IsEnabled(LogEventLevel.Verbose))
        {
            Log.Verbose("Performing validation on {Source}: {Result}",source,result.Dump());
        }
        if (result.Errors.Count == 0)
        {
            return true;
        }

        Console.WriteLine();
        Log.Error("{Source} had validation errors",source);

        foreach (var validationFailure in result.Errors)
        {
            Log.Error("{Line}",validationFailure);
        }

        Console.WriteLine();
        Console.WriteLine();
        Log.Error("Correct the errors and try again. Exiting");
        Console.WriteLine();

        return false;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        //sb.AppendLine($"Version: {MajorVersion}.{MinorVersion}");
        sb.AppendLine($"Flags: {Flags}");
        sb.AppendLine($"Chunk count: {ChunkCount}");
        //sb.AppendLine($"First/last Chunk #: {FirstChunkNumber}/{LastChunkNumber}");
        sb.AppendLine($"Stored/Calculated CRC: {Crc:X}/{CalculatedCrc:X}");
        sb.AppendLine($"Earliest timestamp: {EarliestTimestamp?.ToUniversalTime():yyyy-MM-dd HH:mm:ss.fffffff}");
        sb.AppendLine($"Latest timestamp:   {LatestTimestamp?.ToUniversalTime():yyyy-MM-dd HH:mm:ss.fffffff}");
        //sb.AppendLine($"Calculated CRC: {CalculatedCrc:X}");
        sb.AppendLine($"Total event log records found: {TotalEventLogs:N0}");

        return sb.ToString();
    }

    /// <summary>
    ///     Every record in every chunk, in file order. Chunks are independent (each has its own strings and templates),
    ///     so they are parsed in parallel, a batch at a time
    /// </summary>
    public IEnumerable<EventRecord> GetEventRecords()
    {
        EventIdMetrics = new Dictionary<long, int>();

        var ticksForTimeDelta = 10000000L * TimeDiscrepancyThreshold; //10000000 == ticks in a second
        var batchSize = Math.Max(2, Environment.ProcessorCount * 2);
        var chunkNumber = 0;
        var chunkOffset = _stream.Position;
        var done = false;

        while (done == false)
        {
            var buffers = new List<(byte[] Bytes, long Offset, int Number)>(batchSize);
            while (buffers.Count < batchSize)
            {
                var chunkBuffer = new byte[ChunkInfo.ChunkSize];
                var bytesRead = ReadFully(_stream, chunkBuffer);
                if (bytesRead == 0)
                {
                    done = true;
                    break;
                }

                buffers.Add((chunkBuffer, chunkOffset, chunkNumber));
                chunkOffset += bytesRead;
                chunkNumber += 1;

                if (bytesRead < chunkBuffer.Length)
                {
                    //a truncated file: the rest of the last chunk is zeros
                    done = true;
                    break;
                }
            }

            var chunks = new ChunkInfo[buffers.Count];
            Parallel.For(0, buffers.Count, i =>
            {
                var (bytes, offset, number) = buffers[i];
                if (BitConverter.ToInt64(bytes, 0) != ChunkSignature)
                {
                    Log.Verbose("Skipping chunk at 0x{ChunkOffset:X} as it does not have correct signature", offset);
                    return;
                }

                Log.Debug("Processing chunk at offset 0x{ChunkOffset:X}", offset);
                try
                {
                    chunks[i] = new ChunkInfo(bytes, offset, number, PreparePayloadJson, PrepareXml);
                }
                catch (Exception e)
                {
                    Log.Error("Chunk at 0x{ChunkOffset:X} could not be read: {Message}", offset, e.Message);
                }
            });

            foreach (var ci in chunks)
            {
                if (ci == null)
                {
                    continue;
                }

                foreach (var er in ci.EventRecords)
                {
                    if (EarliestTimestamp == null || er.Timestamp.Ticks < EarliestTimestamp.Value.Ticks)
                    {
                        EarliestTimestamp = er.Timestamp;
                    }

                    if (LatestTimestamp == null || er.Timestamp.Ticks > LatestTimestamp.Value.Ticks)
                    {
                        LatestTimestamp = er.Timestamp;
                    }

                    EventIdMetrics.TryGetValue(er.EventId, out var count);
                    EventIdMetrics[er.EventId] = count + 1;

                    //ticksForTimeDelta == totalticks for discrepancy value
                    if (LastSeenTicks > 0 && LastSeenTicks - ticksForTimeDelta > er.TimeCreated.Ticks)
                    {
                        Log.Warning("Record #: {RecordNumber} (timestamp: {TimeCreated:yyyy-MM-dd HH:mm:ss.fffffff}): Warning! Time just went backwards! Last seen time before change: {Ts:yyyy-MM-dd HH:mm:ss.fffffff}", er.RecordNumber, er.TimeCreated, new DateTimeOffset(LastSeenTicks, TimeSpan.Zero).ToUniversalTime());
                    }

                    LastSeenTicks = er.TimeCreated.Ticks;

                    yield return er;
                }

                foreach (var chunkInfoErrorRecord in ci.ErrorRecords)
                {
                    ErrorRecords[chunkInfoErrorRecord.Key] = chunkInfoErrorRecord.Value;
                }

                TotalEventLogs += ci.EventRecords.Count;
                ChunksProcessed += 1;
            }
        }
    }

    /// <summary>
    ///     Build each record's <see cref="EventRecord.PayloadAsJson" /> while its chunk is parsed (in parallel) instead
    ///     of when it is asked for
    /// </summary>
    public bool PreparePayloadJson { get; set; }

    /// <summary>Write each record's <see cref="EventRecord.ConvertPayloadToXml" /> while its chunk is parsed</summary>
    public bool PrepareXml { get; set; }

    /// <summary>Chunks that had a chunk signature</summary>
    public int ChunksProcessed { get; private set; }
}