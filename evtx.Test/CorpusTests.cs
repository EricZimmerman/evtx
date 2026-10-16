using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace evtx.Test;

/// <summary>
///     Every .evtx in a local collection that is not in the repository: Corpus/EventLogs next to the repository (or
///     EventLogs next to it), or EVTX_CORPUS. Explicit (slow); skipped when the folder is missing
/// </summary>
[TestFixture]
[Explicit]
public class CorpusTests
{
    private static string CorpusPath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("EVTX_CORPUS");
        if (string.IsNullOrEmpty(fromEnvironment) == false && Directory.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            foreach (var candidate in new[] { Path.Combine(dir.FullName, "Corpus", "EventLogs"), Path.Combine(dir.FullName, "EventLogs") })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            dir = dir.Parent;
        }

        Assert.Ignore("Event log corpus not found");
        return null;
    }

    [Test]
    public void EveryEventLogParses()
    {
        var files = Directory.GetFiles(CorpusPath(), "*.evtx", SearchOption.AllDirectories);
        files.Should().NotBeEmpty();

        long records = 0, errors = 0;
        foreach (var file in files)
        {
            using var fs = File.OpenRead(file);
            var log = new EventLog(fs);
            foreach (var r in log.GetEventRecords())
            {
                records++;
                r.PayloadAsJson().Should().NotBeNull();
            }

            errors += log.ErrorRecords.Count;
        }

        TestContext.Out.WriteLine($"{files.Length:N0} files, {records:N0} records, {errors:N0} record errors");
        records.Should().BeGreaterThan(0);
    }
}
