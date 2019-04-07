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

                foreach (var eventRecord in es.GetEventRecords())
                {
                    //      l.Info($"Record: {eventRecord}");
                    //        eventRecord.ConvertPayloadToXml();
                }
            }

            l.Info($"Total: {total}");
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
                {
                    //     l.Info($"Record: {eventRecord}");
                    //      eventRecord.ConvertPayloadToXml();
                }
            }

            l.Info($"Total: {total}");
        }

       

        [Test]
        public void DirTestEZW()
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


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\EZW_Home").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                    {
                        //            l.Info($"Record: {eventRecord}");
                        //     eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestOther()
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


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\othertests").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                    {
                        //          l.Info($"Record: {eventRecord}");
                        //      eventRecord.ConvertPayloadToXml();
                    }
                }

                l.Info(file);
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        //

        [Test]
        public void DirTestDefConFS()
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


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\DefConFS").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                    {
                        //     l.Info($"Record: {eventRecord}");
                        //   eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }


        [Test]
        public void DirTestFury()
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


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Fury").ToList();

            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                    {
                        //                 l.Info($"Record: {eventRecord}");
                        //           eventRecord.ConvertPayloadToXml();
                    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }

        [Test]
        public void DirTestCorrupt()
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
              var sourceDir = @"D:\SynologyDrive\EventLogs\To Fix\Other";
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
                    {
                        //                 l.Info($"Record: {eventRecord}");
                        //           eventRecord.ConvertPayloadToXml();
                    }

//                    }
//                    catch (Exception e)
//                    {
                    //        l.Error($"***{Path.GetFileName(file)}*** had error: {e.Message}");
                    //    }
                }
            }


            var total = 0;


            l.Info($"Total: {total}");
        }


        [Test]
        public void DirTestRomanoff()
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


            var files = Directory.GetFiles(@"D:\SynologyDrive\EventLogs\Romanoff").ToList();


            foreach (var file in files)
            {
                l.Info($"--------------------------{file}--------------------------");
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    var es = new EventLog(fs);

                    foreach (var eventRecord in es.GetEventRecords())
                    {
                        //              l.Info($"Record: {eventRecord}");
                        //         eventRecord.ConvertPayloadToXml();
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
                {
                    //      l.Info($"Record: {eventRecord}");
                    //       eventRecord.ConvertPayloadToXml();
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
                {
                    //      l.Info($"Record: {eventRecord}");
                    //       eventRecord.ConvertPayloadToXml();
                }
            }

            l.Info($"Total: {total}");
        }
    }
}