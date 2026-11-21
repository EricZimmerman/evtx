using CsvHelper;
using evtx;

namespace EvtxECmd;

/// <summary>
///     Writes <see cref="EventRecord" /> (a library type, which has no CsvHelper attributes) as the CSV columns EvtxECmd has
///     always written, in their order: <c>TimeCreated</c> in the --dt format, and <c>Recovered</c> last, only with --rr
/// </summary>
internal sealed class EventRecordCsvWriter : CsvRecordWriter<EventRecord>
{
    private static readonly string[] Columns =
    {
        "RecordNumber", "EventRecordId", "TimeCreated", "EventId", "Level", "Provider", "Channel", "ProcessId", "ThreadId",
        "Computer", "ChunkNumber", "UserId", "MapDescription", "UserName", "RemoteHost", "PayloadData1", "PayloadData2",
        "PayloadData3", "PayloadData4", "PayloadData5", "PayloadData6", "ExecutableInfo", "HiddenRecord", "SourceFile",
        "Keywords", "ExtraDataOffset", "Payload", "RenderingInfo"
    };

    private readonly string _dateTimeFormat;
    private readonly bool _recovered;

    /// <param name="dateTimeFormat">The --dt format of TimeCreated</param>
    /// <param name="recovered">--rr: add the Recovered column</param>
    public EventRecordCsvWriter(string dateTimeFormat, bool recovered)
    {
        _dateTimeFormat = dateTimeFormat;
        _recovered = recovered;
    }

    public override int FieldCount => Columns.Length + (_recovered ? 1 : 0);

    public override void WriteHeader(CsvWriter writer)
    {
        foreach (var column in Columns)
        {
            WriteColumnName(writer, column);
        }

        if (_recovered)
        {
            WriteColumnName(writer, "Recovered");
        }
    }

    public override void Write(CsvWriter writer, EventRecord record)
    {
        writer.WriteField(record.RecordNumber);
        writer.WriteField(record.EventRecordId);
        //the current culture, as CsvHelper 33's Convert did
        writer.WriteField(record.TimeCreated.ToString(_dateTimeFormat));
        writer.WriteField(record.EventId);
        writer.WriteField(record.Level);
        writer.WriteField(record.Provider);
        writer.WriteField(record.Channel);
        writer.WriteField(record.ProcessId);
        writer.WriteField(record.ThreadId);
        writer.WriteField(record.Computer);
        writer.WriteField(record.ChunkNumber);
        writer.WriteField(record.UserId);
        writer.WriteField(record.MapDescription);
        writer.WriteField(record.UserName);
        writer.WriteField(record.RemoteHost);
        writer.WriteField(record.PayloadData1);
        writer.WriteField(record.PayloadData2);
        writer.WriteField(record.PayloadData3);
        writer.WriteField(record.PayloadData4);
        writer.WriteField(record.PayloadData5);
        writer.WriteField(record.PayloadData6);
        writer.WriteField(record.ExecutableInfo);
        writer.WriteField(record.HiddenRecord);
        writer.WriteField(record.SourceFile);
        writer.WriteField(record.Keywords);
        writer.WriteField(record.ExtraDataOffset);
        writer.WriteField(record.Payload);
        writer.WriteField(record.RenderingInfo);

        if (_recovered)
        {
            writer.WriteField(record.Recovered);
        }
    }
}
