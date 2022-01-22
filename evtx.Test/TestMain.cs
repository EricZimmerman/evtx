using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NFluent;
using NUnit.Framework;
using Serilog;

namespace evtx.Test;

public class TestMain
{
    [Test]
    public void vss14Sec()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var sysLog =
            @"D:\Temp\KAPE\vss012\Windows\system32\winevt\logs\Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider%4Operational.evtx";

        var total = 0;
        var total2 = 0;

        using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
        {
            var es = new EventLog(fs);

            foreach (var eventRecord in es.GetEventRecords())
            {
                //Log.Information($"Record #: {eventRecord.RecordNumber}");
                if (eventRecord.EventId == 4000)
                {
                    Log.Information(
                        $"Record #: {eventRecord.RecordNumber} {eventRecord.Channel} {eventRecord.Computer} {eventRecord.TimeCreated}  {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                }

                //   eventRecord.ConvertPayloadToXml();

                total += 1;
            }

            foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
            {
                total2 += esEventIdMetric.Value;
                Log.Information($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
            }

            Log.Information($"Total from here: {total:N0}");
            Log.Information($"Total2 from here: {total2:N0}");
            Log.Information($"Event log details: {es}");
            Log.Information($"Event log error count: {es.ErrorRecords.Count:N0}");

            Check.That(es.ErrorRecords.Count).IsEqualTo(0);
        }
    }

    [Test]
    public void vss15Sec()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var sysLog = @"D:\Temp\KAPE\vss015\Windows\system32\winevt\logs\Security.evtx";

        var total = 0;
        var total2 = 0;

        using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
        {
            var es = new EventLog(fs);

            foreach (var eventRecord in es.GetEventRecords())
            {
                //Log.Information($"Record #: {eventRecord.RecordNumber}");
                if (eventRecord.EventId == 4000)
                {
                    Log.Information(
                        $"Record #: {eventRecord.RecordNumber} {eventRecord.Channel} {eventRecord.Computer} {eventRecord.TimeCreated}  {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                }

                //   eventRecord.ConvertPayloadToXml();

                total += 1;
            }

            foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
            {
                total2 += esEventIdMetric.Value;
                Log.Information($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
            }

            Log.Information($"Total from here: {total:N0}");
            Log.Information($"Total2 from here: {total2:N0}");
            Log.Information($"Event log details: {es}");
            Log.Information($"Event log error count: {es.ErrorRecords.Count:N0}");

            Check.That(es.ErrorRecords.Count).IsEqualTo(2);
        }
    }

    [Test]
    public void MapTest1()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        //var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\System.evtx";


        EventLog.LoadMaps(@"D:\Code\evtx\evtx\Maps");

        Check.That(EventLog.EventLogMaps.Count).IsStrictlyGreaterThan(0);
    }

    [Test]
    public void DanderTest()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var logs = new List<string>();

        logs.Add(@" D:\Temp\logs\Security_danderspritz_3548.evtx");
        logs.Add(@" D:\Temp\logs\Security_deleted_25733.evtx");
        logs.Add(@" D:\Temp\logs\Security_foxit_danderspritz.evtx");
        logs.Add(@" D:\Temp\logs\Security_original.evtx");
        logs.Add(@" D:\Temp\logs\System2.evtx");


        foreach (var sysLog in logs)
        {

            var total = 0;
            var total2 = 0;

            Log.Error(sysLog + " *******************************************" );

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                   
                    Log.Information(
                        $"Record #: {eventRecord.RecordNumber} Hidden: {eventRecord.HiddenRecord}, Timestamp: {eventRecord.TimeCreated.ToUniversalTime()} Channel: {eventRecord.Channel} Computer: {eventRecord.Computer} {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                   

                    //   eventRecord.ConvertPayloadToXml();

                    total += 1;
                }

                foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                {
                    total2 += esEventIdMetric.Value;
                    Log.Information($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                }

                Log.Information($"Total from here: {total:N0}");
                Log.Information($"Total2 from here: {total2:N0}");
                Log.Information($"Event log details: {es}");
                Log.Information($"Event log error count: {es.ErrorRecords.Count:N0}");

                Check.That(es.ErrorRecords.Count).IsEqualTo(0);
            }                
        }

    }

    [Test]
    public void SystemLog()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\System.evtx";

        var total = 0;
        var total2 = 0;

        using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
        {
            var es = new EventLog(fs);

            foreach (var eventRecord in es.GetEventRecords())
            {
                //Log.Information($"Record #: {eventRecord.RecordNumber}");
                if (eventRecord.EventId == 4000)
                {
                    Log.Information(
                        $"Record #: {eventRecord.RecordNumber} {eventRecord.Channel} {eventRecord.Computer} {eventRecord.TimeCreated}  {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                }

                //   eventRecord.ConvertPayloadToXml();

                total += 1;
            }

            foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
            {
                total2 += esEventIdMetric.Value;
                Log.Information($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
            }

            Log.Information($"Total from here: {total:N0}");
            Log.Information($"Total2 from here: {total2:N0}");
            Log.Information($"Event log details: {es}");
            Log.Information($"Event log error count: {es.ErrorRecords.Count:N0}");

            Check.That(es.ErrorRecords.Count).IsEqualTo(0);
        }
    }

    [Test]
    public void DBlakeSystemLog()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var sysLog = @"C:\Temp\DBlakeSystem.evtx";

        var total = 0;
        var total2 = 0;

        using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
        {
            var es = new EventLog(fs);

            foreach (var eventRecord in es.GetEventRecords())
            {
                //Log.Information($"Record #: {eventRecord.RecordNumber}");
                if (eventRecord.EventId == 4000)
                {
                    Log.Information(
                        $"Record #: {eventRecord.RecordNumber} {eventRecord.Channel} {eventRecord.Computer} {eventRecord.TimeCreated}  {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                }

                //   eventRecord.ConvertPayloadToXml();

                total += 1;
            }

            foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
            {
                total2 += esEventIdMetric.Value;
                Log.Information($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
            }

            Log.Information($"Total from here: {total:N0}");
            Log.Information($"Total2 from here: {total2:N0}");
            Log.Information($"Event log details: {es}");
            Log.Information($"Event log error count: {es.ErrorRecords.Count:N0}");

            Check.That(es.ErrorRecords.Count).IsEqualTo(0);
        }
    }

    [Test]
    public void SecurityLog()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();
        var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\Security.evtx";

        var total = 0;
        var total2 = 0;

        using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
        {
            EventLog.LoadMaps(@"D:\Code\evtx\evtx\Maps");

            var es = new EventLog(fs);

            foreach (var eventRecord in es.GetEventRecords())
                //     Log.Information($"Record: {eventRecord}");
            {
                //  eventRecord.ConvertPayloadToXml();
            }

            foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
            {
                total2 += esEventIdMetric.Value;
                Log.Information($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
            }

            Log.Information($"Total from here: {total:N0}");
            Log.Information($"Total2 from here: {total2:N0}");
            Log.Information($"Event log details: {es}");
            Log.Information($"Event log error count: {es.ErrorRecords.Count:N0}");

            Check.That(es.ErrorRecords.Count).IsEqualTo(0);
        }

        Log.Information($"Total: {total}");
    }


    [Test]
    public void DirTestEZW1()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\EZW_Home\1").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestEZW2()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\EZW_Home\2").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //            Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestEZW3()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\EZW_Home\3").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //            Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestOther1()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\othertests\1").ToList();
        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //          Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }

            Log.Information(file);
        }

        var total = 0;

        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestOther2()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();


        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\othertests\2").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //          Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }

            Log.Information(file);
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestOther3()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\othertests\3").ToList();
        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //          Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }

            Log.Information(file);
        }

        var total = 0;

        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestDefConFS1()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\DefConFS\1").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //     Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestDefConFS2()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\DefConFS\2").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //     Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestDefConFS3()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\DefConFS\3").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //     Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }


    [Test]
    public void DirTestFury1()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury\1").ToList();

        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //                 Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestFury2()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury\2").ToList();

        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //                 Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestFury3()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury\3").ToList();

        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //                 Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestFury4()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();


        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury\4").ToList();

        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //                 Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    [Test]
    public void DirTestToFix()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        // var sourceDir = @"D:\SynologyDrive\EventLogs\To Fix\Damaged";
        var sourceDir = @"D:\SynologyDrive\EventLogs\To Fix";
        var files = Directory.GetFiles(sourceDir, "*.evtx").ToList();
        //   var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\To Fix\Template OK","*.evtx").ToList();

        Log.Information($"{sourceDir}");

        var total = 0;
        var total2 = 0;

        foreach (var file in files)
        {
            Log.Information(
                $"\r\n\r\n\r\n-------------------------- file {Path.GetFileName(file)}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //  try
                    {
                        //      Log.Information( eventRecord);
                        //Log.Information(eventRecord.ConvertPayloadToXml());
                        //eventRecord.ConvertPayloadToXml();
                    }

//                        catch (Exception e)
//                        {
//                           l.Error($"Record: {eventRecord} failed to parse: {e.Message} {e.StackTrace}");
//                        }

                    foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                    {
                        total2 += esEventIdMetric.Value;
                        Log.Information($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                    }

                    Log.Information($"Total from here: {total:N0}");
                    Log.Information($"Total2 from here: {total2:N0}");
                    Log.Information($"Event log details: {es}");
                    Log.Information($"Event log error count: {es.ErrorRecords.Count:N0}");

                    Check.That(es.ErrorRecords.Count).IsEqualTo(0);
                }
                catch (Exception e)
                {
                    Log.Error($"FILE : {file} failed to parse: {e.Message} {e.StackTrace}");
                }
            }
        }
    }


    [Test]
    public void DirTestRomanoff1()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Romanoff\1").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //              Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }


    [Test]
    public void DirTestRomanoff2()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();


        var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Romanoff\2").ToList();


        foreach (var file in files)
        {
            Log.Information($"--------------------------{file}--------------------------");
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //              Log.Information($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }
        }


        var total = 0;


        Log.Information($"Total: {total}");
    }

