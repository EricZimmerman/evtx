using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace evtx.Test;

/// <summary>Records built token by token (EvtxBuilder), for BinXml (MS-EVEN6 2.2.12) features real samples rarely have</summary>
[TestFixture]
public class BinXmlTests
{
    private static readonly DateTime When = new(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);

    private static List<EventRecord> Parse(byte[] file, out EventLog log)
    {
        log = new EventLog(new MemoryStream(file));
        return log.GetEventRecords().ToList();
    }

    private static List<EventRecord> Parse(byte[] file) => Parse(file, out _);

    /// <summary>
    ///     Event/System (provider 0, event id 1, level 2, time 3, record id 4, channel 5, computer 6, user id 7
    ///     (optional)), then EventData with the given Data elements
    /// </summary>
    private static Action<ChunkBuilder> EventTemplate(params Action<ChunkBuilder>[] data)
    {
        return d => d.Element("Event", true, e =>
        {
            e.Element("System", true, s =>
            {
                s.Element("Provider", true, null, -1, ("Name", v => v.Substitution(0, 0x01)));
                s.Element("EventID", true, c => c.Substitution(1, 0x06));
                s.Element("Level", true, c => c.Substitution(2, 0x04));
                s.Element("TimeCreated", true, null, -1, ("SystemTime", v => v.Substitution(3, 0x11)));
                s.Element("EventRecordID", true, c => c.Substitution(4, 0x0A));
                s.Element("Channel", true, c => c.Substitution(5, 0x01));
                s.Element("Computer", true, c => c.Substitution(6, 0x01));
                s.Element("Security", true, null, -1, ("UserID", v => v.Substitution(7, 0x13, true)));
            });
            e.Element("EventData", true, ed =>
            {
                foreach (var a in data)
                {
                    a(ed);
                }
            });
        }, -1, ("xmlns", v => v.Text("http://schemas.microsoft.com/win/2004/08/events/event")));
    }

    private static Action<ChunkBuilder> Data(string name, int index, byte type, bool optional = false, int dependency = -1)
    {
        return ed => ed.Element("Data", true, c => c.Substitution(index, type, optional), dependency, ("Name", v => v.Text(name)));
    }

    private static Val[] SystemValues(ushort eventId, ulong recordId, params Val[] data)
    {
        return new[]
        {
            V.Str("Test-Provider"), V.U16(eventId), V.U8(4), V.FileTime(When), V.U64(recordId), V.Str("Application"),
            V.Str("HOST1"), V.Null()
        }.Concat(data).ToArray();
    }

    [Test]
    public void TemplateInstanceWithSubstitutions()
    {
        var b = new EvtxBuilder();
        b.Chunk().Record(1, When, r => r.FragmentHeader().TemplateInstance(
            EventTemplate(Data("User", 8, 0x01), Data("Count", 9, 0x07)), 0,
            SystemValues(4624, 1, V.Str("bob"), V.I32(-3))));

        var records = Parse(b.Build(), out var log);

        records.Should().ContainSingle();
        var e = records[0];
        e.EventId.Should().Be(4624);
        e.Provider.Should().Be("Test-Provider");
        e.Channel.Should().Be("Application");
        e.Computer.Should().Be("HOST1");
        e.Level.Should().Be("Info");
        e.EventRecordId.Should().Be("1");
        e.TimeCreated.Should().Be(new DateTimeOffset(When));
        e.UserId.Should().BeNull("an optional NULL attribute is not rendered");
        e.PayloadAsJson().Should().Be("{\"EventData\":{\"Data\":[{\"@Name\":\"User\",\"#text\":\"bob\"},{\"@Name\":\"Count\",\"#text\":\"-3\"}]}}");
        e.ConvertPayloadToXml().Should().Be(
            "<Event>\r\n  <System>\r\n    <Provider Name=\"Test-Provider\" />\r\n    <EventID>4624</EventID>\r\n    <Level>4</Level>\r\n" +
            "    <TimeCreated SystemTime=\"2024-05-06 07:08:09.0000000\" />\r\n    <EventRecordID>1</EventRecordID>\r\n" +
            "    <Channel>Application</Channel>\r\n    <Computer>HOST1</Computer>\r\n    <Security />\r\n  </System>\r\n" +
            "  <EventData>\r\n    <Data Name=\"User\">bob</Data>\r\n    <Data Name=\"Count\">-3</Data>\r\n  </EventData>\r\n</Event>");
        log.ErrorRecords.Should().BeEmpty();
    }

