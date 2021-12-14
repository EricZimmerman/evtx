using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Alphaleonis.Win32.Security;
using CsvHelper.Configuration;
using evtx;
using Exceptionless;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using RawCopy;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;
using EventLog = evtx.EventLog;

#if !NET6_0
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
#else
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using File = System.IO.File;
#endif

namespace EvtxECmd
{
    internal class Program
    {
        private static Logger _logger;

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
            @" EvtxECmd.exe -f ""C:\Temp\Application.evtx"" --csv ""c:\temp\out""" + "\r\n\t " +
            @" EvtxECmd.exe -f ""C:\Temp\Application.evtx"" --json ""c:\temp\jsonout""" + "\r\n\t " +
            "\r\n\t" +
            "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes";

        private static RootCommand _rootCommand;
        
        private static async Task Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("tYeWS6A5K5uItgpB44dnNy2qSb2xJxiQWRRGWebq");

            SetupNLog();

            _logger = LogManager.GetLogger("EvtxECmd");

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
                    getDefaultValue:()=>"yyyy-MM-dd HH:mm:ss.fffffff",
                    "The custom date/time format to use when displaying time stamps"),

                new Option<string>(
                    "--inc",
                    "List of Event IDs to process. All others are ignored. Overrides --exc Format is 4624,4625,5410"),