    //

    [Test]
    public void OneOff()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        //     var sysLog = @"C:\temp\Archive-ForwardedEvents-test.evtx";
        var sysLog = @"D:\OneDrive\Desktop\Security.evtx";

        // var total = 0;

        using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
        {
                
            var es = new EventLog(fs);

            foreach (var eventRecord in es.GetEventRecords())
                //      Log.Information($"Record: {eventRecord}");
            {
                eventRecord.ConvertPayloadToXml();
            }

            Log.Information($"early : {es.EarliestTimestamp}");
            Log.Information($"last: {es.LatestTimestamp}");
        }

         
    }


    [Test]
    public void ApplicationLog()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\Application.evtx";

        // var total = 0;

        using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
        {
                
            var es = new EventLog(fs);

            foreach (var eventRecord in es.GetEventRecords())
                //      Log.Information($"Record: {eventRecord}");
            {
                // eventRecord.ConvertPayloadToXml();
            }

            Log.Information($"early : {es.EarliestTimestamp}");
            Log.Information($"last: {es.LatestTimestamp}");
        }

         
    }

    [Test]
    public void sixty8k()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information()
            .CreateLogger();

        var sysLog =
            @"D:\SynologyDrive\EventLogs\DefConFS\Microsoft-Windows-TerminalServices-RemoteConnectionManager%4Admin.evtx";

        var total = 0;

        using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
        {
            var es = new EventLog(fs);

            foreach (var eventRecord in es.GetEventRecords())
                //      Log.Information($"Record: {eventRecord}");
            {
                eventRecord.ConvertPayloadToXml();
            }
        }

        Log.Information("Total: {Total}",total);
    }
}