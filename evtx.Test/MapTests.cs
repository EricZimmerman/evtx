using System;
using System.IO;
using System.Linq;
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
}
