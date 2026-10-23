# EvtxECmd

Parses Windows event logs (`.evtx`), including logs collected with Windows Event Forwarding, into one standardized
layout: maps turn the EventData of each event into common columns (`UserName`, `RemoteHost`, `ExecutableInfo`,
`PayloadData1`-`6`) and a description. Logs in use (read raw, as administrator) and Volume Shadow Copies can be
processed too. Results are exported to CSV, JSON and XML.

| | |
|---|---|
| **Version** | 2026.10.1 |
| **Download** | [.NET 10](https://download.ericzimmermanstools.com/net/EvtxECmd.zip) (recommended) &middot; [.NET Framework 4.7.2](https://download.ericzimmermanstools.com/EvtxECmd.zip) |
| **All tools** | [ericzimmerman.github.io](https://ericzimmerman.github.io) or [Get-ZimmermanTools](https://download.ericzimmermanstools.com/Get-ZimmermanTools.zip), which downloads them all and keeps them current |
| **Changes** | [ChangeLog.txt](https://download.ericzimmermanstools.com/ChangeLog.txt) |
| **Issues** | [EricZimmerman/evtx](https://github.com/EricZimmerman/evtx/issues) or [EricZimmerman/Issues](https://github.com/EricZimmerman/Issues/issues) |

## Requirements

- **.NET 10 build**: the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Run `EvtxECmd.exe` on
  Windows, or `dotnet EvtxECmd.dll` on any system with the runtime (`--vss` and reading logs in use are Windows only).
- **.NET Framework build**: .NET Framework 4.7.2 or later (Windows only).

## Command line

```text
Usage:
  EvtxECmd [options]

Options:
  -f <f>           File to process. Either this or -d is required
  -d <d>           Directory to recursively process. Either this or -f is required
  --csv <csv>      Directory to save CSV formatted results to. Be sure to include the full path in double quotes
  --csvf <csvf>    File name to save CSV formatted results to. When present, overrides default name
  --json <json>    Directory to save JSON formatted results to. Be sure to include the full path in double quotes
  --jsonf <jsonf>  File name to save JSON formatted results to. When present, overrides default name
  --xml <xml>      Directory to save XML formatted results to
  --xmlf <xmlf>    File name to save XML formatted results to. When present, overrides default name
  --dt <dt>        The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options [default: yyyy-MM-dd HH:mm:ss.fffffff]
  --inc <inc>      List of Event IDs to process. All others are ignored. Overrides --exc Format is 4624,4625,5410,5500-5600
  --exc <exc>      List of Event IDs to IGNORE. All others are included. Format is 4624,4625,5410,5500-5600
  --sd <sd>        Start date for including events (UTC). Anything OLDER than this is dropped. Format should match --dt
  --ed <ed>        End date for including events (UTC). Anything NEWER than this is dropped. Format should match --dt
  --fj             When true, export all available data when using --json
  --tdt <tdt>      The number of seconds to use for time discrepancy detection. Default is 1 [default: 1]
  --met            When true, show metrics about processed event log. Default is true
  --maps <maps>    The path where event maps are located. Defaults to 'Maps' folder where program was executed
  --vss            Process all Volume Shadow Copies that exist on drive specified by -f or -d
  --dedupe         Deduplicate -f or -d & VSCs based on SHA-1. First file found wins
  --sync           If true, the latest maps from https://github.com/EricZimmerman/evtx/tree/master/evtx/Maps are downloaded and local maps updated
  --debug          Show debug information during processing
  --trace          Show trace information during processing
  -?, -h, --help   Show help and usage information
  --version        Show version information
```

Short options (a single letter) start with one dash, long options with two.

## Examples

```text
EvtxECmd.exe -f "C:\Temp\Application.evtx" --csv "c:\temp\out" --csvf MyOutputFile.csv
EvtxECmd.exe -f "C:\Temp\Application.evtx" --csv "c:\temp\out"
EvtxECmd.exe -f "C:\Temp\Application.evtx" --json "c:\temp\jsonout"
```

## Output

- **Console**: the details of each log, the records included, errors and events dropped (by `--inc`, `--exc`, `--sd`
  and `--ed`), the records that could not be read (by record number) and, with `--met` (on by default), the number of
  events per event ID.
- **CSV** (`--csv`): one row per event in `<timestamp>_EvtxECmd_Output.csv` (or the `--csvf` name). The columns are
  `RecordNumber`, `EventRecordId`, `TimeCreated`, `EventId`, `Level`, `Provider`, `Channel`, `ProcessId`, `ThreadId`,
  `Computer`, `ChunkNumber`, `UserId`, `MapDescription`, `UserName`, `RemoteHost`, `PayloadData1`-`6`,
  `ExecutableInfo`, `HiddenRecord`, `SourceFile`, `Keywords`, `ExtraDataOffset`, `Payload` (the EventData or UserData
  as JSON) and `RenderingInfo` (the message and the level, task, opcode and keyword names that forwarded events carry
  from the source computer).
- **JSON** (`--json`): one object per line in `<timestamp>_EvtxECmd_Output.json` (or the `--jsonf` name), time stamps in
  ISO 8601 UTC (`2024-01-02T03:04:05.6780000Z`). `--fj` writes the whole event instead of the columns above.
- **XML** (`--xml`): every event as XML, in `<timestamp>_EvtxECmd_Output.xml` (or the `--xmlf` name).

A file that is not an event log is reported and skipped. A damaged chunk or record is reported and skipped, and the rest
of the log is still read. Records hidden in the slack of another record (as DanderSpritz's eventlogedit leaves them)
are returned with `HiddenRecord` set.

## Maps

Maps are YAML files, one per event (`Channel_Provider_EventID.map`), that pick values out of an event's XML with XPath
and put them in `PayloadData1`-`6`, `UserName`, `RemoteHost` and `ExecutableInfo`, with `MapDescription` saying what
the event is.

- EvtxECmd loads the maps from the `Maps` folder next to it, or the folder given with `--maps`.
- They live in this repository, in [evtx/Maps](https://github.com/EricZimmerman/evtx/tree/master/evtx/Maps).
  `EvtxECmd.exe --sync` downloads the latest maps from there into the `Maps` folder next to EvtxECmd, so maps merged
  into the repository reach you without a new release.
- To write a map, see the [README](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!!!!README.md) in the
  Maps folder, and use the
  [Guide](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!Channel-Name_Provider-Name_EventID.guide) to
  make maps from the
  [Template](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!Channel-Name_Provider-Name_EventID.template).

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. Damaged event logs, damaged records and files that aren't event logs are reported but are not a failure |
| `1` | The tool failed: invalid arguments, a missing `-f` or `-d` path, output that can't be written, or event logs or folders it can't open or read. Folders under `-d` that can't be read are reported (`Unable to read folder <path>: <reason>`) and the rest is still processed |

## What's new in 2026.10

- About 2x faster overall (1,039 logs: 589 s -> 300 s); large logs 2.4-4x faster (128 MB Security log: 16.1 s ->
  6.6 s), and maps load faster.
- New event log parser, written to MS-EVEN6 and checked against the Rust evtx parser: about 331,000 more records read
  over 1,039 test logs. Forwarded events (ForwardedEvents and other Windows Event Forwarding logs) are read; they gave
  no records.
- New `RenderingInfo` column (last CSV column) with the message and names that forwarded events carry.
- JSON time stamps are ISO 8601 UTC with a Z; `&`, `<` and `>` are no longer written as `&amp;`, `&lt;` and `&gt;` in
  `Payload` and the map columns.
- Map Values can be XPath expressions that calculate a value (#99); a Value that isn't valid XPath is reported when
  maps load.
- A damaged record or chunk is reported and skipped, and the rest of the log is still read; a log in use that can't be
  read raw is skipped instead of ending the run. `--vss` works again.
- Exit code 1 when the tool fails (it was always 0). Error reports include source file and line numbers.

The full list is in the [ChangeLog](https://download.ericzimmermanstools.com/ChangeLog.txt).

## Building

```text
dotnet build evtx.slnx -c Release
```

Builds `net10.0` and `net472`. Event log parsing is in the `evtx` library in this repository
([NuGet](https://www.nuget.org/packages/evtx)), which parses
[MS-EVEN6](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-even6/) BinXml:

```csharp
using var fs = File.OpenRead(@"C:\Temp\Security.evtx");
var log = new evtx.EventLog(fs);
foreach (var record in log.GetEventRecords())
{
    Console.WriteLine($"{record.RecordNumber} {record.TimeCreated} {record.EventId} {record.Provider}");
    var xml = record.ConvertPayloadToXml(); //the event as XML
    var json = record.PayloadAsJson();      //EventData/UserData as JSON
}
```

Call `EventLog.LoadMaps(path)` first to fill the map columns. A file that is not an event log throws
`EventLogParseException`; records that could not be read are in `log.ErrorRecords`, by record number.

## Further reading

- [Introducing EvtxECmd!!](https://binaryforay.blogspot.com/2019/04/introducing-evtxecmd.html)
- [Introduction to EvtxECmd](https://www.youtube.com/watch?v=YvMg3p7O6ro)
- [Enhancing Event Log Analysis with EvtxEcmd using KAPE](https://www.youtube.com/watch?v=BIkyWexMF0I)

## Special thanks

Open source development funding and support provided by the [SANS Institute](https://www.sans.org/) and
[SANS DFIR](https://www.sans.org/digital-forensics-incident-response/).

## License

[MIT](LICENSE), Copyright (c) Eric R. Zimmerman 2027.
