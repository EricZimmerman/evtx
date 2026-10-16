using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace evtx.Test;

/// <summary>The omerbenamram/evtx samples: record counts as that parser finds them, and values</summary>
[TestFixture]
[NonParallelizable] //EventLog.EventLogMaps is static
public class SampleTests
{
    [TearDown]
    public void TearDown()
    {
        EventLog.EventLogMaps.Clear();
    }

    [TestCase("security.evtx", 2261)]
    [TestCase("system.evtx", 1881)]
    [TestCase("sysmon.evtx", 358)]
    [TestCase("new-user-security.evtx", 4)]
    [TestCase("issue_201.evtx", 1)]
    [TestCase("Archive-ForwardedEvents-test.evtx", 653)]
    [TestCase("MSExchange_Management_wec.evtx", 1)]
    [TestCase("sample_with_a_bad_chunk_magic.evtx", 270)]
    [TestCase("security_bad_string_cache.evtx", 2261)]
    [TestCase("sample-with-zero-data-size-event.evtx", 335)]
    [TestCase("sample-with-irregular-bool-values.evtx", 3028)]
    [TestCase("Security_with_size_t.evtx", 636)]
    [TestCase("Application_no_crc32.evtx", 17)]
    [TestCase("2-system-Security-dirty.evtx", 14621)]
    public void RecordCounts(string name, int expected)
    {
        var log = new EventLog(new MemoryStream(TestFiles.RustSample(name)));
        var records = log.GetEventRecords().ToList();

        records.Should().HaveCount(expected);
        //sample_with_a_bad_chunk_magic has a record with EventID 0 (as the Rust parser shows it)
        records.Should().OnlyContain(t => t.EventId >= 0 && t.PayloadAsJson() != null && t.ConvertPayloadToXml().StartsWith("<Event>"));
    }

    [Test]
    public void ForwardedEvents()
    {
        //EvtxECmd issue #15: none of these records could be read (they have no templates and typed inline values)
        EventLog.LoadMaps(TestFiles.MapsDir);
        var log = new EventLog(new MemoryStream(TestFiles.RustSample("Archive-ForwardedEvents-test.evtx")));
        var records = log.GetEventRecords().ToList();

        log.ErrorRecords.Should().BeEmpty();
        var first = records[0];
        first.RecordNumber.Should().Be(127638931);
        first.EventRecordId.Should().Be("1447406953");
        first.EventId.Should().Be(4625);
        first.Provider.Should().Be("Microsoft-Windows-Security-Auditing");
        first.Channel.Should().Be("Security");
        first.Computer.Should().Be("slad1.saclink.csus.edu");
        first.Level.Should().Be("LogAlways");
        first.Keywords.Should().Be("Audit failure");
        first.ProcessId.Should().Be(512);
        first.ThreadId.Should().Be(12960);
        first.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss.fffffff").Should().Be("2019-11-08 23:20:54.6705004");
        first.MapDescription.Should().Be("Failed logon");
        first.RemoteHost.Should().Be("IRT-PA-IDM1 (10.115.247.238)");
        first.RenderingInfo.Should().StartWith("{\"Level\":\"Information\",\"Opcode\":\"Info\",\"Task\":\"Logon\",\"Channel\":\"Security\",\"Provider\":\"Microsoft Windows security auditing.\",\"Keywords\":\"Audit Failure\",\"Message\":\"An account failed to log on.");
        first.PayloadAsJson().Should().StartWith("{\"EventData\":{\"Data\":[{\"@Name\":\"SubjectUserSid\",\"#text\":\"S-1-0-0\"}");
        records.Should().OnlyContain(t => t.RenderingInfo != null);
    }

    [Test]
    public void SizeTValuesAreHex()
    {
        var log = new EventLog(new MemoryStream(TestFiles.RustSample("Security_with_size_t.evtx")));
        var xml = string.Concat(log.GetEventRecords().Select(t => t.ConvertPayloadToXml()));

        //size_t used to be the raw bytes (00-10-00-...)
        xml.Should().MatchRegex("<Data Name=\"[A-Za-z]+\">0x[0-9A-F]+</Data>");
        xml.Should().NotMatchRegex(">[0-9A-F]{2}(-[0-9A-F]{2}){7}<");
    }

    [Test]
    public void ChunkChecksums()
    {
        var bytes = TestFiles.RustSample("security.evtx");
        var chunk = new ChunkInfo(bytes.Skip(0x1000).Take(0x10000).ToArray(), 0x1000, 0);

        chunk.CalculatedCrc.Should().Be(chunk.Crc);
        chunk.CalculatedDataCrc.Should().Be(chunk.DataCrc);
        var header = new EventLog(new MemoryStream(bytes));
        header.CalculatedCrc.Should().Be(header.Crc);
    }
}
