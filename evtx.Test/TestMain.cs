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
        public void Foo()
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


            var sysLog = @"D:\SynologyDrive\EventLogs\HP_Spec\System.evtx";

            using (var fs = new FileStream(sysLog,FileMode.Open,FileAccess.Read))
            {
            
                var es = new EventLog(fs);

                var l = LogManager.GetLogger("foo");

                l.Info(es.NextRecordId);
                l.Info($"Current chunk: {es.CurrentChunk:N0}, chunk count: {es.ChunkCount:N0}");
                l.Info(es.Version + "." + es.Revision);
                l.Info($"Dirty: {es.IsDirty}");
                l.Info($"Log full: {es.IsLogFull}");
                l.Info($"CRC: {es.Crc:X}");

            }


        }
    }
}
