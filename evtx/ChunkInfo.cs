using System;
using System.Collections.Generic;
using evtx.BinXml;
using Force.Crc32;
using Serilog;

namespace evtx;

/// <summary>
///     A 64 KB chunk: a 512 byte header (with a 64 entry string table and a 32 entry template table, which are only
///     hash tables of what the records define), then event records up to the free space offset. Each record is a
///     signature (0x00002A2A), its size, its number, a FILETIME, BinXml event data, and its size again.
/// </summary>
public class ChunkInfo
{
    public const int ChunkSize = 0x10000;
    public const int HeaderSize = 0x200;

    private const ulong ChunkSignature = 0x006B6E6843666C45; //ElfChnk\0
    private const uint RecordSignature = 0x00002A2A;
    private const int RecordHeaderSize = 0x18;

    public ChunkInfo(byte[] chunkBytes, long absoluteOffset, int chunkNumber) : this(chunkBytes, absoluteOffset, chunkNumber, false, false, false)
    {
    }

    /// <param name="prepareJson">Build each record's payload JSON now (chunks are parsed in parallel)</param>
    /// <param name="prepareXml">Write each record's XML now</param>
    /// <param name="recover">Also look for records in the slack after the last record and in chunks whose header was reset (cleared logs): <see cref="RecoveredRecords" /></param>
    internal ChunkInfo(byte[] chunkBytes, long absoluteOffset, int chunkNumber, bool prepareJson, bool prepareXml, bool recover)
    {
        ChunkBytes = chunkBytes;
        AbsoluteOffset = absoluteOffset;
        ChunkNumber = chunkNumber;

        ErrorRecords = new Dictionary<long, string>();
        EventRecords = new List<EventRecord>();
        RecoveredRecords = new List<EventRecord>();

        if (chunkBytes.Length < HeaderSize || BitConverter.ToUInt64(chunkBytes, 0) != ChunkSignature)
        {
            throw new EventLogParseException("Invalid chunk signature! Expected 'ElfChnk'", absoluteOffset, "ChunkHeader");
        }

        FirstEventRecordNumber = BitConverter.ToInt64(chunkBytes, 0x8);
        LastEventRecordNumber = BitConverter.ToInt64(chunkBytes, 0x10);
        FirstEventRecordIdentifier = BitConverter.ToInt64(chunkBytes, 0x18);
        LastEventRecordIdentifier = BitConverter.ToInt64(chunkBytes, 0x20);

        LastRecordOffset = BitConverter.ToUInt32(chunkBytes, 0x2C);
        FreeSpaceOffset = BitConverter.ToUInt32(chunkBytes, 0x30);
        DataCrc = BitConverter.ToInt32(chunkBytes, 0x34);
        Flags = BitConverter.ToUInt32(chunkBytes, 0x78);
        Crc = BitConverter.ToInt32(chunkBytes, 0x7C);

        //the header CRC covers 0x00-0x77 and 0x80-0x1FF
        var crc = Crc32Algorithm.Compute(chunkBytes, 0, 0x78);
        crc = Crc32Algorithm.Append(crc, chunkBytes, 0x80, HeaderSize - 0x80);
        CalculatedCrc = (int)crc;

        //records end at the free space offset; a bad value (dirty or damaged chunk) means the end of the chunk
        var end = FreeSpaceOffset >= HeaderSize && FreeSpaceOffset <= chunkBytes.Length ? (int)FreeSpaceOffset : chunkBytes.Length;
        CalculatedDataCrc = (int)Crc32Algorithm.Compute(chunkBytes, HeaderSize, end - HeaderSize);

        if (FirstEventRecordIdentifier == -1)
        {
            //an empty chunk; one that was in use before the log was cleared still has its old records
            if (recover)
            {
                Recover(HeaderSize, RecoveredFromClearedChunk, prepareJson, prepareXml);
            }

            return;
        }

        var parser = new BinXmlParser(chunkBytes);
        var renderer = new Renderer(chunkBytes, parser);

        var position = HeaderSize;
        while (position + RecordHeaderSize + 4 <= end)
        {
            if (BitConverter.ToUInt32(chunkBytes, position) != RecordSignature)
            {
                //damaged data (or the end of the records): go on at the next record that checks out, as Rust's evtx does
                var next = NextLiveRecord(position + 1, end);
                if (next < 0)
                {
                    Log.Verbose("No record signature at 0x{Offset:X}. Stopping", absoluteOffset + position);
                    break;
                }

                Log.Warning("No record signature at 0x{Offset:X}: damaged data up to the next record, at 0x{Next:X}", absoluteOffset + position, absoluteOffset + next);
                AddError(BitConverter.ToInt64(chunkBytes, next + 8) - 1, $"Damaged data at offset 0x{absoluteOffset + position:X} (no record signature), skipped up to the next record at 0x{absoluteOffset + next:X}");
                position = next;
                continue;
            }

            var size = BitConverter.ToUInt32(chunkBytes, position + 4);
            var recordNumber = BitConverter.ToInt64(chunkBytes, position + 8);

            if (size < RecordHeaderSize + 4 || position + (long)size > chunkBytes.Length)
            {
                AddError(recordNumber, $"Record at offset 0x{absoluteOffset + position:X} has an invalid size (0x{size:X})");
                var next = NextLiveRecord(position + 1, end);
                if (next < 0)
                {
                    Log.Verbose("Record at 0x{Offset:X} has an invalid size 0x{Size:X}. Stopping", absoluteOffset + position, size);
                    break;
                }

                Log.Warning("Record at 0x{Offset:X} has an invalid size 0x{Size:X}: going on at the next record, at 0x{Next:X}", absoluteOffset + position, size, absoluteOffset + next);
                position = next;
                continue;
            }

            try
            {
                var record = new EventRecord(this, parser, renderer, position, (int)size);
                if (prepareJson)
                {
                    record.PayloadAsJson();
                }

                if (prepareXml)
                {
                    record.ConvertPayloadToXml();
                }

                EventRecords.Add(record);

                if (recover)
                {
                    //for finding recovered copies of it (in parallel here, not one by one later)
                    record.ComputeContentKey();
                }

                if (record.HasHiddenRecord(chunkBytes))
                {
                    AddHiddenRecord(parser, renderer, record);
                }
            }
            catch (Exception e)
            {
                Log.Error("Record error at offset 0x{Offset:X}, record #: {RecordNumber} error: {Message}", AbsoluteOffset + position, recordNumber, e.Message);
                AddError(recordNumber, e.Message);
            }

            position += (int)size;

            if (recordNumber == LastEventRecordIdentifier)
            {
                //what follows is slack (as the chunk header says, and the reference parsers agree)
                break;
            }
        }

        TemplateCount = parser.TemplateCount;

        if (recover)
        {
            //after the last record (or where a damaged record stopped the walk): older records the chunk had before
            Recover(position, RecoveredFromSlack, prepareJson, prepareXml);
        }
    }

