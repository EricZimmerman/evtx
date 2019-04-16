using System.IO;
using System.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace evtx.Test
{
    public class TestMain
    {
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

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

//                var r = es.GetEventRecords().First();
//
//                l.Info(r.ConvertPayloadToXml());


                foreach (var eventRecord in es.GetEventRecords())
                {
                    //l.Info($"Record #: {eventRecord.RecordNumber}");
                    //l.Info($"{eventRecord.ConvertPayloadToXml()}");

                    eventRecord.ConvertPayloadToXml();

                    //    File.AppendAllText(@"C:\temp\sys.txt",eventRecord.ConvertPayloadToXml());

                    total += 1;
                }

                l.Info($"Total from here: {total:N0}");
                l.Info($"Event log details: {es}");
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

            using (var fs = new FileStream(sysLog, FileMode.Open, FileAccess.Read))
            {
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                    //     l.Info($"Record: {eventRecord}");
                {
                    eventRecord.ConvertPayloadToXml();
                }
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

            foreach (var file in files)
            {
                l.Info($"-------------------------- file {Path.GetFileName(file)}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
//                    try
//                    {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                        //  try
                    {
                        //    l.Info( eventRecord);
                        //    l.Info( eventRecord.ConvertPayloadToXml());
                        eventRecord.ConvertPayloadToXml();
                    }

//                        catch (Exception e)
//                        {
//                           l.Error($"Record: {eventRecord} failed to parse: {e.Message} {e.StackTrace}");
//                        }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
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

        [Test]
        public void ApplicationLog()
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

            var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\Application.evtx";

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