    [Test]
    public void LaterRecordsUseTheTemplateByOffset()
    {
        var b = new EvtxBuilder();
        var chunk = b.Chunk();
        var offset = 0;
        chunk.Record(1, When, r => offset = r.FragmentHeader().TemplateInstance(EventTemplate(Data("User", 8, 0x01)), 0, SystemValues(1, 1, V.Str("first"))));
        chunk.Record(2, When, r => r.FragmentHeader().TemplateInstance(null, offset, SystemValues(2, 2, V.Str("second"))));

        var records = Parse(b.Build());

        records.Select(t => t.EventId).Should().Equal(1, 2);
        records.Select(t => t.PayloadAsJson()).Should().Equal(
            "{\"EventData\":{\"Data\":{\"@Name\":\"User\",\"#text\":\"first\"}}}",
            "{\"EventData\":{\"Data\":{\"@Name\":\"User\",\"#text\":\"second\"}}}");
    }

    [Test]
    public void ValueTypes()
    {
        var sid = new byte[] { 1, 2, 0, 0, 0, 0, 0, 5, 0x20, 0, 0, 0, 0x20, 2, 0, 0 };
        var guid = new Guid("01234567-89ab-cdef-0123-456789abcdef");
        var values = new List<(string Name, Val Value, string Expected)>
        {
            ("Int8", V.Of(0x03, new byte[] { 0xFB }), "-5"),
            ("UInt64", V.U64(ulong.MaxValue), "18446744073709551615"),
            ("Real64", V.Of(0x0C, BitConverter.GetBytes(1.5)), "1.5"),
            ("Bool", V.Of(0x0D, BitConverter.GetBytes(1)), "True"),
            ("Binary", V.Of(0x0E, new byte[] { 0xDE, 0xAD }), "DE-AD"),
            ("Guid", V.Of(0x0F, guid.ToByteArray()), "01234567-89ab-cdef-0123-456789abcdef"),
            ("SizeT", V.Of(0x10, BitConverter.GetBytes(0x1000UL)), "0x1000"),
            ("FileTime", V.FileTime(When), "2024-05-06 07:08:09.0000000"),
            ("BadFileTime", V.Of(0x11, BitConverter.GetBytes(-1L)), "0xFFFFFFFFFFFFFFFF"),
            ("SysTime", V.Of(0x12, SystemTime(2023, 2, 3, 4, 5, 6, 7)), "2023-02-03 04:05:06.0070000"),
            ("Sid", V.Of(0x13, sid), "S-1-5-32-544"),
            ("Hex32", V.Of(0x14, BitConverter.GetBytes(0x1F)), "0x1F"),
            ("Hex64", V.Hex64(0x7FFF0000), "0x7FFF0000"),
            ("Strings", V.Of(0x81, Encoding.Unicode.GetBytes("a\0b\0")), "a, b"),
            ("Real64s", V.Of(0x8C, BitConverter.GetBytes(1.5).Concat(BitConverter.GetBytes(-2.25)).ToArray()), "1.5,-2.25"),
            ("Sids", V.Of(0x93, sid.Concat(sid).ToArray()), "S-1-5-32-544,S-1-5-32-544"),
            ("Hex64s", V.Of(0x95, BitConverter.GetBytes(1L).Concat(BitConverter.GetBytes(255L)).ToArray()), "0x1,0xFF"),
            ("Controls", V.Str("line1\r\nline2\0"), "line1, line2")
        };

        var b = new EvtxBuilder();
        b.Chunk().Record(1, When, r => r.FragmentHeader().TemplateInstance(
            EventTemplate(values.Select((t, i) => Data(t.Name, 8 + i, t.Value.Type)).ToArray()), 0,
            SystemValues(1, 1, values.Select(t => t.Value).ToArray())));

        var json = Newtonsoft.Json.Linq.JObject.Parse(Parse(b.Build()).Single().PayloadAsJson());
        var data = json["EventData"]["Data"].ToDictionary(t => (string)t["@Name"], t => (string)t["#text"]);
        foreach (var (name, _, expected) in values)
        {
            data[name].Should().Be(expected, name);
        }
    }

    private static byte[] SystemTime(int year, int month, int day, int hour, int minute, int second, int ms)
    {
        return new[] { year, month, 0, day, hour, minute, second, ms }.SelectMany(t => BitConverter.GetBytes((ushort)t)).ToArray();
    }