    /// <summary>
    ///     Where the walk goes on after damaged data (2026.11.1, as Rust's evtx): the next record signature from
    ///     <paramref name="from" /> whose size fits, whose size copy matches, that ends in the live area, that starts by the
    ///     header's last record offset and whose number is in the header's range (records of the chunk's earlier use, in
    ///     its slack, are outside that range); -1 when there is none
    /// </summary>
    private int NextLiveRecord(int from, int end)
    {
        var b = ChunkBytes;
        var searchEnd = LastRecordOffset >= HeaderSize && LastRecordOffset + 4 <= end ? (int)LastRecordOffset + 4 : end;
        var i = from;
        while (i + RecordHeaderSize + 4 <= end && i < searchEnd)
        {
            i = Array.IndexOf(b, (byte)0x2A, i, searchEnd - i);
            if (i < 0 || i + RecordHeaderSize + 4 > end)
            {
                return -1;
            }

            if (BitConverter.ToUInt32(b, i) == RecordSignature)
            {
                var size = BitConverter.ToUInt32(b, i + 4);
                var number = BitConverter.ToInt64(b, i + 8);
                if (size >= RecordHeaderSize + 4 && i + (long)size <= end && BitConverter.ToUInt32(b, i + (int)size - 4) == size &&
                    number >= FirstEventRecordIdentifier && number <= LastEventRecordIdentifier)
                {
                    return i;
                }
            }

            i += 1;
        }

        return -1;
    }

    /// <summary><see cref="EventRecord.Recovered" /> for a record found after the last record of a chunk</summary>
    public const string RecoveredFromSlack = "Slack";

    /// <summary><see cref="EventRecord.Recovered" /> for a record in a chunk whose header says it is empty (a cleared log)</summary>
    public const string RecoveredFromClearedChunk = "ClearedChunk";

    /// <summary>
    ///     <see cref="EventRecord.Recovered" /> for a record in the chunk's live area (before the free space offset, its
    ///     number in the header's range) after a record that could not be read, which stopped the walk
    /// </summary>
    public const string RecoveredAfterError = "AfterError";

    /// <summary>
    ///     Records found by --rr in the chunk's slack or in a cleared chunk (Issues #268). A candidate is a record
    ///     signature, a size that fits in the chunk and the same size in its last 4 bytes; it is kept when it parses
    ///     and renders, and its templates fit its values (<see cref="BinXmlParser.Strict" />). They have their own
    ///     string and template caches, so they never change how the live records render
    /// </summary>
    public List<EventRecord> RecoveredRecords { get; }

