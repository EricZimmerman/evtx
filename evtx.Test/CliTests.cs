using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace evtx.Test;

/// <summary>Runs the built EvtxECmd.dll (copied next to the tests, with the Maps folder) as a separate process</summary>
[TestFixture]
public class CliTests
{
    private string _dir;
    private string Out => Path.Combine(_dir, "out");

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "evtxecmd-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_dir, true);
    }

    private (int ExitCode, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _dir
        };
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "EvtxECmd.dll"));
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Close();

        process.WaitForExit(300_000).Should().BeTrue("EvtxECmd should finish");
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private string OutFile(string suffix) => Directory.GetFiles(Out, "*" + suffix).Should().ContainSingle().Subject;

    [Test]
    public void ExitCodes()
    {
        Run().ExitCode.Should().Be(1);
        Run("-f", Path.Combine(_dir, "nope.evtx"), "--csv", Out).ExitCode.Should().Be(1);

        var notEvtx = Path.Combine(_dir, "text.evtx");
        File.WriteAllText(notEvtx, "not an event log");
        var (exitCode, output) = Run("-f", notEvtx, "--csv", Out);
        // a damaged file is reported, but it is not a failure of the tool
        exitCode.Should().Be(0);
        output.Should().Contain("is not an evtx file");
    }

    [Test]
    public void OutputThatCannotBeWrittenExitsWithOne()
    {
        var file = TestFiles.RustSampleFile("security.evtx", _dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "");

        Run("-f", file, "--csv", blocker).ExitCode.Should().Be(1);
    }

    [Test]
    public void LockedFileExitsWithOne()
    {
        if (OperatingSystem.IsWindows() == false)
        {
            Assert.Ignore("Windows only");
        }

        var file = TestFiles.RustSampleFile("security.evtx", _dir);

        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // not elevated: "administrator privileges are needed to read it raw"; elevated, the raw read of a temp file
            // either works (0) or fails (1), so only the non-elevated case is checked
            var (exitCode, output) = Run("-f", file, "--csv", Out);
            if (output.Contains("administrator privileges are needed"))
            {
                exitCode.Should().Be(1);
            }
        }
    }

    [Test]
    public void UnreadableFolderExitsWithOne()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows only");
            return;
        }

        // A folder under -d that can't be read is reported, the rest is processed, and the exit code is 1
        var logs = Path.Combine(_dir, "logs");
        Directory.CreateDirectory(logs);
        TestFiles.RustSampleFile("Archive-ForwardedEvents-test.evtx", logs);
        var denied = Path.Combine(logs, "denied");
        Directory.CreateDirectory(denied);
        TestFiles.RustSampleFile("security.evtx", denied);

        using (new DeniedFolder(denied))
        {
            var (exitCode, output) = Run("-d", logs, "--csv", Out);

            exitCode.Should().Be(1);
            output.Should().Contain("Unable to read folder " + denied);
            output.Should().Contain("Archive-ForwardedEvents-test.evtx").And.NotContain("security.evtx");
        }
    }

    [Test]
    public void ForwardedEventsToCsvJsonAndXml()
    {
        var file = TestFiles.RustSampleFile("Archive-ForwardedEvents-test.evtx", _dir);

        var (exitCode, output) = Run("-f", file, "--csv", Out, "--json", Out, "--xml", Out);

        exitCode.Should().Be(0, output);
        output.Should().Contain("Records included: 653 Errors: 0");

        var csv = File.ReadAllLines(OutFile(".csv"));
        csv[0].TrimStart('﻿').Should().Be("RecordNumber,EventRecordId,TimeCreated,EventId,Level,Provider,Channel,ProcessId,ThreadId,Computer,ChunkNumber,UserId,MapDescription,UserName,RemoteHost,PayloadData1,PayloadData2,PayloadData3,PayloadData4,PayloadData5,PayloadData6,ExecutableInfo,HiddenRecord,SourceFile,Keywords,ExtraDataOffset,Payload,RenderingInfo");
        csv[1].Should().StartWith("127638931,1447406953,2019-11-08 23:20:54.6705004,4625,LogAlways,Microsoft-Windows-Security-Auditing,Security,512,12960,slad1.saclink.csus.edu,0,,Failed logon,-\\-,IRT-PA-IDM1 (10.115.247.238),Target: IRT-PA-IDM1\\psadmin,");

        var json = File.ReadAllLines(OutFile(".json"));
        json.Should().HaveCount(653);
        json[0].Should().Contain("\"RenderingInfo\":").And.Contain("\"Payload\":\"{\\\"EventData\\\"");

        File.ReadAllText(OutFile(".xml")).Should().Contain("<EventID>4625</EventID>").And.Contain("<RenderingInfo Culture=\"en-US\">");
    }

    [Test]
    public void IncludeFilterAndDirectories()
    {
        var logs = Path.Combine(_dir, "logs");
        Directory.CreateDirectory(Path.Combine(logs, "sub"));
        TestFiles.RustSampleFile("security.evtx", logs);
        TestFiles.RustSampleFile("system.evtx", Path.Combine(logs, "sub"));

        var (exitCode, output) = Run("-d", logs, "--csv", Out, "--inc", "4624");

        exitCode.Should().Be(0, output);
        output.Should().Contain("Processed 2 files");
        var rows = File.ReadAllLines(OutFile(".csv")).Skip(1).ToList();
        rows.Select(t => t.Split(',')[3]).Should().NotBeEmpty().And.OnlyContain(t => t == "4624");
    }
}
