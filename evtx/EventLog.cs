using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentValidation.Results;
using Force.Crc32;
using Serilog;
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
    private const long ChunkSignature = 0x6B6E6843666C45;

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

        var headerBytes = new byte[4096];
        fileStream.Read(headerBytes, 0, 4096);

        if (BitConverter.ToInt64(headerBytes, 0) != EventSignature)
        {
            throw new Exception("Invalid signature! Expected 'ElfFile'");
        }

        FirstChunkNumber = BitConverter.ToInt64(headerBytes, 0x8);
        LastChunkNumber = BitConverter.ToInt64(headerBytes, 0x10);

        NextRecordId = BitConverter.ToInt64(headerBytes, 0x18);
        var unusedSize = BitConverter.ToInt32(headerBytes, 0x20);

        MinorVersion = BitConverter.ToInt16(headerBytes, 0x24);
        MajorVersion = BitConverter.ToInt16(headerBytes, 0x26);

        var unusedHeaderSize = BitConverter.ToInt16(headerBytes, 0x28);
        ChunkCount = BitConverter.ToUInt16(headerBytes, 0x2A);

        Flags = (EventLogFlag) BitConverter.ToInt32(headerBytes, 0x78);

        Crc = BitConverter.ToInt32(headerBytes, 0x7C);

        var inputArray = new byte[120 + 4];
        Buffer.BlockCopy(headerBytes, 0, inputArray, 0, 120);

        Crc32Algorithm.ComputeAndWriteToEnd(inputArray); // last 4 bytes contains CRC
        CalculatedCrc = BitConverter.ToInt32(inputArray, inputArray.Length - 4);

        ErrorRecords = new Dictionary<long, string>();
    }


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

    public static bool LoadMaps(string mapPath)
    {
        EventLogMaps = new Dictionary<string, EventLogMap>();

        var mapFiles =
            Directory.EnumerateFileSystemEntries(mapPath, "*.map").ToList();


        var deserializer = new DeserializerBuilder()
            .Build();

        var validator = new EventLogMapValidator();

        var errorMaps = new List<string>();

        foreach (var mapFile in mapFiles.OrderBy(t => t))
        {
            try
            {
                var eventMapFile = deserializer.Deserialize<EventLogMap>(File.ReadAllText(mapFile));

                Log.Verbose("{Map}",eventMapFile.Dump());

                var validate = validator.Validate(eventMapFile);

                if (DisplayValidationResults(validate, mapFile))
                {
                    if (EventLogMaps.ContainsKey(
                            $"{eventMapFile.EventId}-{eventMapFile.Channel.ToUpperInvariant()}") == false)
                    {
                        Log.Debug("{Path} is valid. Adding to maps...",Path.GetFileName(mapFile));

                        // if (eventMapFile.Provider.IsNullOrEmpty() == false)
                        // {
                        //     EventLogMaps.Add($"{eventMapFile.EventId}-{eventMapFile.Channel.ToUpperInvariant()}-{eventMapFile.Provider.ToUpperInvariant()}", eventMapFile);
                        // }
                        // else
                        // {
                        //     
                        // }
                        EventLogMaps.Add($"{eventMapFile.EventId}-{eventMapFile.Channel.ToUpperInvariant()}-{eventMapFile.Provider.ToUpperInvariant()}", eventMapFile);    
                            
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

    private static bool DisplayValidationResults(ValidationResult result, string source)
    {
        Log.Verbose("Performing validation on {Source}: {Result}",source,result.Dump());
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

    public IEnumerable<EventRecord> GetEventRecords()
    {
        //we are at offset 0x1000 and ready to start

        //chunk size == 65536, or 0x10000
        var chunkBuffer = new byte[0x10000];

        var chunkOffset = _stream.Position;
        var bytesRead = _stream.Read(chunkBuffer, 0, 0x10000);

        EventIdMetrics = new Dictionary<long, int>();

        var chunkNumber = 0;
        while (bytesRead > 0)
        {
            var chunkSig = BitConverter.ToInt64(chunkBuffer, 0);

            Log.Debug("Processing chunk at offset 0x{ChunkOffset:X}. Events found so far: {TotalEventLogs:N0}",chunkOffset,TotalEventLogs);

            if (chunkSig == ChunkSignature)
            {
                var ci = new ChunkInfo(chunkBuffer, chunkOffset, chunkNumber);

                foreach (var er in ci.EventRecords)
                {
                    if (EarliestTimestamp == null)
                    {
                        EarliestTimestamp = er.Timestamp;
                    }
                    else
                    {
                        if (er.Timestamp.Ticks < EarliestTimestamp.Value.Ticks)
                        {
                            EarliestTimestamp = er.Timestamp;
                        }
                    }

                    if (LatestTimestamp == null)
                    {
                        LatestTimestamp = er.Timestamp;
                    }
                    else
                    {
                        if (er.Timestamp.Ticks > LatestTimestamp.Value.Ticks)
                        {
                            LatestTimestamp = er.Timestamp;
                        }
                    }

                    if (EventIdMetrics.ContainsKey(er.EventId) == false)
                    {
                        EventIdMetrics.Add(er.EventId, 0);
                    }

                    EventIdMetrics[er.EventId] += 1;

                    yield return er;
                }

                foreach (var chunkInfoErrorRecord in ci.ErrorRecords)
                {
                    ErrorRecords.Add(chunkInfoErrorRecord.Key, chunkInfoErrorRecord.Value);
                }

                TotalEventLogs += ci.EventRecords.Count;
            }
            else
            {
                Log.Verbose("Skipping chunk at 0x{ChunkOffset:X} as it does not have correct signature",chunkOffset);
            }

            chunkOffset = _stream.Position;
            bytesRead = _stream.Read(chunkBuffer, 0, 0x10000);

            chunkNumber += 1;
        }



    }
}