    [Test]
    public void ElementsWhoseDependencyIsNullAreRendered()
    {
        //MS-EVEN6 2.2.12.1 leaves them out, but EvtxECmd always showed them (e.g. <Binary></Binary>), as Rust's evtx does
        var b = new EvtxBuilder();
        b.Chunk().Record(1, When, r => r.FragmentHeader().TemplateInstance(
            EventTemplate(Data("Present", 8, 0x01, true, 8), Data("Missing", 9, 0x01, true, 9)), 0,
            SystemValues(1, 1, V.Str("yes"), V.Null())));

        Parse(b.Build()).Single().PayloadAsJson().Should().Be("{\"EventData\":{\"Data\":[{\"@Name\":\"Present\",\"#text\":\"yes\"},{\"@Name\":\"Missing\"}]}}");
    }

    [Test]
    public void CharacterAndEntityReferencesCDataAndProcessingInstructions()
    {
        var b = new EvtxBuilder();
        b.Chunk().Record(1, When, r => r.FragmentHeader().TemplateInstance(
            EventTemplate(ed =>
            {
                ed.Element("Data", true, c => c.Text("a").CharRef('&').Text("b").EntityRef("lt").Text("c"), -1, ("Name", v => v.Text("Refs")));
                ed.Element("Data", true, c => c.CData("x < y"), -1, ("Name", v => v.Text("CData")));
                ed.Element("Data", true, c => c.Pi("target", "data"), -1, ("Name", v => v.Text("Pi")));
            }), 0, SystemValues(1, 1)));

        var e = Parse(b.Build()).Single();

        e.ConvertPayloadToXml().Should().Contain("<Data Name=\"Refs\">a&amp;b&lt;c</Data>")
            .And.Contain("<Data Name=\"CData\"><![CDATA[x < y]]></Data>")
            .And.Contain("<Data Name=\"Pi\">\r\n      <?target data?>\r\n    </Data>");
        e.PayloadAsJson().Should().Contain("{\"@Name\":\"Refs\",\"#text\":\"a&b<c\"}")
            .And.Contain("{\"@Name\":\"CData\",\"#cdata-section\":\"x < y\"}");
    }

    [Test]
    public void ValuesAreEscapedOnce()
    {
        //text used to be escaped twice (a literal &amp; in the payload), and < in attributes broke the record
        var b = new EvtxBuilder();
        b.Chunk().Record(1, When, r => r.FragmentHeader().TemplateInstance(
            EventTemplate(ed => ed.Element("Data", true, c => c.Substitution(8, 0x01), -1, ("Name", v => v.Substitution(9, 0x01)))), 0,
            SystemValues(1, 1, V.Str("PCI\\VEN_8086&DEV_1 <x> \"q\""), V.Str("a<b\"c"))));

        var e = Parse(b.Build()).Single();

        e.ConvertPayloadToXml().Should().Contain("<Data Name=\"a&lt;b&quot;c\">PCI\\VEN_8086&amp;DEV_1 &lt;x&gt; \"q\"</Data>");
        e.PayloadAsJson().Should().Be("{\"EventData\":{\"Data\":{\"@Name\":\"a<b\\\"c\",\"#text\":\"PCI\\\\VEN_8086&DEV_1 <x> \\\"q\\\"\"}}}");
    }

    [Test]
    public void RecordsWithoutTemplates()
    {
        //as Windows Event Forwarding writes them: elements with no dependency ids, typed inline values, RenderingInfo
        var b = new EvtxBuilder();
        b.Chunk().Record(7, When, r => r.FragmentHeader().Element("Event", false, e =>
        {
            e.Element("System", false, s =>
            {
                s.Element("Provider", false, null, -1, ("Name", v => v.Text("Microsoft-Windows-Security-Auditing")));
                s.Element("EventID", false, c => c.TypedText(0x06, BitConverter.GetBytes((ushort)4625)));
                s.Element("Level", false, c => c.TypedText(0x04, new byte[] { 0 }));
                s.Element("TimeCreated", false, null, -1, ("SystemTime", v => v.TypedText(0x11, BitConverter.GetBytes(When.ToFileTimeUtc()))));
                s.Element("EventRecordID", false, c => c.TypedText(0x0A, BitConverter.GetBytes(99UL)));
                s.Element("Channel", false, c => c.Text("Security"));
                s.Element("Computer", false, c => c.Text("dc1.example.com"));
            });
            e.Element("EventData", false, ed => ed.Element("Data", false, c => c.Text("bob"), -1, ("Name", v => v.Text("TargetUserName"))));
            e.Element("RenderingInfo", false, ri =>
            {
                ri.Element("Message", false, c => c.Text("An account failed to log on."));
                ri.Element("Level", false, c => c.Text("Information"));
                ri.Element("Keywords", false, k => k.Element("Keyword", false, c => c.Text("Audit Failure")));
            }, -1, ("Culture", v => v.Text("en-US")));
        }).Eof());

        var record = Parse(b.Build()).Single();

        record.EventId.Should().Be(4625);
        record.Level.Should().Be("LogAlways", "Level comes from System, not RenderingInfo");
        record.Channel.Should().Be("Security");
        record.EventRecordId.Should().Be("99");
        record.TimeCreated.Should().Be(new DateTimeOffset(When));
        record.PayloadAsJson().Should().Be("{\"EventData\":{\"Data\":{\"@Name\":\"TargetUserName\",\"#text\":\"bob\"}}}");
        record.RenderingInfo.Should().Be("{\"Level\":\"Information\",\"Keywords\":\"Audit Failure\",\"Message\":\"An account failed to log on.\"}");
    }

