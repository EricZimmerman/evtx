using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            using (var fs = new FileStream(sysLog,FileMode.Open,FileAccess.Read))
            {
            
                var es = new EventLog(fs);

              foreach (var eventRecord in es.GetEventRecords())
              {
                  l.Info($"Record: {eventRecord}");
                  eventRecord.ConvertPayloadToXml();
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

            using (var fs = new FileStream(sysLog,FileMode.Open,FileAccess.Read))
            {
            
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                    l.Info($"Record: {eventRecord}");
                    eventRecord.ConvertPayloadToXml();
                }

                

            }

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

            using (var fs = new FileStream(sysLog,FileMode.Open,FileAccess.Read))
            {
            
                var es = new EventLog(fs);

                foreach (var eventRecord in es.GetEventRecords())
                {
                    l.Info($"Record: {eventRecord}");
                    eventRecord.ConvertPayloadToXml();
                }

                

            }

            l.Info($"Total: {total}");

        }
    }
}