    /// <summary>Candidates (signature and size copy) that could not be read as records</summary>
    public int RecoveryFailures { get; private set; }

    private void Recover(int start, string from, bool prepareJson, bool prepareXml)
    {
        var b = ChunkBytes;
        var parser = new BinXmlParser(b) { Strict = true };
        var renderer = new Renderer(b, parser);

        var i = Math.Max(start, HeaderSize);
        while (i + RecordHeaderSize + 4 <= b.Length)
        {
            i = Array.IndexOf(b, (byte)0x2A, i);
            if (i < 0 || i + RecordHeaderSize + 4 > b.Length)
            {
                break;
            }

            if (BitConverter.ToUInt32(b, i) != RecordSignature)
            {
                i += 1;
                continue;
            }

            var size = BitConverter.ToUInt32(b, i + 4);
            if (size < RecordHeaderSize + 4 || i + (long)size > b.Length || BitConverter.ToUInt32(b, i + (int)size - 4) != size)
            {
                i += 1;
                continue;
            }

            try
            {
                var record = new EventRecord(this, parser, renderer, i, (int)size) { Recovered = from };
                if (from == RecoveredFromSlack && i < FreeSpaceOffset && i + size <= FreeSpaceOffset &&
                    record.RecordNumber >= FirstEventRecordIdentifier && record.RecordNumber <= LastEventRecordIdentifier)
                {
                    record.Recovered = RecoveredAfterError;
                }

                if (prepareJson)
                {
                    record.PayloadAsJson();
                }

                //the XML is what duplicates are found by
                record.ComputeContentKey();

                RecoveredRecords.Add(record);
                Log.Debug("Recovered record #: {RecordNumber} at 0x{Offset:X} ({From})", record.RecordNumber, AbsoluteOffset + i, from);
                i += (int)size;
            }
            catch (Exception e)
            {
                RecoveryFailures += 1;
                Log.Debug("Possible record at 0x{Offset:X} could not be recovered: {Message}", AbsoluteOffset + i, e.Message);
                i += 1;
            }
        }
    }

    public uint LastRecordOffset { get; }
    public uint FreeSpaceOffset { get; }

    /// <summary>0x1: dirty, 0x4: no CRC32 (the header and data CRCs are not kept up to date)</summary>
    public uint Flags { get; }

    /// <summary>The number of template definitions the records used</summary>
    public int TemplateCount { get; }

    public List<EventRecord> EventRecords { get; }
    public Dictionary<long, string> ErrorRecords { get; }

    /// <summary>CRC32 of the header, as stored and as calculated</summary>
    public int Crc { get; }

    public int CalculatedCrc { get; }

    /// <summary>CRC32 of the event records, as stored and as calculated</summary>
    public int DataCrc { get; }

    public int CalculatedDataCrc { get; }

    public long LastEventRecordIdentifier { get; }
    public long FirstEventRecordIdentifier { get; }
    public long LastEventRecordNumber { get; }
    public long FirstEventRecordNumber { get; }

    public byte[] ChunkBytes { get; set; }
    public long AbsoluteOffset { get; }
    public int ChunkNumber { get; }

    private void AddError(long recordNumber, string message)
    {
        if (ErrorRecords.ContainsKey(recordNumber) == false)
        {
            ErrorRecords.Add(recordNumber, message);
        }
    }

    /// <summary>A record hidden in the slack of another (e.g. DanderSpritz's eventlogedit)</summary>
    private void AddHiddenRecord(BinXmlParser parser, Renderer renderer, EventRecord host)
    {
        var position = host.RecordPosition + (int)host.ExtraDataOffset;
        var size = BitConverter.ToInt32(ChunkBytes, position + 4);
        try
        {
            var hidden = new EventRecord(this, parser, renderer, position, size) { HiddenRecord = true };
            Log.Warning("Record #: {RecordNumber} (timestamp: {TimeCreated:yyyy-MM-dd HH:mm:ss.fffffff}): Warning! A hidden record was found! Possible DanderSpritz use detected!", hidden.RecordNumber, hidden.TimeCreated);
            EventRecords.Add(hidden);
        }
        catch (Exception e)
        {
            Log.Debug("Possible hidden record at 0x{Offset:X} could not be read: {Message}", AbsoluteOffset + position, e.Message);
        }
    }

    public override string ToString()
    {
        return
            $"Chunk absolute offset 0x{AbsoluteOffset:X8} Chunk #: {ChunkNumber.ToString(),-5} FirstEventRecordNumber: {FirstEventRecordNumber.ToString(),-8} LastEventRecordNumber: {LastEventRecordNumber.ToString(),-8} FirstEventRecordIdentifier: {FirstEventRecordIdentifier.ToString(),-8} LastEventRecordIdentifier: {LastEventRecordIdentifier.ToString(),-8}";
    }
}