    [Test]
    public void BinXmlValuesAreRenderedInPlace()
    {
        //UserData is typically a BinXml substitution value with a template instance of its own
        var b = new EvtxBuilder();
        b.Chunk().Record(1, When, r => r.FragmentHeader().TemplateInstance(
            d => d.Element("Event", true, e =>
            {
                e.Element("System", true, s => s.Element("EventID", true, c => c.Substitution(0, 0x06)));
                e.Substitution(1, 0x21, true);
            }), 0,
            V.U16(1102),
            V.BinXml(n => n.FragmentHeader().TemplateInstance(
                d => d.Element("UserData", true, u => u.Element("LogFileCleared", true, l =>
                    l.Element("SubjectUserName", true, c => c.Substitution(0, 0x01)))), 0, V.Str("admin")))));

        var e = Parse(b.Build()).Single();

        e.EventId.Should().Be(1102);
        e.PayloadAsJson().Should().Be("{\"UserData\":{\"LogFileCleared\":{\"SubjectUserName\":\"admin\"}}}");
    }

    [Test]
    public void DamagedRecordsAreReportedAndTheRestParsed()
    {
        var b = new EvtxBuilder();
        var chunk = b.Chunk();
        chunk.Record(1, When, r => r.FragmentHeader().TemplateInstance(EventTemplate(), 0, SystemValues(1, 1)));
        //an unknown token where an element should be
        chunk.Record(2, When, r => r.FragmentHeader().Byte(0x3F).Byte(0x3F));
        chunk.Record(3, When, r => r.FragmentHeader().Element("Event", false, e => e.Element("System", false, s => s.Element("EventID", false, c => c.Text("3")))).Eof());

        var records = Parse(b.Build(), out var log);

        records.Select(t => t.RecordNumber).Should().Equal(1, 3);
        log.ErrorRecords.Keys.Should().Equal(2L);
        log.ErrorRecords[2].Should().Contain("token 0x3F");
    }

    [Test]
    public void HiddenRecordsInSlackAreFound()
    {
        //a record grown over the one after it (DanderSpritz's eventlogedit)
        var b = new EvtxBuilder();
        var chunk = b.Chunk();
        chunk.Record(1, When, r => r.FragmentHeader().Element("Event", false, e => e.Element("System", false, s => s.Element("EventID", false, c => c.Text("1")))).Eof(),
            slack => slack.HiddenRecord(2, When, r => r.FragmentHeader().Element("Event", false, e => e.Element("System", false, s => s.Element("EventID", false, c => c.Text("4688")))).Eof()));

        var records = Parse(b.Build());

        records.Should().HaveCount(2);
        records[1].HiddenRecord.Should().BeTrue();
        records[1].EventId.Should().Be(4688);
        records[0].ExtraDataOffset.Should().BeGreaterThan(0);
    }

    [Test]
    public void BadInputDoesNotThrow()
    {
        FluentActions.Invoking(() => new EventLog(new MemoryStream(new byte[10]))).Should().Throw<EventLogParseException>()
            .WithMessage("Invalid signature! Expected 'ElfFile'");

        var sample = TestFiles.RustSample("security.evtx");
        var random = new Random(5);
        for (var i = 0; i < 20; i++)
        {
            var bytes = (byte[])sample.Clone();
            for (var c = 0; c < 2000; c++)
            {
                bytes[random.Next(0x1000, bytes.Length)] = (byte)random.Next(256);
            }

            var log = new EventLog(new MemoryStream(bytes));
            foreach (var r in log.GetEventRecords())
            {
                _ = r.PayloadAsJson();
                _ = r.ConvertPayloadToXml();
            }
        }
    }
}
