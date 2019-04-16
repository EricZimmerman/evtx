using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Exceptionless;
using Fclp;
using NLog;
using NLog.Config;
using NLog.Targets;
using ServiceStack;

namespace EvtxECmd
{
    class Program
    {
        private static Logger _logger;

        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;

        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);


        static void Main(string[] args)
        {
             ExceptionlessClient.Default.Startup("tYeWS6A5K5uItgpB44dnNy2qSb2xJxiQWRRGWebq");

            SetupNLog();

            _logger = LogManager.GetLogger("EvtxECmd");

            _fluentCommandLineParser = new FluentCommandLineParser<ApplicationArguments>
            {
                IsCaseSensitive = false
            };

            _fluentCommandLineParser.Setup(arg => arg.File)
                .As('f')
                .WithDescription("File to process ($MFT | $J | $LogFile | $Boot | $SDS). Required\r\n");

            _fluentCommandLineParser.Setup(arg => arg.JsonDirectory)
                .As("json")
                .WithDescription(
                    "Directory to save JSON formatted results to. This or --csv required");
            _fluentCommandLineParser.Setup(arg => arg.JsonName)
                .As("jsonf")
                .WithDescription(
                    "File name to save JSON formatted results to. When present, overrides default name");

            _fluentCommandLineParser.Setup(arg => arg.CsvDirectory)
                .As("csv")
                .WithDescription(
                    "Directory to save CSV formatted results to. This or --json required");

            _fluentCommandLineParser.Setup(arg => arg.CsvName)
                .As("csvf")
                .WithDescription(
                    "File name to save CSV formatted results to. When present, overrides default name\r\n");

            _fluentCommandLineParser.Setup(arg => arg.DateTimeFormat)
                .As("dt")
                .WithDescription(
                    "The custom date/time format to use when displaying time stamps. Default is: yyyy-MM-dd HH:mm:ss.fffffff")
                .SetDefault("yyyy-MM-dd HH:mm:ss.fffffff");


            _fluentCommandLineParser.Setup(arg => arg.Debug)
                .As("debug")
                .WithDescription("Show debug information during processing").SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.Trace)
                .As("trace")
                .WithDescription("Show trace information during processing\r\n").SetDefault(false);

            var header =
                $"EvtxECmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/evtx";

            var footer = @"Examples: MFTECmd.exe -f ""C:\Temp\SomeMFT"" --csv ""c:\temp\out"" --csvf MyOutputFile.csv" +
                         "\r\n\t " +
                         @" MFTECmd.exe -f ""C:\Temp\SomeMFT"" --csv ""c:\temp\out""" + "\r\n\t " +
                         @" MFTECmd.exe -f ""C:\Temp\SomeMFT"" --json ""c:\temp\jsonout""" + "\r\n\t " +
                         @" MFTECmd.exe -f ""C:\Temp\SomeMFT"" --body ""c:\temp\bout"" --bdl c" + "\r\n\t " +
                         @" MFTECmd.exe -f ""C:\Temp\SomeMFT"" --de 5-5" + "\r\n\t " +
                         "\r\n\t" +
                         "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

            _fluentCommandLineParser.SetupHelp("?", "help")
                .WithHeader(header)
                .Callback(text => _logger.Info(text + "\r\n" + footer));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                _logger.Error("");
                _logger.Error(result.ErrorText);

                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                return;
            }

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty())
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("-f is required. Exiting");
                return;
            }

        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void SetupNLog()
        {
            if (File.Exists(Path.Combine(BaseDirectory, "Nlog.config")))
            {
                return;
            }

            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
        }
    }

    internal class ApplicationArguments
    {
        public string File { get; set; }
        public string CsvDirectory { get; set; }
        public string JsonDirectory { get; set; }
        public string DateTimeFormat { get; set; }
      
        public bool Debug { get; set; }
        public bool Trace { get; set; }

        public string CsvName { get; set; }
        public string JsonName { get; set; }
    }
}