                new Option<string>(
                    "--exc",
                    "List of Event IDs to IGNORE. All others are included. Format is 4624,4625,5410"),

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
                    "-tdt",getDefaultValue:()=>1,
                    "The number of seconds to use for time discrepancy detection"),
                
                new Option<bool>(
                    "--met",
                    () => false,
                    "When true, show metrics about processed event log"),
                
                new Option<string>(
                    "--maps",
                    "The path where event maps are located. Defaults to 'Maps' folder where program was executed"),

                new Option<bool>(
                    "--vss",
                    () => false,
                    "Process all Volume Shadow Copies that exist on drive specified by -f or -d"),
                
                new Option<bool>(
                    "--dedupe",
                    () => false,
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

        private static void DoWork(string f, string d, string csv, string csvf, string json, string jsonf, string xml, string xmlf, string dt, string inc, string exc, string sd, string ed, bool fj, int tdt,bool met, string maps, bool vss, bool dedupe, bool sync, bool debug, bool trace)
        {
            if (sync)
            {
                try
                {
                    _logger.Info(Header);
                    UpdateFromRepo();
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"There was an error checking for updates: {e.Message}");
                }

                Environment.Exit(0);
            }

            if (f.IsNullOrEmpty() &&
                d.IsNullOrEmpty())
            {
                var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
                var hc = new HelpContext(helpBld, _rootCommand, Console.Out);

                helpBld.Write(hc);

                _logger.Warn("-f or -d is required. Exiting");
                return;
            }

            _logger.Info(Header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

           

            if (IsAdministrator() == false)
            {
                _logger.Fatal("Warning: Administrator privileges not found!\r\n");
            }

            if (debug)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Debug);
            }

            if (trace)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Trace);
            }

            LogManager.ReconfigExistingLoggers();
            
            if (vss & !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                vss = false;
                _logger.Warn($"--vss not supported on non-Windows platforms. Disabling...");
            }

            if (vss & (IsAdministrator() == false))
            {
                _logger.Error("--vss is present, but administrator rights not found. Exiting\r\n");
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
                    _logger.Warn(
                        $"Path to '{json}' doesn't exist. Creating...");

                    try
                    {
                        Directory.CreateDirectory(json);
                    }
                    catch (Exception)
                    {
                        _logger.Fatal(
                            $"Unable to create directory '{json}'. Does a file with the same name exist? Exiting");
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.json";

                if (jsonf.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(jsonf);
                }

                var outFile = Path.Combine(json, outName);

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

            if (xml.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(xml) == false)
                {
                    _logger.Warn(
                        $"Path to '{xml}' doesn't exist. Creating...");

                    try
                    {
                        Directory.CreateDirectory(xml);
                    }
                    catch (Exception)
                    {
                        _logger.Fatal(
                            $"Unable to create directory '{xml}'. Does a file with the same name exist? Exiting");
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.xml";

                if (xmlf.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(xmlf);
                }

                var outFile = Path.Combine(xml, outName);

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

            if (sd.IsNullOrEmpty() == false)
            {
                if (DateTimeOffset.TryParse(sd,null,DateTimeStyles.AssumeUniversal, out var dateTimeOffset))
                {
                    _startDate = dateTimeOffset;
                    _logger.Info($"Setting Start date to '{_startDate.Value.ToUniversalTime().ToString(dt)}'");
                }
                else
                {
                    _logger.Warn($"Could not parse '{sd}' to a valud datetime! Events will not be filtered by Start date!");
                }
            }
            if (ed.IsNullOrEmpty() == false)
            {
                if (DateTimeOffset.TryParse(ed,null,DateTimeStyles.AssumeUniversal, out var dateTimeOffset))
                {
                    _endDate = dateTimeOffset;
                    _logger.Info($"Setting End date to '{_endDate.Value.ToUniversalTime().ToString(dt)}'");
                }
                else
                {
                    _logger.Warn($"Could not parse '{ed}' to a valud datetime! Events will not be filtered by End date!");
                }
            }

            if (_startDate.HasValue || _endDate.HasValue)
            {
                _logger.Info("");
            }


            if (csv.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(csv) == false)
                {
                    _logger.Warn(
                        $"Path to '{csv}' doesn't exist. Creating...");

                    try
                    {
                        Directory.CreateDirectory(csv);
                    }
                    catch (Exception)
                    {
                        _logger.Fatal(
                            $"Unable to create directory '{csv}'. Does a file with the same name exist? Exiting");
                        return;
                    }
                }

                var outName = $"{ts:yyyyMMddHHmmss}_EvtxECmd_Output.csv";

                if (csvf.IsNullOrEmpty() == false)
                {
                    outName = Path.GetFileName(csvf);
                }

                var outFile = Path.Combine(csv, outName);

                _logger.Warn($"CSV output will be saved to '{outFile}'\r\n");

                try
                {
                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    var opt = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                    ShouldUseConstructorParameters = _ => false
                    };

                    _csvWriter = new CsvWriter(_swCsv,opt);
                }
                catch (Exception)
                {
                    _logger.Error($"Unable to open '{outFile}'! Is it in use? Exiting!\r\n");
                    Environment.Exit(0);
                }



                var foo = _csvWriter.Context.AutoMap<EventRecord>();

                // if (_fluentCommandLineParser.Object.PayloadAsJson == false)
                // {
                //     foo.Map(t => t.Payload).Ignore();
                // }
                // else
                // {
                //   
                // }
                
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
                _logger.Warn(
                    $"Maps directory '{maps}' does not exist! Event ID maps will not be loaded!!");
            }
            else
            {
                _logger.Debug($"Loading maps from '{Path.GetFullPath(maps)}'");
                var errors = EventLog.LoadMaps(Path.GetFullPath(maps));
                
                if (errors)
                {
                    return;
                }

                _logger.Info($"Maps loaded: {EventLog.EventLogMaps.Count:N0}");
            }

            _includeIds = new HashSet<int>();
            _excludeIds = new HashSet<int>();

            if (exc.IsNullOrEmpty() == false)
            {
                var excSegs = exc.Split(',');

                foreach (var incSeg in excSegs)
                {
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


                Helper.MountVss(driveLetter,VssDir);
                Console.WriteLine();
            }

            EventLog.TimeDiscrepancyThreshold = tdt;

            if (f.IsNullOrEmpty() == false)
            {
                if (File.Exists(f) == false)
                {
                    _logger.Warn($"\t'{f}' does not exist! Exiting");
                    return;
                }

                if (_swXml == null && _swJson == null && _swCsv == null)
                {
                    //no need for maps
                    _logger.Debug("Clearing map collection since no output specified");
                    EventLog.EventLogMaps.Clear();
                }

                dedupe = false;

                ProcessFile(Path.GetFullPath(f),dedupe,dt,fj,met);

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
                            ProcessFile(newPath,dedupe,dt,fj,met);
                        }
                    }
                }
            }
            else
            {
                if (Directory.Exists(d) == false)
                {
                    _logger.Warn($"\t'{d}' does not exist! Exiting");
                    return;
                }

                _logger.Info($"Looking for event log files in '{d}'");
                _logger.Info("");

#if !NET6_0
                
                var directoryEnumerationFilters = new Alphaleonis.Win32.Filesystem.DirectoryEnumerationFilters
                {
                    InclusionFilter = fsei => fsei.Extension.ToUpperInvariant() == ".EVTX",
                    RecursionFilter = entryInfo => !entryInfo.IsMountPoint && !entryInfo.IsSymbolicLink,
                    ErrorFilter = (errorCode, errorMessage, pathProcessed) => true
                };

                var dirEnumOptions =
                    Alphaleonis.Win32.Filesystem.DirectoryEnumerationOptions.Files | Alphaleonis.Win32.Filesystem.DirectoryEnumerationOptions.Recursive |
                    Alphaleonis.Win32.Filesystem.DirectoryEnumerationOptions.SkipReparsePoints | Alphaleonis.Win32.Filesystem.DirectoryEnumerationOptions.ContinueOnException |
                    Alphaleonis.Win32.Filesystem.DirectoryEnumerationOptions.BasicSearch;

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
                    _logger.Debug("Clearing map collection since no output specified");
                    EventLog.EventLogMaps.Clear();
                }
                
                foreach (var file in files2)
                {
                    ProcessFile(file,dedupe,dt,fj,met);
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

                        _logger.Fatal($"\r\nSearching 'VSS{target.Replace($"{VssDir}\\","")}' for event logs...");

                        var vssFiles = Helper.GetFilesFromPath(target, true, "*.evtx");

                        foreach (var file in vssFiles)
                        {
                            ProcessFile(file,dedupe,dt,fj,met);
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
                _logger.Error($"Error when flushing output files to disk! Error message: {e.Message}");
            }
            

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

            if (vss)
            {
                if (Directory.Exists(VssDir))
                {
                    foreach (var directory in Directory.GetDirectories(VssDir))
                    {
                        Directory.Delete(directory);
                    }
                    
                    #if !NET6_0
                        Directory.Delete(VssDir,true,true);
                    #else
                        Directory.Delete(VssDir,true);
                    #endif
                    
                    
                    
                }
            }
        }
        
        private static readonly HashSet<string> _seenHashes = new HashSet<string>();

        private static async void UpdateFromRepo()
        {
            Console.WriteLine();

            _logger.Info(
                "Checking for updated maps at https://github.com/EricZimmerman/evtx/tree/master/evtx/Maps...");
            Console.WriteLine();
            var archivePath = Path.Combine(BaseDirectory, "____master.zip");

            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            using (var client = new HttpClient())
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
               var foo =  await client.GetStreamAsync(new Uri("https://github.com/EricZimmerman/evtx/archive/master.zip"));
               
               File.WriteAllBytes(archivePath,await foo.ReadFullyAsync());
            }

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

            var newMapPath = Path.Combine(BaseDirectory, "evtx-master","evtx", "Maps");

            var orgMapPath = Path.Combine(BaseDirectory, "Maps");

            var newMaps = Directory.GetFiles(newMapPath);

            var newLocalMaps = new List<string>();

            var updatedLocalMaps = new List<string>();

            if (File.Exists(Path.Combine(orgMapPath, "Security_4624.map")))
            {
                _logger.Warn($"Old map format found. Zipping to '!!oldMaps.zip' and cleaning up old maps");
                //old maps found, so zip em first
                var oldZip = Path.Combine(orgMapPath, "!!oldMaps.zip");
                fff.CreateZip(oldZip,orgMapPath,false,@"\.map$");
                foreach (var m in Directory.GetFiles(orgMapPath,"*.map"))
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
                    var fiNew = new Alphaleonis.Win32.Filesystem.FileInfo(newMap);
                    var fi = new Alphaleonis.Win32.Filesystem.FileInfo(dest);

                    if (fiNew.GetHash(HashType.SHA1) != fi.GetHash(HashType.SHA1))
                    {
                        //updated file
                        updatedLocalMaps.Add(mName);
                    }
                }

                #if !NET6_0
                    File.Copy(newMap, dest, Alphaleonis.Win32.Filesystem.CopyOptions.None);
                #else
                    File.Copy(newMap,dest,true);
                #endif
                
            }

            if (newLocalMaps.Count > 0 || updatedLocalMaps.Count > 0)
            {
                _logger.Fatal("Updates found!");
                Console.WriteLine();

                if (newLocalMaps.Count > 0)
                {
                    _logger.Error("New maps");
                    foreach (var newLocalMap in newLocalMaps)
                    {
                        _logger.Info(Path.GetFileNameWithoutExtension(newLocalMap));
                    }

                    Console.WriteLine();
                }

                if (updatedLocalMaps.Count > 0)
                {
                    _logger.Error("Updated maps");
                    foreach (var um in updatedLocalMaps)
                    {
                        _logger.Info(Path.GetFileNameWithoutExtension(um));
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                _logger.Info("No new maps available");
                Console.WriteLine();
            }

            Directory.Delete(Path.Combine(BaseDirectory, "evtx-master"), true);
        }

        private static void ProcessFile(string file, bool dedupe, string dt, bool fj, bool met)
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

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logger.Fatal("\r\nRaw disk reads not supported on non-Windows platformsExiting!!\r\n");
                    Environment.Exit(0);
                }
                
                if (Helper.IsAdministrator() == false)
                {
                    _logger.Fatal("\r\nAdministrator privileges not found! Exiting!!\r\n");
                    Environment.Exit(0);
                }

                if (file.StartsWith("\\\\"))
                {
                    _logger.Fatal($"Raw access across UNC shares is not supported! Run locally on the system or extract logs via other means and try again. Exiting");
                    Environment.Exit(0);
                }

                _logger.Warn($"\r\n'{file}' is in use. Rerouting...");

                var files = new List<string>
                {
                    file
                };

                var rawFiles = Helper.GetFiles(files);
                fileS = rawFiles.First().FileStream;
            }

            try
            {
                if (dedupe)
                {
                    var sha = Helper.GetSha1FromStream(fileS,0);
                    fileS.Seek(0, SeekOrigin.Begin);
                    if (_seenHashes.Contains(sha))
                    {
                        _logger.Debug($"Skipping '{file}' as a file with SHA-1 '{sha}' has already been processed");
                        return;
                    }

                    _seenHashes.Add(sha);
                }

                EventLog.LastSeenTicks = 0;
                var evt = new EventLog(fileS);

                _logger.Info($"Chunk count: {evt.ChunkCount:N0}, Iterating records...");

                var seenRecords = 0;

                var fsw = new Stopwatch();
                fsw.Start();

                foreach (var eventRecord in evt.GetEventRecords())
                {
           

                    if (seenRecords % 10 == 0)
                    {
                        Console.Title = $"Processing chunk {eventRecord.ChunkNumber:N0} of {evt.ChunkCount} % complete: {(eventRecord.ChunkNumber/(double)evt.ChunkCount):P} Records found: {seenRecords:N0}";
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
                            _logger.Debug($"Dropping record Id '{eventRecord.EventRecordId}' with timestamp '{eventRecord.TimeCreated.ToUniversalTime().ToString(dt)}' as its too old.");
                            _droppedEvents += 1;
                            continue;
                        }
                    }

                    if (_endDate.HasValue)
                    {
                        if (eventRecord.TimeCreated > _endDate.Value)
                        {
                            //too new
                            _logger.Debug($"Dropping record Id '{eventRecord.EventRecordId}' with timestamp '{eventRecord.TimeCreated.ToUniversalTime().ToString(dt)}' as its too new.");
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
                        _logger.Error($"Error processing record #{eventRecord.RecordNumber}: {e.Message}");
                        evt.ErrorRecords.Add(21,e.Message);
                    }
                }

                fsw.Stop();

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

                if (met && evt.EventIdMetrics.Count > 0)
                {
                    _logger.Fatal("\r\nMetrics (including dropped events)");
                    _logger.Warn("Event ID\tCount");
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }
            
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

   
}