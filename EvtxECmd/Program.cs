using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Xml;
using Alphaleonis.Win32.Filesystem;
using evtx;
using Exceptionless;
using Fclp;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using RawCopy;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using EventLog = evtx.EventLog;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace EvtxECmd
{
    internal class Program
    {
        private static Logger _logger;

        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;

        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static Dictionary<string, int> _errorFiles;
        private static int _fileCount;

        private static CsvWriter _csvWriter;
        private static StreamWriter _swCsv;
        private static StreamWriter _swJson;
        private static StreamWriter _swXml;
        private static HashSet<int> _includeIds;
        private static HashSet<int> _excludeIds;

        private static DateTimeOffset? _startDate;
        private static DateTimeOffset? _endDate;
        private static int _droppedEvents;

        private static readonly string VssDir = @"C:\____vssMount";

        private static void Main(string[] args)
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
                .WithDescription("File to process. This or -d is required\r\n");
            _fluentCommandLineParser.Setup(arg => arg.Directory)
                .As('d')
                .WithDescription("Directory to process that contains evtx files. This or -f is required");

            _fluentCommandLineParser.Setup(arg => arg.CsvDirectory)
                .As("csv")
                .WithDescription(
                    "Directory to save CSV formatted results to."); // This, --json, or --xml required

            _fluentCommandLineParser.Setup(arg => arg.CsvName)
                .As("csvf")
                .WithDescription(
                    "File name to save CSV formatted results to. When present, overrides default name");

            _fluentCommandLineParser.Setup(arg => arg.JsonDirectory)
                .As("json")
                .WithDescription(
                    "Directory to save JSON formatted results to."); // This, --csv, or --xml required
            _fluentCommandLineParser.Setup(arg => arg.JsonName)
                .As("jsonf")
                .WithDescription(
                    "File name to save JSON formatted results to. When present, overrides default name");

            _fluentCommandLineParser.Setup(arg => arg.XmlDirectory)
                .As("xml")
                .WithDescription(
                    "Directory to save XML formatted results to."); // This, --csv, or --json required

            _fluentCommandLineParser.Setup(arg => arg.XmlName)
                .As("xmlf")
                .WithDescription(
                    "File name to save XML formatted results to. When present, overrides default name\r\n");

            _fluentCommandLineParser.Setup(arg => arg.DateTimeFormat)
                .As("dt")
                .WithDescription(
                    "The custom date/time format to use when displaying time stamps. Default is: yyyy-MM-dd HH:mm:ss.fffffff")
                .SetDefault("yyyy-MM-dd HH:mm:ss.fffffff");

            _fluentCommandLineParser.Setup(arg => arg.IncludeIds)
                .As("inc")
                .WithDescription(
                    "List of event IDs to process. All others are ignored. Overrides --exc Format is 4624,4625,5410")
                .SetDefault(string.Empty);

            _fluentCommandLineParser.Setup(arg => arg.ExcludeIds)
                .As("exc")
                .WithDescription(
                    "List of event IDs to IGNORE. All others are included. Format is 4624,4625,5410")
                .SetDefault(string.Empty);

            _fluentCommandLineParser.Setup(arg => arg.StartDate)
                .As("sd")
                .WithDescription(
                    "Start date for including events (UTC). Anything OLDER than this is dropped. Format should match --dt")
                .SetDefault(string.Empty);

            _fluentCommandLineParser.Setup(arg => arg.EndDate)
                .As("ed")
                .WithDescription(
                    "End date for including events (UTC). Anything NEWER than this is dropped. Format should match --dt")
                .SetDefault(string.Empty);

            _fluentCommandLineParser.Setup(arg => arg.FullJson)
                .As("fj")
                .WithDescription(
                    "When true, export all available data when using --json. Default is FALSE.")
                .SetDefault(false);
            _fluentCommandLineParser.Setup(arg => arg.PayloadAsJson)
                .As("pj")
                .WithDescription(
                    "When true, include event *payload* as json. Default is TRUE.")
                .SetDefault(true);

            _fluentCommandLineParser.Setup(arg => arg.Metrics)
                .As("met")
                .WithDescription(
                    "When true, show metrics about processed event log. Default is TRUE.\r\n")
                .SetDefault(true);

            _fluentCommandLineParser.Setup(arg => arg.MapsDirectory)
                .As("maps")
                .WithDescription(
                    "The path where event maps are located. Defaults to 'Maps' folder where program was executed\r\n  ")
                .SetDefault(Path.Combine(BaseDirectory, "Maps"));

            _fluentCommandLineParser.Setup(arg => arg.Vss)
                .As("vss")
                .WithDescription(
                    "Process all Volume Shadow Copies that exist on drive specified by -f or -d . Default is FALSE")
                .SetDefault(false);
            _fluentCommandLineParser.Setup(arg => arg.Dedupe)
                .As("dedupe")
                .WithDescription(
                    "Deduplicate -f or -d & VSCs based on SHA-1. First file found wins. Default is TRUE\r\n")
                .SetDefault(true);

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

            var footer =
                @"Examples: EvtxECmd.exe -f ""C:\Temp\Application.evtx"" --csv ""c:\temp\out"" --csvf MyOutputFile.csv" +
                "\r\n\t " +
                @" EvtxECmd.exe -f ""C:\Temp\Application.evtx"" --csv ""c:\temp\out""" + "\r\n\t " +
                @" EvtxECmd.exe -f ""C:\Temp\Application.evtx"" --json ""c:\temp\jsonout""" + "\r\n\t " +
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

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty() &&
                _fluentCommandLineParser.Object.Directory.IsNullOrEmpty())
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("-f or -d is required. Exiting");
                return;
            }

            _logger.Info(header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

            if (IsAdministrator() == false)
            {
                _logger.Fatal("Warning: Administrator privileges not found!\r\n");
            }

            if (_fluentCommandLineParser.Object.Debug)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Debug);
            }

            if (_fluentCommandLineParser.Object.Trace)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Trace);
            }

            LogManager.ReconfigExistingLoggers();

            if (_fluentCommandLineParser.Object.Vss & (IsAdministrator() == false))
            {
                _logger.Error("--vss is present, but administrator rights not found. Exiting\r\n");
                return;
            }

            var sw = new Stopwatch();
            sw.Start();

            var ts = DateTimeOffset.UtcNow;

            _errorFiles = new Dictionary<string, int>();

            if (_fluentCommandLineParser.Object.JsonDirectory.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(_fluentCommandLineParser.Object.JsonDirectory) == false)
                {
                    _logger.Warn(
                        $"Path to '{_fluentCommandLineParser.Object.JsonDirectory}' doesn't exist. Creating...");

                    try
                    {
                        Directory.CreateDirectory(_fluentCommandLineParser.Object.JsonDirectory);
                    }
                    catch (Exception)
                    {
                        _logger.Fatal(
                            $"Unable to create directory '{_fluentCommandLineParser.Object.JsonDirectory}'. Does a file with the same name exist? Exiting");
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.json";

                if (_fluentCommandLineParser.Object.JsonName.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(_fluentCommandLineParser.Object.JsonName);
                }

                var outFile = Path.Combine(_fluentCommandLineParser.Object.JsonDirectory, outName);

                _logger.Warn($"json output will be saved to '{outFile}'\r\n");
                
                try
                {
                    _swJson = new StreamWriter(outFile, false, Encoding.UTF8);
                }
                catch (Exception)
                {
                    _logger.Error($"Unable to open '{outFile}'! Is it in use? Exiting!\r\n");
                    Environment.Exit(0);
                }

                JsConfig.DateHandler = DateHandler.ISO8601;
            }

            if (_fluentCommandLineParser.Object.XmlDirectory.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(_fluentCommandLineParser.Object.XmlDirectory) == false)
                {
                    _logger.Warn(
                        $"Path to '{_fluentCommandLineParser.Object.XmlDirectory}' doesn't exist. Creating...");

                    try
                    {
                        Directory.CreateDirectory(_fluentCommandLineParser.Object.XmlDirectory);
                    }
                    catch (Exception)
                    {
                        _logger.Fatal(
                            $"Unable to create directory '{_fluentCommandLineParser.Object.XmlDirectory}'. Does a file with the same name exist? Exiting");
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.xml";

                if (_fluentCommandLineParser.Object.XmlName.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(_fluentCommandLineParser.Object.XmlName);
                }

                var outFile = Path.Combine(_fluentCommandLineParser.Object.XmlDirectory, outName);

                _logger.Warn($"XML output will be saved to '{outFile}'\r\n");

                try
                {
                    _swXml = new StreamWriter(outFile, false, Encoding.UTF8);
                }
                catch (Exception)
                {
                    _logger.Error($"Unable to open '{outFile}'! Is it in use? Exiting!\r\n");
                    Environment.Exit(0);
                }
            }

            if (_fluentCommandLineParser.Object.StartDate.IsNullOrEmpty() == false)
            {
                if (DateTimeOffset.TryParse(_fluentCommandLineParser.Object.StartDate, out var dt))
                {
                    _startDate = dt;
                    _logger.Info($"Setting Start date to '{_startDate.Value.ToUniversalTime().ToString(_fluentCommandLineParser.Object.DateTimeFormat)}'");
                }
                else
                {
                    _logger.Warn($"Count not parse '{_fluentCommandLineParser.Object.StartDate}' to a valud datetime! Events will not be filtered by Start date!");
                }
            }
            if (_fluentCommandLineParser.Object.EndDate.IsNullOrEmpty() == false)
            {
                if (DateTimeOffset.TryParse(_fluentCommandLineParser.Object.EndDate, out var dt))
                {
                    _endDate = dt;
                    _logger.Info($"Setting End date to '{_endDate.Value.ToUniversalTime().ToString(_fluentCommandLineParser.Object.DateTimeFormat)}'");
                }
                else
                {
                    _logger.Warn($"Count not parse '{_fluentCommandLineParser.Object.EndDate}' to a valud datetime! Events will not be filtered by End date!");
                }
            }

            if (_startDate.HasValue || _endDate.HasValue)
            {
                _logger.Info("");
            }


            if (_fluentCommandLineParser.Object.CsvDirectory.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(_fluentCommandLineParser.Object.CsvDirectory) == false)
                {
                    _logger.Warn(
                        $"Path to '{_fluentCommandLineParser.Object.CsvDirectory}' doesn't exist. Creating...");

                    try
                    {
                        Directory.CreateDirectory(_fluentCommandLineParser.Object.CsvDirectory);
                    }
                    catch (Exception)
                    {
                        _logger.Fatal(
                            $"Unable to create directory '{_fluentCommandLineParser.Object.CsvDirectory}'. Does a file with the same name exist? Exiting");
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.csv";

                if (_fluentCommandLineParser.Object.CsvName.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(_fluentCommandLineParser.Object.CsvName);
                }

                var outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                _logger.Warn($"CSV output will be saved to '{outFile}'\r\n");

                try
                {
                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv);
                }
                catch (Exception)
                {
                    _logger.Error($"Unable to open '{outFile}'! Is it in use? Exiting!\r\n");
                    Environment.Exit(0);
                }

                var foo = _csvWriter.Configuration.AutoMap<EventRecord>();

                if (_fluentCommandLineParser.Object.PayloadAsJson == false)
                {
                    foo.Map(t => t.Payload).Ignore();
                }
                else
                {
                    foo.Map(t => t.Payload).Index(22);
                }
                
                foo.Map(t => t.RecordPosition).Ignore();
                foo.Map(t => t.Size).Ignore();
                foo.Map(t => t.Timestamp).Ignore();

                foo.Map(t => t.RecordNumber).Index(0);
                foo.Map(t => t.EventRecordId).Index(1);
                foo.Map(t => t.TimeCreated).Index(2);
                foo.Map(t => t.TimeCreated).ConvertUsing(t =>
                    $"{t.TimeCreated.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}");
                foo.Map(t => t.EventId).Index(3);
                foo.Map(t => t.Level).Index(4);
                foo.Map(t => t.Provider).Index(5);
                foo.Map(t => t.Channel).Index(6);
                foo.Map(t => t.ProcessId).Index(7);
                foo.Map(t => t.ThreadId).Index(8);
                foo.Map(t => t.Computer).Index(9);
                foo.Map(t => t.UserId).Index(10);
                foo.Map(t => t.MapDescription).Index(11);
                foo.Map(t => t.UserName).Index(12);
                foo.Map(t => t.RemoteHost).Index(13);
                foo.Map(t => t.PayloadData1).Index(14);
                foo.Map(t => t.PayloadData2).Index(15);
                foo.Map(t => t.PayloadData3).Index(16);
                foo.Map(t => t.PayloadData4).Index(17);
                foo.Map(t => t.PayloadData5).Index(18);
                foo.Map(t => t.PayloadData6).Index(19);
                foo.Map(t => t.ExecutableInfo).Index(20);
                foo.Map(t => t.SourceFile).Index(21);

                _csvWriter.Configuration.RegisterClassMap(foo);
                _csvWriter.WriteHeader<EventRecord>();
                _csvWriter.NextRecord();
            }

            if (Directory.Exists(_fluentCommandLineParser.Object.MapsDirectory) == false)
            {
                _logger.Warn(
                    $"Maps directory '{_fluentCommandLineParser.Object.MapsDirectory}' does not exist! Event ID maps will not be loaded!!");
            }
            else
            {
                _logger.Debug($"Loading maps from '{Path.GetFullPath(_fluentCommandLineParser.Object.MapsDirectory)}'");
                var errors = EventLog.LoadMaps(Path.GetFullPath(_fluentCommandLineParser.Object.MapsDirectory));
                
                if (errors)
                {
                    return;
                }

                _logger.Info($"Maps loaded: {EventLog.EventLogMaps.Count:N0}");
            }

            _includeIds = new HashSet<int>();
            _excludeIds = new HashSet<int>();

            if (_fluentCommandLineParser.Object.ExcludeIds.IsNullOrEmpty() == false)
            {
                var excSegs = _fluentCommandLineParser.Object.ExcludeIds.Split(',');

                foreach (var incSeg in excSegs)
                {
                    if (int.TryParse(incSeg, out var goodId))
                    {
                        _excludeIds.Add(goodId);
                    }
                }
            }

            if (_fluentCommandLineParser.Object.IncludeIds.IsNullOrEmpty() == false)
            {
                _excludeIds.Clear();
                var incSegs = _fluentCommandLineParser.Object.IncludeIds.Split(',');

                foreach (var incSeg in incSegs)
                {
                    if (int.TryParse(incSeg, out var goodId))
                    {
                        _includeIds.Add(goodId);
                    }
                }
            }

            if (_fluentCommandLineParser.Object.Vss)
            {
                string driveLetter;
                if (_fluentCommandLineParser.Object.File.IsEmpty() == false)
                {
                    driveLetter = Path.GetPathRoot(Path.GetFullPath(_fluentCommandLineParser.Object.File))
                        .Substring(0, 1);
                }
                else
                {
                    driveLetter = Path.GetPathRoot(Path.GetFullPath(_fluentCommandLineParser.Object.Directory))
                        .Substring(0, 1);
                }


                Helper.MountVss(driveLetter,VssDir);
                Console.WriteLine();
            }


            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty() == false)
            {
                if (File.Exists(_fluentCommandLineParser.Object.File) == false)
                {
                    _logger.Warn($"'{_fluentCommandLineParser.Object.File}' does not exist! Exiting");
                    return;
                }

                _fluentCommandLineParser.Object.Dedupe = false;

                ProcessFile(_fluentCommandLineParser.Object.File);

                if (_fluentCommandLineParser.Object.Vss)
                {
                    var vssDirs = Directory.GetDirectories(VssDir);

                    var root = Path.GetPathRoot(Path.GetFullPath(_fluentCommandLineParser.Object.File));
                    var stem = Path.GetFullPath(_fluentCommandLineParser.Object.File).Replace(root, "");

                    foreach (var vssDir in vssDirs)
                    {
                        var newPath = Path.Combine(vssDir, stem);
                        if (File.Exists(newPath))
                        {
                            ProcessFile(newPath);
                        }
                    }
                }
            }
            else
            {
                _logger.Info($"Looking for event log files in '{_fluentCommandLineParser.Object.Directory}'");
                _logger.Info("");

                var f = new DirectoryEnumerationFilters();
                f.InclusionFilter = fsei => fsei.Extension.ToUpperInvariant() == ".EVTX";

                f.RecursionFilter = entryInfo => !entryInfo.IsMountPoint && !entryInfo.IsSymbolicLink;

                f.ErrorFilter = (errorCode, errorMessage, pathProcessed) => true;

                var dirEnumOptions =
                    DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive |
                    DirectoryEnumerationOptions.SkipReparsePoints | DirectoryEnumerationOptions.ContinueOnException |
                    DirectoryEnumerationOptions.BasicSearch;

                var files2 =
                    Directory.EnumerateFileSystemEntries(Path.GetFullPath(_fluentCommandLineParser.Object.Directory), dirEnumOptions, f);
                
                foreach (var file in files2)
                {
                    ProcessFile(file);
                }

                if (_fluentCommandLineParser.Object.Vss)
                {
                    var vssDirs = Directory.GetDirectories(VssDir);

                    Console.WriteLine();

                    foreach (var vssDir in vssDirs)
                    {
                        var root = Path.GetPathRoot(Path.GetFullPath(_fluentCommandLineParser.Object.Directory));
                        var stem = Path.GetFullPath(_fluentCommandLineParser.Object.Directory).Replace(root, "");

                        var target = Path.Combine(vssDir, stem);

                        _logger.Fatal($"\r\nSearching 'VSS{target.Replace($"{VssDir}\\","")}' for event logs...");

                        var vssFiles = Helper.GetFilesFromPath(target, true, "*.evtx");

                        foreach (var file in vssFiles)
                        {
                            ProcessFile(file);
                        }                        
                    }

                }
            }

            _swCsv?.Flush();
            _swCsv?.Close();

            _swJson?.Flush();
            _swJson?.Close();

            _swXml?.Flush();
            _swXml?.Close();

            sw.Stop();
            _logger.Info("");

            var suff = string.Empty;
            if (_fileCount != 1)
            {
                suff = "s";
            }

            _logger.Error(
                $"Processed {_fileCount:N0} file{suff} in {sw.Elapsed.TotalSeconds:N4} seconds\r\n");

            if (_errorFiles.Count > 0)
            {
                _logger.Info("");
                _logger.Error("Files with errors");
                foreach (var errorFile in _errorFiles)
                {
                    _logger.Info($"'{errorFile.Key}' error count: {errorFile.Value:N0}");
                }

                _logger.Info("");
            }

            if (_fluentCommandLineParser.Object.Vss)
            {
                if (Directory.Exists(VssDir))
                {
                    foreach (var directory in Directory.GetDirectories(VssDir))
                    {
                        Directory.Delete(directory);
                    }
                    Directory.Delete(VssDir,true,true);
                }
            }
        }

        private static HashSet<string> _seenHashes = new HashSet<string>();

        private static void ProcessFile(string file)
        {
            if (File.Exists(file) == false)
            {
                _logger.Warn($"'{file}' does not exist! Skipping");
                return;
            }

            if (file.StartsWith(VssDir))
            {
                _logger.Warn($"\r\nProcessing 'VSS{file.Replace($"{VssDir}\\", "")}'");
            }
            else
            {
                _logger.Warn($"\r\nProcessing '{file}'...");
            }

            Stream fileS;

            try
            {
                fileS = new FileStream(file, FileMode.Open, FileAccess.Read);
            }
            catch (Exception)
            {
                //file is in use

                if (Helper.IsAdministrator() == false)
                {
                    _logger.Fatal("\r\nAdministrator privileges not found! Exiting!!\r\n");
                    Environment.Exit(0);
                }

                _logger.Warn($"\r\n'{file}' is in use. Rerouting...");

                var files = new List<string>();
                files.Add(file);

                var rawFiles = Helper.GetFiles(files);
                fileS = rawFiles.First().FileStream;
            }

            try
            {
                if (_fluentCommandLineParser.Object.Dedupe)
                {
                    var sha = Helper.GetSha1FromStream(fileS,0);
                    if (_seenHashes.Contains(sha))
                    {
                        _logger.Debug($"Skipping '{file}' as a file with SHA-1 '{sha}' has already been processed");
                        return;
                    }

                    _seenHashes.Add(sha);
                }

                var fsw = new Stopwatch();
                fsw.Start();

                var evt = new EventLog(fileS);

                fsw.Stop();
                var seenRecords = 0;

                _logger.Info($"File processing completed in {fsw.Elapsed.TotalSeconds:N3} seconds. Iterating records...");
                

                foreach (var eventRecord in evt.GetEventRecords())
                {
                    eventRecord.BuildProperties();
                    if (_includeIds.Count > 0)
                    {
                        if (_includeIds.Contains(eventRecord.EventId) == false)
                        {
                            //it is NOT in the list, so skip
                            _droppedEvents += 1;
                            continue;
                        }
                    }
                    else if (_excludeIds.Count > 0)
                    {
                        if (_excludeIds.Contains(eventRecord.EventId))
                        {
                            //it IS in the list, so skip
                            _droppedEvents += 1;
                            continue;
                        }
                    }

                    if (_startDate.HasValue)
                    {
                        if (eventRecord.TimeCreated < _startDate.Value)
                        {
                            //too old
                            _logger.Debug($"Dropping record Id '{eventRecord.EventRecordId}' with timestamp '{eventRecord.TimeCreated.ToUniversalTime().ToString(_fluentCommandLineParser.Object.DateTimeFormat)}' as its too old.");
                            _droppedEvents += 1;
                            continue;
                        }
                    }

                    if (_endDate.HasValue)
                    {
                        if (eventRecord.TimeCreated > _endDate.Value)
                        {
                            //too new
                            _logger.Debug($"Dropping record Id '{eventRecord.EventRecordId}' with timestamp '{eventRecord.TimeCreated.ToUniversalTime().ToString(_fluentCommandLineParser.Object.DateTimeFormat)}' as its too new.");
                            _droppedEvents += 1;
                            continue;
                        }
                    }

                    if (file.StartsWith(VssDir))
                    {
                        eventRecord.SourceFile = $"VSS{file.Replace($"{VssDir}\\", "")}";
                    }
                    else
                    {
                        eventRecord.SourceFile = file;    
                    }
                    
                    try
                    {
                        if (_fluentCommandLineParser.Object.PayloadAsJson)
                        {
                            var xdo = new XmlDocument();
                            xdo.LoadXml(eventRecord.Payload);

                           var payOut = JsonConvert.SerializeXmlNode(xdo);
                           eventRecord.Payload = payOut;
                        }

                        _csvWriter?.WriteRecord(eventRecord);
                        _csvWriter?.NextRecord();

                        var xml = string.Empty;
                        if (_swXml != null)
                        {
                            xml = eventRecord.ConvertPayloadToXml();
                            _swXml.WriteLine(xml);
                        }

                        if (_swJson != null)
                        {
                            var jsOut = eventRecord.ToJson();
                            if (_fluentCommandLineParser.Object.FullJson)
                            {
                                if (xml.IsNullOrEmpty())
                                {
                                    xml = eventRecord.ConvertPayloadToXml();
                                }
                             
                                jsOut = GetPayloadAsJson(xml);
                            }

                            _swJson.WriteLine(jsOut);
                        }

                        seenRecords += 1;
                    }
                    catch (Exception e)
                    {
                        _logger.Error($"Error processing record #{eventRecord.RecordNumber}: {e.Message}");
                        evt.ErrorRecords.Add(21,e.Message);
                    }
                }

                if (evt.ErrorRecords.Count > 0)
                {
                    var fn = file;
                    if (file.StartsWith(VssDir))
                    {
                        fn=$"VSS{file.Replace($"{VssDir}\\", "")}";
                    }

                    _errorFiles.Add(fn, evt.ErrorRecords.Count);
                }

                _fileCount += 1;

                _logger.Info("");
                _logger.Fatal("Event log details");
                _logger.Info(evt);

                _logger.Info($"Records included: {seenRecords:N0} Errors: {evt.ErrorRecords.Count:N0} Events dropped: {_droppedEvents:N0}");

                if (evt.ErrorRecords.Count > 0)
                {
                    _logger.Warn("\r\nErrors");
                    foreach (var evtErrorRecord in evt.ErrorRecords)
                    {
                        _logger.Info($"Record #{evtErrorRecord.Key}: Error: {evtErrorRecord.Value}");
                    }
                }

                if (_fluentCommandLineParser.Object.Metrics && evt.EventIdMetrics.Count > 0)
                {
                    _logger.Fatal("\r\nMetrics (including dropped events)");
                    _logger.Warn("Event Id\tCount");
                    foreach (var esEventIdMetric in evt.EventIdMetrics.OrderBy(t => t.Key))
                    {
                        if (_includeIds.Count > 0)
                        {
                            if (_includeIds.Contains((int) esEventIdMetric.Key) == false)
                            {
                                //it is NOT in the list, so skip
                                continue;
                            }
                        }
                        else if (_excludeIds.Count > 0)
                        {
                            if (_excludeIds.Contains((int) esEventIdMetric.Key))
                            {
                                //it IS in the list, so skip
                                continue;
                            }
                        }

                        _logger.Info($"{esEventIdMetric.Key}\t\t{esEventIdMetric.Value:N0}");
                    }
                }
            }
            catch (Exception e)
            {
                var fn = file;
                if (file.StartsWith(VssDir))
                {
                    fn=$"VSS{file.Replace($"{VssDir}\\", "")}";
                }

                if (e.Message.Contains("Invalid signature! Expected 'ElfFile"))
                {
                    _logger.Info($"'{fn}' is not an evtx file! Message: {e.Message} Skipping...");
                }
                else
                {
                    _logger.Error($"Error processing '{fn}'! Message: {e.Message}");
                }
            }

            fileS?.Close();
        }

        public static string GetPayloadAsJson(string xmlPayload)
        {
            var xdo = new XmlDocument();
            xdo.LoadXml(xmlPayload);
            return JsonConvert.SerializeXmlNode(xdo);
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
        public string Directory { get; set; }
        public string CsvDirectory { get; set; }
        public string JsonDirectory { get; set; }
        public string XmlDirectory { get; set; }

        public string DateTimeFormat { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }

        public bool Debug { get; set; }
        public bool Trace { get; set; }

        public string CsvName { get; set; }
        public string JsonName { get; set; }
        public string XmlName { get; set; }
        public string MapsDirectory { get; set; }
        public string IncludeIds { get; set; }
        public string ExcludeIds { get; set; }

        public bool FullJson { get; set; }
        public bool PayloadAsJson { get; set; }
        public bool Metrics { get; set; }

        public bool Vss { get; set; }
        public bool Dedupe { get; set; }
    }
}