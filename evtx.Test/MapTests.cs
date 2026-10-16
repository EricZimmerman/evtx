using System;
using System.IO;
using System.Linq;
using System.Xml.XPath;
using FluentAssertions;
using NUnit.Framework;

namespace evtx.Test;

/// <summary>The maps in evtx/Maps (copied next to the tests)</summary>
[TestFixture]
[NonParallelizable] //EventLog.EventLogMaps is static
public class MapTests
{
    [TearDown]
    public void TearDown()
    {
        EventLog.EventLogMaps.Clear();
    }

    [Test]
    public void EveryMapLoads()
    {
        var maps = Path.Combine(TestContext.CurrentContext.TestDirectory, "Maps");
        var count = Directory.GetFiles(maps, "*.map").Length;

        EventLog.LoadMaps(maps).Should().BeFalse("no map should have errors");

        //every map is valid and none is for the same event as another (EventId, Channel and Provider)
        EventLog.EventLogMaps.Should().HaveCount(count);
    }

    [Test]
    public void OpenSshAdminMaps()
    {
        //issue #253
        EventLog.LoadMaps(Path.Combine(TestContext.CurrentContext.TestDirectory, "Maps")).Should().BeFalse();

        var b = new EvtxBuilder();
        var chunk = b.Chunk();
        var when = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        chunk.Record(1, when, r => r.FragmentHeader().Element("Event", false, e =>
        {
            e.Element("System", false, s =>
            {
                s.Element("Provider", false, null, -1, ("Name", v => v.Text("OpenSSH")));
                s.Element("EventID", false, c => c.TypedText(0x06, BitConverter.GetBytes((ushort)2)));
                s.Element("Channel", false, c => c.Text("OpenSSH/Admin"));
            });
            e.Element("EventData", false, ed =>
            {
                ed.Element("Data", false, c => c.Text("sshd"), -1, ("Name", v => v.Text("process")));
                ed.Element("Data", false, c => c.Text("error: connect to 192.168.21.12 port 445 failed: No error"), -1, ("Name", v => v.Text("payload")));
            });
        }).Eof());

        var record = new EventLog(new MemoryStream(b.Build())).GetEventRecords().Single();

        record.MapDescription.Should().Be("SSH error");
        record.PayloadData1.Should().Be("sshd");
        record.PayloadData2.Should().Be("error: connect to 192.168.21.12 port 445 failed: No error");
    }

