using System;
using System.IO;
using System.Linq;
using NFluent;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace evtx.Test
{
    public class TestMain
    {
        [Test]
        public void vss14Sec()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog =
                @"D:\Temp\KAPE\vss012\Windows\system32\winevt\logs\Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider%4Operational.evtx";

            var total = 0;
            var total2 = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                    //l.Info($"Record #: {eventRecord.RecordNumber}");
                    if (eventRecord.EventId == 4000)
                    {
                        l.Info(
                            $"Record #: {eventRecord.RecordNumber} {eventRecord.Channel} {eventRecord.Computer} {eventRecord.TimeCreated}  {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                    }

                    //   eventRecord.ConvertPayloadToXml();

                    total += 1;
                }

                foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                {
                    total2 += esEventIdMetric.Value;
                    l.Info($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                }

                l.Info($"Total from here: {total:N0}");
                l.Info($"Total2 from here: {total2:N0}");
                l.Info($"Event log details: {es}");
                l.Info($"Event log error count: {es.ErrorRecords.Count:N0}");

                Check.That(es.ErrorRecords.Count).IsEqualTo(0);
            }
        }

        [Test]
        public void vss15Sec()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog = @"D:\Temp\KAPE\vss015\Windows\system32\winevt\logs\Security.evtx";

            var total = 0;
            var total2 = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                    //l.Info($"Record #: {eventRecord.RecordNumber}");
                    if (eventRecord.EventId == 4000)
                    {
                        l.Info(
                            $"Record #: {eventRecord.RecordNumber} {eventRecord.Channel} {eventRecord.Computer} {eventRecord.TimeCreated}  {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                    }

                    //   eventRecord.ConvertPayloadToXml();

                    total += 1;
                }

                foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                {
                    total2 += esEventIdMetric.Value;
                    l.Info($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                }

                l.Info($"Total from here: {total:N0}");
                l.Info($"Total2 from here: {total2:N0}");
                l.Info($"Event log details: {es}");
                l.Info($"Event log error count: {es.ErrorRecords.Count:N0}");

                Check.That(es.ErrorRecords.Count).IsEqualTo(2);
            }
        }

        [Test]
        public void MapTest1()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            //var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\System.evtx";


            EventLog.LoadMaps(@"D:\Code\evtx\evtx\Maps");

            Check.That(EventLog.EventLogMaps.Count).IsStrictlyGreaterThan(0);
        }

           [Test]
        public void DanderTest()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog = @"D:\!Downloads\System2.evtx";

            var total = 0;
            var total2 = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                   
                        l.Info(
                            $"Record #: {eventRecord.RecordNumber} Hidden: {eventRecord.HiddenRecord}, Timestamp: {eventRecord.TimeCreated.ToUniversalTime()} Channel: {eventRecord.Channel} Computer: {eventRecord.Computer} {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                   

                    //   eventRecord.ConvertPayloadToXml();

                    total += 1;
                }

                foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                {
                    total2 += esEventIdMetric.Value;
                    l.Info($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                }

                l.Info($"Total from here: {total:N0}");
                l.Info($"Total2 from here: {total2:N0}");
                l.Info($"Event log details: {es}");
                l.Info($"Event log error count: {es.ErrorRecords.Count:N0}");

                Check.That(es.ErrorRecords.Count).IsEqualTo(0);
            }
        }

        [Test]
        public void SystemLog()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\System.evtx";

            var total = 0;
            var total2 = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                    //l.Info($"Record #: {eventRecord.RecordNumber}");
                    if (eventRecord.EventId == 4000)
                    {
                        l.Info(
                            $"Record #: {eventRecord.RecordNumber} {eventRecord.Channel} {eventRecord.Computer} {eventRecord.TimeCreated}  {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                    }

                    //   eventRecord.ConvertPayloadToXml();

                    total += 1;
                }

                foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                {
                    total2 += esEventIdMetric.Value;
                    l.Info($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                }

                l.Info($"Total from here: {total:N0}");
                l.Info($"Total2 from here: {total2:N0}");
                l.Info($"Event log details: {es}");
                l.Info($"Event log error count: {es.ErrorRecords.Count:N0}");

                Check.That(es.ErrorRecords.Count).IsEqualTo(0);
            }
        }

        [Test]
        public void DBlakeSystemLog()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Trace;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog = @"C:\Temp\DBlakeSystem.evtx";

            var total = 0;
            var total2 = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                    //l.Info($"Record #: {eventRecord.RecordNumber}");
                    if (eventRecord.EventId == 4000)
                    {
                        l.Info(
                            $"Record #: {eventRecord.RecordNumber} {eventRecord.Channel} {eventRecord.Computer} {eventRecord.TimeCreated}  {eventRecord.PayloadData1} {eventRecord.PayloadData2}");
                    }

                    //   eventRecord.ConvertPayloadToXml();

                    total += 1;
                }

                foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                {
                    total2 += esEventIdMetric.Value;
                    l.Info($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                }

                l.Info($"Total from here: {total:N0}");
                l.Info($"Total2 from here: {total2:N0}");
                l.Info($"Event log details: {es}");
                l.Info($"Event log error count: {es.ErrorRecords.Count:N0}");

                Check.That(es.ErrorRecords.Count).IsEqualTo(0);
            }
        }

        [Test]
        public void SecurityLog()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\Security.evtx";

            var total = 0;
            var total2 = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                EventLog.LoadMaps(@"D:\Code\evtx\evtx\Maps");

                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //     l.Info($"Record: {eventRecord}");
                {
                    //  eventRecord.ConvertPayloadToXml();
                }

                foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                {
                    total2 += esEventIdMetric.Value;
                    l.Info($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                }

                l.Info($"Total from here: {total:N0}");
                l.Info($"Total2 from here: {total2:N0}");
                l.Info($"Event log details: {es}");
                l.Info($"Event log error count: {es.ErrorRecords.Count:N0}");

                Check.That(es.ErrorRecords.Count).IsEqualTo(0);
            }

            l.Info($"Total: {total}");
        }


        [Test]
        public void DirTestEZW1()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\EZW_Home\1").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
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


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestEZW2()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\EZW_Home\2").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //            l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestEZW3()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\EZW_Home\3").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //            l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestOther1()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\othertests\1").ToList();
            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //          l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }

                l.Info(file);
            }

            var total = 0;

            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestOther2()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\othertests\2").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //          l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }

                l.Info(file);
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestOther3()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\othertests\3").ToList();
            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //          l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }

                l.Info(file);
            }

            var total = 0;

            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestDefConFS1()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\DefConFS\1").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //     l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestDefConFS2()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\DefConFS\2").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //     l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestDefConFS3()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\DefConFS\3").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //     l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }


        [Test]
        public void DirTestFury1()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury\1").ToList();

            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //                 l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestFury2()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury\2").ToList();

            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //                 l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestFury3()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury\3").ToList();

            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //                 l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestFury4()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury\4").ToList();

            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //                 l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestToFix()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Debug;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            // var sourceDir = @"D:\SynologyDrive\EventLogs\To Fix\Damaged";
            var sourceDir = @"D:\SynologyDrive\EventLogs\To Fix";
            var files = Directory.GetFiles(sourceDir, "*.evtx").ToList();
            //   var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\To Fix\Template OK","*.evtx").ToList();

            l.Info($"{sourceDir}");

            var total = 0;
            var total2 = 0;

            foreach (var file in files)
            {
                l.Info(
                    $"\r\n\r\n\r\n-------------------------- file {Path.GetFileName(file)}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    try
                    {
                        var es = new EventLog(fs);

                        foreach (var eventRecord in es.GetEventRecords())
                            //  try
                        {
                            //      l.Info( eventRecord);
                            //l.Info(eventRecord.ConvertPayloadToXml());
                            //eventRecord.ConvertPayloadToXml();
                        }

//                        catch (Exception e)
//                        {
//                           l.Error($"Record: {eventRecord} failed to parse: {e.Message} {e.StackTrace}");
//                        }

                        foreach (var esEventIdMetric in es.EventIdMetrics.OrderBy(t => t.Key))
                        {
                            total2 += esEventIdMetric.Value;
                            l.Info($"{esEventIdMetric.Key}: {esEventIdMetric.Value:N0}");
                        }

                        l.Info($"Total from here: {total:N0}");
                        l.Info($"Total2 from here: {total2:N0}");
                        l.Info($"Event log details: {es}");
                        l.Info($"Event log error count: {es.ErrorRecords.Count:N0}");

                        Check.That(es.ErrorRecords.Count).IsEqualTo(0);
                    }
                    catch (Exception e)
                    {
                        l.Error($"FILE : {file} failed to parse: {e.Message} {e.StackTrace}");
                    }
                }
            }
        }


        [Test]
        public void DirTestRomanoff1()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Romanoff\1").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //              l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }


        [Test]
        public void DirTestRomanoff2()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Romanoff\2").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //              l.Info($"Record: {eventRecord}");
                    {
                        eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        //

        [Test]
        public void OneOff()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog = @"C:\temp\Archive-ForwardedEvents-test.evtx";

            // var total = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //      l.Info($"Record: {eventRecord}");
                {
                     eventRecord.ConvertPayloadToXml();
                }

                l.Info($"early : {es.EarliestTimestamp}");
                l.Info($"last: {es.LatestTimestamp}");
            }

         
        }


        [Test]
        public void ApplicationLog()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\Application.evtx";

           // var total = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //      l.Info($"Record: {eventRecord}");
                {
                   // eventRecord.ConvertPayloadToXml();
                }

                   l.Info($"early : {es.EarliestTimestamp}");
                l.Info($"last: {es.LatestTimestamp}");
            }

         
        }

        [Test]
        public void sixty8k()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Trace;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
            var l = LogManager.GetLogger("foo");

            var sysLog =
                @"D:\SynologyDrive\EventLogs\DefConFS\Microsoft-Windows-TerminalServices-RemoteConnectionManager%4Admin.evtx";

            var total = 0;

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //      l.Info($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
            }

            l.Info($"Total: {total}");
        }
    }
}