#if !NET6_0
using Alphaleonis.Win32.Filesystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
#else
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using File = System.IO.File;
#endif
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CsvHelper.Configuration;
using evtx;
using Exceptionless;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using RawCopy;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;


namespace EvtxECmd;

    internal class Program
    {

        private static string _activeDateTimeFormat;
        
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

        private static readonly string Header =
            $"EvtxECmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
            "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
            "\r\nhttps://github.com/EricZimmerman/evtx";

        private static readonly string Footer =
            @"Examples: EvtxECmd.exe -f ""C:\Temp\Application.evtx"" --csv ""c:\temp\out"" --csvf MyOutputFile.csv" +
            "\r\n\t " +
            @"   EvtxECmd.exe -f ""C:\Temp\Application.evtx"" --csv ""c:\temp\out""" + "\r\n\t " +
            @"   EvtxECmd.exe -f ""C:\Temp\Application.evtx"" --json ""c:\temp\jsonout""" + "\r\n\t " +
            "\r\n\t" +
            "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes";

        private static RootCommand _rootCommand;

        private static readonly HashSet<string> SeenHashes = new HashSet<string>();

        class DateTimeOffsetFormatter : IFormatProvider, ICustomFormatter
        {
            private readonly IFormatProvider _innerFormatProvider;

            public DateTimeOffsetFormatter(IFormatProvider innerFormatProvider)
            {
                _innerFormatProvider = innerFormatProvider;
            }

            public object GetFormat(Type formatType)
            {
                return formatType == typeof(ICustomFormatter) ? this : _innerFormatProvider.GetFormat(formatType);
            }

            public string Format(string format, object arg, IFormatProvider formatProvider)
            {
                if (arg is DateTimeOffset)
                {
                    var size = (DateTimeOffset)arg;
                    return size.ToString(_activeDateTimeFormat);
                }

                var formattable = arg as IFormattable;
                if (formattable != null)
                {
                    return formattable.ToString(format, _innerFormatProvider);
                }

                return arg.ToString();
            }
        }
        
        private static async Task Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("tYeWS6A5K5uItgpB44dnNy2qSb2xJxiQWRRGWebq");

            _rootCommand = new RootCommand
            {
                new Option<string>(
                    "-f",
                    "File to process. This or -d is required"),

                new Option<string>(
                    "-d",
                    "Directory to process that contains evtx files. This or -f is required"),

                new Option<string>(
                    "--csv",
                    "Directory to save CSV formatted results to"),

                new Option<string>(
                    "--csvf",
                    "File name to save CSV formatted results to. When present, overrides default name"),

                new Option<string>(
                    "--json",
                    "Directory to save JSON formatted results to"),

                new Option<string>(
                    "--jsonf",
                    "File name to save JSON formatted results to. When present, overrides default name"),

                new Option<string>(
                    "--xml",
                    "Directory to save XML formatted results to"),

                new Option<string>(
                    "--xmlf",
                    "File name to save XML formatted results to. When present, overrides default name"),

                new Option<string>(
                    "--dt",
                    getDefaultValue: () => "yyyy-MM-dd HH:mm:ss.fffffff",
                    "The custom date/time format to use when displaying time stamps"),

                new Option<string>(
                    "--inc",
                    "List of Event IDs to process. All others are ignored. Overrides --exc Format is 4624,4625,5410,5500-5600"),

                new Option<string>(
                    "--exc",
                    "List of Event IDs to IGNORE. All others are included. Format is 4624,4625,5410,5500-5600"),

                new Option<string>(
                    "--sd",
                    "Start date for including events (UTC). Anything OLDER than this is dropped. Format should match --dt"),

                new Option<string>(
                    "--ed",
                    "End date for including events (UTC). Anything NEWER than this is dropped. Format should match --dt"),

                new Option<bool>(
                    "--fj",
                    () => false,
                    "When true, export all available data when using --json"),

                new Option<int>(
                    "--tdt", () => 1,
                    "The number of seconds to use for time discrepancy detection"),

                new Option<bool>(
                    "--met",
                    () => true,
                    "When true, show metrics about processed event log"),

                new Option<string>(
                    "--maps",
                    () => Path.Combine(BaseDirectory, "Maps"),
                    "The path where event maps are located. Defaults to 'Maps' folder where program was executed"),

                new Option<bool>(
                    "--vss",
                    () => false,
                    "Process all Volume Shadow Copies that exist on drive specified by -f or -d"),

                new Option<bool>(
                    "--dedupe",
                    () => true,
                    "Deduplicate -f or -d & VSCs based on SHA-1. First file found wins"),

                new Option<bool>(
                    "--sync",
                    () => false,
                    "If true, the latest maps from https://github.com/EricZimmerman/evtx/tree/master/evtx/Maps are downloaded and local maps updated"),

                new Option<bool>(
                    "--debug",
                    () => false,
                    "Show debug information during processing"),

                new Option<bool>(
                    "--trace",
                    () => false,
                    "Show trace information during processing")
            };


            _rootCommand.Description = Header + "\r\n\r\n" + Footer;

            _rootCommand.Handler = CommandHandler.Create(DoWork);

            await _rootCommand.InvokeAsync(args);
        }

        private static HashSet<int> GetRange(string input)
        {
            var segs = input.Split('-');

            var sGood = int.TryParse(segs[0], out var s);
            var sEnd = int.TryParse(segs[1], out var e);

            if (sGood == false)
            {
                throw new FormatException($"{segs[0]} is not an integer");
            }
            
            if (sEnd == false)
            {
                throw new FormatException($"{segs[1]} is not an integer");
            }

            var r = new HashSet<int>();

            for (var i = s; i <= e; i++)
            {
                r.Add(i);
            }

            return r;
        }

        private static void DoWork(string f, string d, string csv, string csvf, string json, string jsonf, string xml, string xmlf, string dt, string inc, string exc, string sd, string ed, bool fj, int tdt, bool met, string maps, bool vss, bool dedupe, bool sync, bool debug, bool trace)
        {
            var levelSwitch = new LoggingLevelSwitch();

            _activeDateTimeFormat = dt;
        
            var formatter  =
                new DateTimeOffsetFormatter(CultureInfo.CurrentCulture);

            var template = "{Message:lj}{NewLine}{Exception}";

            if (debug)
            {
                levelSwitch.MinimumLevel = LogEventLevel.Debug;
                template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
            }

            if (trace)
            {
                levelSwitch.MinimumLevel = LogEventLevel.Verbose;
                template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
            }
        
            var conf = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: template,formatProvider: formatter)
                .MinimumLevel.ControlledBy(levelSwitch);
      
            Log.Logger = conf.CreateLogger();
            
            if (sync)
            {
                try
                {
                    Log.Information("{Header}",Header);
                    UpdateFromRepo();
                }
                catch (Exception e)
                {
                    Log.Error(e, "There was an error checking for updates: {Message}",e.Message);
                }

                Environment.Exit(0);
            }

            if (f.IsNullOrEmpty() &&
                d.IsNullOrEmpty())
            {
                var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
                var hc = new HelpContext(helpBld, _rootCommand, Console.Out);

                helpBld.Write(hc);

                Log.Warning("-f or -d is required. Exiting");
                Console.WriteLine();
                return;
            }

            Log.Information("{Header}",Header);
            Console.WriteLine();
            Log.Information("Command line: {Args}",string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));
            Console.WriteLine();

            if (IsAdministrator() == false)
            {
                Log.Warning("Warning: Administrator privileges not found!");
                Console.WriteLine();
            }
           
            if (vss & !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                vss = false;
                Log.Warning("{Vss} not supported on non-Windows platforms. Disabling...","--vss");
                Console.WriteLine();
            }

            if (vss & (IsAdministrator() == false))
            {
                Log.Error("{Vss} is present, but administrator rights not found. Exiting","--vss");
                Console.WriteLine();
                return;
            }

            var sw = new Stopwatch();
            sw.Start();

            var ts = DateTimeOffset.UtcNow;

            _errorFiles = new Dictionary<string, int>();

            if (json.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(json) == false)
                {
                    Log.Information("Path to {Json} doesn't exist. Creating...",json);

                    try
                    {
                        Directory.CreateDirectory(json);
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal(ex,
                            "Unable to create directory {Json}. Does a file with the same name exist? Exiting",json);
                        Console.WriteLine();
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.json";

                if (jsonf.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(jsonf);
                }

                var outFile = Path.Combine(json, outName);

                Log.Information("json output will be saved to {OutFile}",outFile);
                Console.WriteLine();

                try
                {
                    _swJson = new StreamWriter(outFile, false, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Log.Error(ex,"Unable to open {OutFile}! Is it in use? Exiting!",outFile);
                    Console.WriteLine();
                    Environment.Exit(0);
                }

                JsConfig.DateHandler = DateHandler.ISO8601;
            }

            if (xml.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(xml) == false)
                {
                    Log.Information("Path to {Xml} doesn't exist. Creating...",xml);

                    try
                    {
                        Directory.CreateDirectory(xml);
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal(ex,
                            "Unable to create directory {Xml}. Does a file with the same name exist? Exiting",xml);
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.xml";

                if (xmlf.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(xmlf);
                }

                var outFile = Path.Combine(xml, outName);

                Log.Information("XML output will be saved to {OutFile}",outFile);
                Console.WriteLine();

                try
                {
                    _swXml = new StreamWriter(outFile, false, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Log.Error(ex,"Unable to open {OutFile}! Is it in use? Exiting!",outFile);
                    Console.WriteLine();
                    Environment.Exit(0);
                }
            }

            if (sd.IsNullOrEmpty() == false)
            {
                if (DateTimeOffset.TryParse(sd, null, DateTimeStyles.AssumeUniversal, out var dateTimeOffset))
                {
                    _startDate = dateTimeOffset;
                    Log.Information("Setting Start date to {StartDate}",_startDate.Value);
                }
                else
                {
                   Log.Warning("Could not parse {Sd} to a valid datetime! Events will not be filtered by Start date!",sd);
                }
            }

            if (ed.IsNullOrEmpty() == false)
            {
                if (DateTimeOffset.TryParse(ed, null, DateTimeStyles.AssumeUniversal, out var dateTimeOffset))
                {
                    _endDate = dateTimeOffset;
                    Log.Information("Setting End date to {EndDate}",_endDate.Value);
                }
                else
                {
                    Log.Warning("Could not parse {Ed} to a valid datetime! Events will not be filtered by End date!",ed);
                }
            }

            if (_startDate.HasValue || _endDate.HasValue)
            {
                Console.WriteLine();
            }


            if (csv.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(csv) == false)
                {
                    Log.Information(
                        "Path to {Csv} doesn't exist. Creating...",csv);

                    try
                    {
                        Directory.CreateDirectory(csv);
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal(ex,
                            "Unable to create directory {Csv}. Does a file with the same name exist? Exiting",csv);
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.csv";

                if (csvf.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(csvf);
                }

                var outFile = Path.Combine(csv, outName);

                Log.Information("CSV output will be saved to {OutFile}",outFile);
                Console.WriteLine();

                try
                {
                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    var opt = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        ShouldUseConstructorParameters = _ => false
                    };

                    _csvWriter = new CsvWriter(_swCsv, opt);
                }
                catch (Exception ex)
                {
                    Log.Error(ex,"Unable to open {OutFile}! Is it in use? Exiting!",outFile);
                    Console.WriteLine();
                    Environment.Exit(0);
                }


                var foo = _csvWriter.Context.AutoMap<EventRecord>();

                foo.Map(t => t.RecordPosition).Ignore();
                foo.Map(t => t.Size).Ignore();
                foo.Map(t => t.Timestamp).Ignore();

                foo.Map(t => t.RecordNumber).Index(0);
                foo.Map(t => t.EventRecordId).Index(1);
                foo.Map(t => t.TimeCreated).Index(2);
                foo.Map(t => t.TimeCreated).Convert(t =>
                    $"{t.Value.TimeCreated.ToString(dt)}");
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
                foo.Map(t => t.HiddenRecord).Index(21);
                foo.Map(t => t.SourceFile).Index(22);
                foo.Map(t => t.Keywords).Index(23);
                foo.Map(t => t.Payload).Index(24);

                _csvWriter.Context.RegisterClassMap(foo);
                _csvWriter.WriteHeader<EventRecord>();
                _csvWriter.NextRecord();
            }

            if (Directory.Exists(maps) == false)
            {
                Log.Warning("Maps directory {Maps} does not exist! Event ID maps will not be loaded!!",maps);
            }
            else
            {
                Log.Debug("Loading maps from {Path}",Path.GetFullPath(maps));
                var errors = EventLog.LoadMaps(Path.GetFullPath(maps));

                if (errors)
                {
                    return;
                }

                Log.Information("Maps loaded: {Count:N0}",EventLog.EventLogMaps.Count);
            }

            _includeIds = new HashSet<int>();
            _excludeIds = new HashSet<int>();

            if (exc.IsNullOrEmpty() == false)
            {
                var excSegs = exc.Split(',');
                
                foreach (var incSeg in excSegs)
                {
                    if (incSeg.Contains('-'))
                    {
                        var incRange = GetRange(incSeg);
                        foreach (var ir in incRange)
                        {
                            _excludeIds.Add(ir);
                        }
                        continue;
                    }
                        
                    if (int.TryParse(incSeg, out var goodId))
                    {
                        _excludeIds.Add(goodId);
                    }
                }
            }

            if (inc.IsNullOrEmpty() == false)
            {
                _excludeIds.Clear();
                var incSegs = inc.Split(',');

                foreach (var incSeg in incSegs)
                {
                    if (incSeg.Contains('-'))
                    {
                        var incRange = GetRange(incSeg);
                        foreach (var ir in incRange)
                        {
                            _includeIds.Add(ir);
                        }
                        continue;
                    }
                    
                    if (int.TryParse(incSeg, out var goodId))
                    {
                        _includeIds.Add(goodId);
                    }
                }
            }

            if (vss)
            {
                string driveLetter;
                if (f.IsEmpty() == false)
                {
                    driveLetter = Path.GetPathRoot(Path.GetFullPath(f))
                        .Substring(0, 1);
                }
                else
                {
                    driveLetter = Path.GetPathRoot(Path.GetFullPath(d))
                        .Substring(0, 1);
                }


                Helper.MountVss(driveLetter, VssDir);
                Console.WriteLine();
            }

            EventLog.TimeDiscrepancyThreshold = tdt;

            if (f.IsNullOrEmpty() == false)
            {
                if (File.Exists(f) == false)
                {
                    Log.Warning("\t{F} does not exist! Exiting",f);
                    Console.WriteLine();
                    return;
                }

                if (_swXml == null && _swJson == null && _swCsv == null)
                {
                    //no need for maps
                    Log.Debug("Clearing map collection since no output specified");
                    EventLog.EventLogMaps.Clear();
                }

                dedupe = false;

                ProcessFile(Path.GetFullPath(f), dedupe,  fj, met);

                if (vss)
                {
                    var vssDirs = Directory.GetDirectories(VssDir);

                    var root = Path.GetPathRoot(Path.GetFullPath(f));
                    var stem = Path.GetFullPath(f).Replace(root, "");

                    foreach (var vssDir in vssDirs)
                    {
                        var newPath = Path.Combine(vssDir, stem);
                        if (File.Exists(newPath))
                        {
                            ProcessFile(newPath, dedupe,  fj, met);
                        }
                    }
                }
            }
            else
            {
                if (Directory.Exists(d) == false)
                {
                    Log.Warning("\t{D} does not exist! Exiting",d);
                    Console.WriteLine();
                    return;
                }

                Log.Information("Looking for event log files in {D}",d);
                Console.WriteLine();

#if !NET6_0

                var directoryEnumerationFilters = new DirectoryEnumerationFilters
                {
                    InclusionFilter = fsei => fsei.Extension.ToUpperInvariant() == ".EVTX",
                    RecursionFilter = entryInfo => !entryInfo.IsMountPoint && !entryInfo.IsSymbolicLink,
                    ErrorFilter = (errorCode, errorMessage, pathProcessed) => true
                };

                var dirEnumOptions =
                    DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive |
                    DirectoryEnumerationOptions.SkipReparsePoints | DirectoryEnumerationOptions.ContinueOnException |
                    DirectoryEnumerationOptions.BasicSearch;

                var files2 =
                    Directory.EnumerateFileSystemEntries(Path.GetFullPath(d), dirEnumOptions, directoryEnumerationFilters);

#else
                var enumerationOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                    RecurseSubdirectories = true,
                    AttributesToSkip = 0
                };
                        
               var files2 =
                    Directory.EnumerateFileSystemEntries(d, "*.evtx",enumerationOptions);
#endif

                if (_swXml == null && _swJson == null && _swCsv == null)
                {
                    //no need for maps
                    Log.Debug("Clearing map collection since no output specified");
                    EventLog.EventLogMaps.Clear();
                }

                foreach (var file in files2)
                {
                    ProcessFile(file, dedupe,  fj, met);
                }

                if (vss)
                {
                    var vssDirs = Directory.GetDirectories(VssDir);

                    Console.WriteLine();

                    foreach (var vssDir in vssDirs)
                    {
                        var root = Path.GetPathRoot(Path.GetFullPath(d));
                        var stem = Path.GetFullPath(d).Replace(root, "");

                        var target = Path.Combine(vssDir, stem);

                        Console.WriteLine();
                        Log.Information("Searching {Vss} for event logs...",$"VSS{target.Replace($"{VssDir}\\", "")}");

                        var vssFiles = Helper.GetFilesFromPath(target, "*.evtx", true);

                        foreach (var file in vssFiles)
                        {
                            ProcessFile(file, dedupe,  fj, met);
                        }
                    }
                }
            }

            try
            {
                _swCsv?.Flush();
                _swCsv?.Close();

                _swJson?.Flush();
                _swJson?.Close();

                _swXml?.Flush();
                _swXml?.Close();
            }
            catch (Exception e)
            {
                Log.Error(e,"Error when flushing output files to disk! Error message: {Message}",e.Message);
            }

            sw.Stop();
            Console.WriteLine();

            if (_fileCount == 1)
            {
                Log.Information("Processed {FileCount:N0} file in {TotalSeconds:N4} seconds",_fileCount,sw.Elapsed.TotalSeconds);    
            }
            else
            {
                Log.Information("Processed {FileCount:N0} files in {TotalSeconds:N4} seconds",_fileCount,sw.Elapsed.TotalSeconds);
            }
            
            Console.WriteLine();

            if (_errorFiles.Count > 0)
            {
                Console.WriteLine();
                Log.Information("Files with errors");
                foreach (var errorFile in _errorFiles)
                {
                    Log.Information("{Key} error count: {Value:N0}",errorFile.Key,errorFile.Value);
                }

                Console.WriteLine();
            }

            if (vss)
            {
                if (Directory.Exists(VssDir))
                {
                    foreach (var directory in Directory.GetDirectories(VssDir))
                    {
                        Directory.Delete(directory);
                    }

#if !NET6_0
                    Directory.Delete(VssDir, true, true);
#else
                        Directory.Delete(VssDir,true);
#endif
                }
            }
        }

        private static void UpdateFromRepo()
        {
            Console.WriteLine();

            Log.Information("Checking for updated maps at {Url}...","https://github.com/EricZimmerman/evtx/tree/master/evtx/Maps");
            Console.WriteLine();
            var archivePath = Path.Combine(BaseDirectory, "____master.zip");

            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
            // using (var client = new WebClient())
            // {
            //     ServicePointManager.Expect100Continue = true;
            //     ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            //     client.DownloadFile("https://github.com/EricZimmerman/evtx/archive/master.zip", archivePath);
            // }
            
            "https://github.com/EricZimmerman/evtx/archive/master.zip".DownloadFileTo(archivePath);
 
            var fff = new FastZip();

            if (Directory.Exists(Path.Combine(BaseDirectory, "Maps")) == false)
            {
                Directory.CreateDirectory(Path.Combine(BaseDirectory, "Maps"));
            }

            var directoryFilter = "Maps";

            // Will prompt to overwrite if target file names already exist
            fff.ExtractZip(archivePath, BaseDirectory, FastZip.Overwrite.Always, null,
                null, directoryFilter, true);

            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            var newMapPath = Path.Combine(BaseDirectory, "evtx-master", "evtx", "Maps");

            var orgMapPath = Path.Combine(BaseDirectory, "Maps");

            var newMaps = Directory.GetFiles(newMapPath);

            var newLocalMaps = new List<string>();

            var updatedLocalMaps = new List<string>();

            if (File.Exists(Path.Combine(orgMapPath, "Security_4624.map")))
            {
                Log.Warning("Old map format found. Zipping to {Old} and cleaning up old maps","!!oldMaps.zip");
                //old maps found, so zip em first
                var oldZip = Path.Combine(orgMapPath, "!!oldMaps.zip");
                fff.CreateZip(oldZip, orgMapPath, false, @"\.map$");
                foreach (var m in Directory.GetFiles(orgMapPath, "*.map"))
                {
                    File.Delete(m);
                }
            }

            if (File.Exists(Path.Combine(orgMapPath, "!!!!README.txt")))
            {
                File.Delete(Path.Combine(orgMapPath, "!!!!README.txt"));
            }

            foreach (var newMap in newMaps)
            {
                var mName = Path.GetFileName(newMap);
                var dest = Path.Combine(orgMapPath, mName);

                if (File.Exists(dest) == false)
                {
                    //new target
                    newLocalMaps.Add(mName);
                }
                else
                {
                    //current destination file exists, so compare to new

                    var s1 = new StreamReader(newMap);
                    var newSha = Helper.GetSha1FromStream(s1.BaseStream, 0);

                    var s2 = new StreamReader(dest);
                    
                    var destSha = Helper.GetSha1FromStream(s2.BaseStream, 0);
                    
                    s2.Close();
                    s1.Close();
                
                    if (newSha != destSha)
                    {
                        //updated file
                        updatedLocalMaps.Add(mName);
                    }    
                    
                }

#if !NET6_0
                File.Copy(newMap, dest, CopyOptions.None);
#else
                    File.Copy(newMap,dest,true);
#endif
            }

            if (newLocalMaps.Count > 0 || updatedLocalMaps.Count > 0)
            {
                Log.Information("Updates found!");
                Console.WriteLine();

                if (newLocalMaps.Count > 0)
                {
                    Log.Information("New maps");
                    foreach (var newLocalMap in newLocalMaps)
                    {
                        Log.Information("{File}",Path.GetFileNameWithoutExtension(newLocalMap));
                    }

                    Console.WriteLine();
                }

                if (updatedLocalMaps.Count > 0)
                {
                    Log.Information("Updated maps");
                    foreach (var um in updatedLocalMaps)
                    {
                        Log.Information("{File}",Path.GetFileNameWithoutExtension(um));
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                Log.Information("No new maps available");
                Console.WriteLine();
            }

            Directory.Delete(Path.Combine(BaseDirectory, "evtx-master"), true);
        }

        private static void ProcessFile(string file, bool dedupe,  bool fj, bool met)
        {
            if (File.Exists(file) == false)
            {
                Log.Warning("{File} does not exist! Skipping",file);
                return;
            }

            if (file.StartsWith(VssDir))
            {
                Console.WriteLine();
                Log.Information("Processing {Vss}",$"VSS{file.Replace($"{VssDir}\\", "")}");
            }
            else
            {
                Console.WriteLine();
                Log.Information("Processing {File}...",file);
            }

            Stream fileS;

            try
            {
                fileS = new FileStream(file, FileMode.Open, FileAccess.Read);
            }
            catch (Exception)
            {
                //file is in use

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.WriteLine();
                    Log.Fatal("Raw disk reads not supported on non-Windows platforms! Exiting!!");
                    Console.WriteLine();
                    Environment.Exit(0);
                }

                if (Helper.IsAdministrator() == false)
                {
                    Console.WriteLine();
                    Log.Fatal("Administrator privileges not found! Exiting!!");
                    Console.WriteLine();
                    Environment.Exit(0);
                }

                if (file.StartsWith("\\\\"))
                {
                    Log.Fatal($"Raw access across UNC shares is not supported! Run locally on the system or extract logs via other means and try again. Exiting");
                    Environment.Exit(0);
                }

                Console.WriteLine();
                Log.Warning("{File} is in use. Rerouting...",file);

                var files = new List<string>
                {
                    file
                };

                var rawFiles = Helper.GetRawFiles(files);
                fileS = rawFiles.First().FileStream;
            }

            try
            {
                if (dedupe)
                {
                    var sha = Helper.GetSha1FromStream(fileS, 0);
                    fileS.Seek(0, SeekOrigin.Begin);
                    if (SeenHashes.Contains(sha))
                    {
                        Log.Debug("Skipping {File} as a file with SHA-1 {Sha} has already been processed",file,sha);
                        return;
                    }

                    SeenHashes.Add(sha);
                }

                EventLog.LastSeenTicks = 0;
                var evt = new EventLog(fileS);

                Log.Information("Chunk count: {ChunkCount:N0}, Iterating records...",evt.ChunkCount);

                var seenRecords = 0;

                var fsw = new Stopwatch();
                fsw.Start();

                foreach (var eventRecord in evt.GetEventRecords())
                {
                    if (seenRecords % 10 == 0)
                    {
                        Console.Title = $"Processing chunk {eventRecord.ChunkNumber:N0} of {evt.ChunkCount} % complete: {(eventRecord.ChunkNumber / (double)evt.ChunkCount):P} Records found: {seenRecords:N0}";
                    }

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
                            Log.Debug("Dropping record Id {EventRecordId} with timestamp {TimeCreated} as its too old",eventRecord.EventRecordId,eventRecord.TimeCreated);
                            _droppedEvents += 1;
                            continue;
                        }
                    }

                    if (_endDate.HasValue)
                    {
                        if (eventRecord.TimeCreated > _endDate.Value)
                        {
                            //too new
                            Log.Debug("Dropping record Id {EventRecordId} with timestamp {TimeCreated} as its too new",eventRecord.EventRecordId,eventRecord.TimeCreated);
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
                        var xdo = new XmlDocument();
                        xdo.LoadXml(eventRecord.Payload);

                        var payOut = JsonConvert.SerializeXmlNode(xdo);
                        eventRecord.Payload = payOut;

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
                            if (fj)
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
                        Log.Error("Error processing record #{RecordNumber}: {Message}",eventRecord.RecordNumber,e.Message);
                        evt.ErrorRecords.Add(21, e.Message);
                    }
                }

                fsw.Stop();

                if (evt.ErrorRecords.Count > 0)
                {
                    var fn = file;
                    if (file.StartsWith(VssDir))
                    {
                        fn = $"VSS{file.Replace($"{VssDir}\\", "")}";
                    }

                    _errorFiles.Add(fn, evt.ErrorRecords.Count);
                }

                _fileCount += 1;

                Console.WriteLine();
                Log.Information("Event log details");
                Log.Information("{Evt}",evt);

                Log.Information("Records included: {SeenRecords:N0} Errors: {ErrorRecordsCount:N0} Events dropped: {DroppedEvents:N0}",seenRecords,evt.ErrorRecords.Count,_droppedEvents);

                if (evt.ErrorRecords.Count > 0)
                {
                    Console.WriteLine();
                    Log.Information("Errors");
                    foreach (var evtErrorRecord in evt.ErrorRecords)
                    {
                        Log.Information("Record #{Key}: Error: {Value}",evtErrorRecord.Key,evtErrorRecord.Value);
                    }
                }

                if (met && evt.EventIdMetrics.Count > 0)
                {
                    Console.WriteLine();
                    Log.Information("Metrics (including dropped events)");
                    Log.Information("Event ID\tCount");
                    foreach (var esEventIdMetric in evt.EventIdMetrics.OrderBy(t => t.Key))
                    {
                        if (_includeIds.Count > 0)
                        {
                            if (_includeIds.Contains((int)esEventIdMetric.Key) == false)
                            {
                                //it is NOT in the list, so skip
                                continue;
                            }
                        }
                        else if (_excludeIds.Count > 0)
                        {
                            if (_excludeIds.Contains((int)esEventIdMetric.Key))
                            {
                                //it IS in the list, so skip
                                continue;
                            }
                        }

                        Log.Information("{Key}\t\t{Value:N0}",esEventIdMetric.Key,esEventIdMetric.Value);
                    }
                }
            }
            catch (Exception e)
            {
                var fn = file;
                if (file.StartsWith(VssDir))
                {
                    fn = $"VSS{file.Replace($"{VssDir}\\", "")}";
                }

                if (e.Message.Contains("Invalid signature! Expected 'ElfFile"))
                {
                    Log.Information("{Fn} is not an evtx file! Message: {Message} Skipping...",fn,e.Message);
                }
                else
                {
                    Log.Error("Error processing {Fn}! Message: {Message}",fn,e.Message);
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

        private static bool IsAdministrator()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }

            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

       
    }