    [Test]
    public void BadMapsAreReportedAndTheRestLoaded()
    {
        var dir = Path.Combine(Path.GetTempPath(), "evtx-maps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var maps = Path.Combine(TestContext.CurrentContext.TestDirectory, "Maps");
            File.Copy(Path.Combine(maps, "OpenSSH-Admin_OpenSSH_1.map"), Path.Combine(dir, "a.map"));
            File.WriteAllText(Path.Combine(dir, "b.map"), "Author: x\nEventId: [1\n");
            File.WriteAllText(Path.Combine(dir, "c.map"), "Author: x\nEventId: 5\nChannel: c\nProvider: p\n"); //no Description
            File.Copy(Path.Combine(maps, "OpenSSH-Admin_OpenSSH_2.map"), Path.Combine(dir, "d.map"));
            File.Copy(Path.Combine(maps, "OpenSSH-Admin_OpenSSH_2.map"), Path.Combine(dir, "e.map")); //a duplicate is skipped

            EventLog.LoadMaps(dir).Should().BeTrue();
            EventLog.EventLogMaps.Keys.Should().BeEquivalentTo("1-OPENSSH/ADMIN-OPENSSH", "2-OPENSSH/ADMIN-OPENSSH");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static EventRecord Battery(params (string Name, string Value)[] data)
    {
        var b = new EvtxBuilder();
        b.Chunk().Record(1, new DateTime(2020, 9, 22, 16, 23, 18, DateTimeKind.Utc), r => r.FragmentHeader().Element("Event", false, e =>
        {
            e.Element("System", false, s =>
            {
                s.Element("Provider", false, null, -1, ("Name", v => v.Text("Microsoft-Windows-Kernel-Power")));
                s.Element("EventID", false, c => c.TypedText(0x06, BitConverter.GetBytes((ushort)105)));
                s.Element("Channel", false, c => c.Text("System"));
            });
            e.Element("EventData", false, ed =>
            {
                foreach (var (name, value) in data)
                {
                    ed.Element("Data", false, c => c.Text(value), -1, ("Name", v => v.Text(name)));
                }
            });
        }).Eof());

        return new EventLog(new MemoryStream(b.Build())).GetEventRecords().Single();
    }

    [Test]
    public void MapValuesCanBeXPathExpressions()
    {
        //issue #99: System_Microsoft-Windows-Kernel-Power_105.map with the battery percentage its comments describe
        var dir = Path.Combine(Path.GetTempPath(), "evtx-maps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var map = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Maps", "System_Microsoft-Windows-Kernel-Power_105.map"));
            map = map.Replace("\n# Documentation:", BatteryPercent + "\n# Documentation:");
            File.WriteAllText(Path.Combine(dir, "a.map"), map);
            EventLog.LoadMaps(dir).Should().BeFalse();

            var record = Battery(("AcOnline", "False"), ("RemainingCapacity", "56293"), ("FullChargeCapacity", "62654"));
            record.PayloadData1.Should().Be("AcOnline: False");
            record.PayloadData2.Should().Be("RemainingCapacity: 56293 of 62654");
            record.PayloadData3.Should().Be("Battery: 90%");

            //a value that is missing or not a number: no percentage
            Battery(("AcOnline", "True"), ("RemainingCapacity", "0"), ("FullChargeCapacity", "0")).PayloadData3.Should().Be("Battery: %");
            Battery(("AcOnline", "True"), ("RemainingCapacity", "56293")).PayloadData3.Should().Be("Battery: %");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private const string BatteryPercent = @"  -
    Property: PayloadData3
    PropertyValue: ""Battery: %Percent%%""
    Values:
      -
        Name: Percent
        Value: ""round(/Event/EventData/Data[@Name=\""RemainingCapacity\""] div /Event/EventData/Data[@Name=\""FullChargeCapacity\""] * 100)""
";

    [TestCase("/Event/Data[@Name=\"A\"]", "56293")]
    [TestCase("/Event/Data[@Name=\"Missing\"]", null)]
    [TestCase("/Event/Data[@Name=\"A\"] + /Event/Data[@Name=\"B\"]", "118947")]
    [TestCase("/Event/Data[@Name=\"A\"] div /Event/Data[@Name=\"B\"] * 100", "89.8474159670572")]
    [TestCase("round(/Event/Data[@Name=\"A\"] div /Event/Data[@Name=\"B\"] * 1000) div 10", "89.8")]
    [TestCase("-1 * 0", "0")]
    [TestCase("/Event/Data[@Name=\"A\"] div 0", null)]
    [TestCase("number(/Event/Data[@Name=\"C\"])", null)]
    [TestCase("1 div 3", "0.333333333333333")]
    [TestCase("123456789 * 1000000000", "123456789000000000")]
    [TestCase("sum(/Event/Data[@Name!=\"C\"])", "118947")]
    [TestCase("count(/Event/Data)", "3")]
    [TestCase("/Event/Data[@Name=\"A\"] > /Event/Data[@Name=\"B\"]", "false")]
    [TestCase("concat(/Event/Data[@Name=\"A\"], \" of \", /Event/Data[@Name=\"B\"])", "56293 of 62654")]
    [TestCase("substring-before(/Event/Data[@Name=\"C\"], \"-\")", "ab")]
    public void ExpressionResults(string xpath, string expected)
    {
        var doc = new XPathDocument(new StringReader("<Event><Data Name=\"A\">56293</Data><Data Name=\"B\">62654</Data><Data Name=\"C\">ab-cd</Data></Event>"));
        EventRecord.MapValue(doc.CreateNavigator(), XPathExpression.Compile(xpath)).Should().Be(expected);
    }

    [Test]
    public void MapsWithBadXPathsAreReported()
    {
        var dir = Path.Combine(Path.GetTempPath(), "evtx-maps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var map = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Maps", "System_Microsoft-Windows-Kernel-Power_105.map"));
            File.WriteAllText(Path.Combine(dir, "a.map"), map.Replace("\n# Documentation:", BatteryPercent.Replace("round(/Event", "round((/Event") + "\n# Documentation:"));

            EventLog.LoadMaps(dir).Should().BeTrue();
            EventLog.EventLogMaps.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
