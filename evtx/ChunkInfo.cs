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

    public ChunkInfo(byte[] chunkBytes, long absoluteOffset, int chunkNumber) : this(chunkBytes, absoluteOffset, chunkNumber, false, false)
    {
    }

    /// <param name="prepareJson">Build each record's payload JSON now (chunks are parsed in parallel)</param>
    /// <param name="prepareXml">Write each record's XML now</param>
    internal ChunkInfo(byte[] chunkBytes, long absoluteOffset, int chunkNumber, bool prepareJson, bool prepareXml)
    {
        ChunkBytes = chunkBytes;
        AbsoluteOffset = absoluteOffset;
        ChunkNumber = chunkNumber;

        ErrorRecords = new Dictionary<long, string>();
        EventRecords = new List<EventRecord>();

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
            //an empty chunk
            return;
        }

        var parser = new BinXmlParser(chunkBytes);
        var renderer = new Renderer(chunkBytes, parser);

        var position = HeaderSize;
        while (position + RecordHeaderSize + 4 <= end)
        {
            if (BitConverter.ToUInt32(chunkBytes, position) != RecordSignature)
            {
                Log.Verbose("No record signature at 0x{Offset:X}. Stopping", absoluteOffset + position);
                break;
            }

            var size = BitConverter.ToUInt32(chunkBytes, position + 4);
            var recordNumber = BitConverter.ToInt64(chunkBytes, position + 8);

            if (size < RecordHeaderSize + 4 || position + (long)size > chunkBytes.Length)
            {
                Log.Verbose("Record at 0x{Offset:X} has an invalid size 0x{Size:X}. Stopping", absoluteOffset + position, size);
                AddError(recordNumber, $"Record at offset 0x{absoluteOffset + position:X} has an invalid size (0x{size:X})");
                break;
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
