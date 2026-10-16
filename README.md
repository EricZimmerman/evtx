# EvtxECmd

This repo that contains all the Maps used by Eric Zimmerman's EvtxECmd.

## Ongoing Projects

 * [EvtxECmd Maps Ideas](https://github.com/EricZimmerman/evtx/projects/1) - Development roadmap for EvtxECmd Maps. Please feel free to contribute by adding ideas or by finishing tasks in the `To Do` column. Any help is appreciated! 


## Command Line Interface

    Description:
      EvtxECmd version 2026.10.0

      Author: Eric Zimmerman (saericzimmerman@gmail.com)
      https://github.com/EricZimmerman/evtx

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

    Examples: EvtxECmd.exe -f "C:\Temp\Application.evtx" --csv "c:\temp\out" --csvf MyOutputFile.csv
              EvtxECmd.exe -f "C:\Temp\Application.evtx" --csv "c:\temp\out"
              EvtxECmd.exe -f "C:\Temp\Application.evtx" --json "c:\temp\jsonout"

              Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes

EvtxECmd exits with 0 when everything was processed, and 1 for bad arguments, a file that could not be processed, or output that could not be written.

## Library

The `evtx` NuGet package parses event logs ([MS-EVEN6](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-even6/) BinXml, including logs collected with Windows Event Forwarding, whose records have no templates):

```csharp
using var fs = File.OpenRead(@"C:\Temp\Security.evtx");
var log = new evtx.EventLog(fs);
foreach (var record in log.GetEventRecords())
{
    Console.WriteLine($"{record.RecordNumber} {record.TimeCreated} {record.EventId} {record.Provider}");
    var xml = record.ConvertPayloadToXml(); //the event as XML
    var json = record.PayloadAsJson();      //EventData/UserData as JSON
}

foreach (var error in log.ErrorRecords) //records that could not be read, by record number
{
    Console.WriteLine($"{error.Key}: {error.Value}");
}
```

- Call `EventLog.LoadMaps(path)` first to fill `PayloadData1`-`6`, `UserName`, `RemoteHost`, `ExecutableInfo` and `MapDescription` from the maps.
- A file that is not an event log throws `EventLogParseException`. A damaged chunk or record is logged and skipped, and the rest of the log is still read.
- Records hidden in another record's slack (DanderSpritz's eventlogedit) are returned with `HiddenRecord` set.
- `RenderingInfo` holds the `RenderingInfo` element of forwarded events (the message text and level/task/opcode names from the source computer) as JSON.

## Documentation

This project contains both the core parsing engine as well as a command line front end that uses it.

For documentation on creating maps, check out the [README](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!!!!README.md) in the Maps directory. 

Use the [Guide](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!Channel-Name_Provider-Name_EventID.guide) to learn how to make maps from the [Template](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!Channel-Name_Provider-Name_EventID.template) provided.

[Introducing EvtxECmd!!](https://binaryforay.blogspot.com/2019/04/introducing-evtxecmd.html)

[Introduction to EvtxECmd](https://www.youtube.com/watch?v=YvMg3p7O6ro)

[Enhancing Event Log Analysis with EvtxEcmd using KAPE](https://www.youtube.com/watch?v=BIkyWexMF0I)

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md). 

# Special Thanks

Open Source Development funding and support provided by the following contributors: 
- [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
- [Tines](https://www.tines.com/?utm_source=oss&utm_medium=sponsorship&utm_campaign=ericzimmerman)
