# EvtxECmd

This repo that contains all the Maps used by Eric Zimmerman's EvtxECmd.

## Ongoing Projects

 * [EvtxECmd Maps Ideas](https://github.com/EricZimmerman/evtx/projects/1) - Development roadmap for EvtxECmd Maps. Please feel free to contribute by adding ideas or by finishing tasks in the `To Do` column. Any help is appreciated! 


## Command Line Interface

    EvtxECmd version 0.6.5.0
    
    Author: Eric Zimmerman (saericzimmerman@gmail.com)
    https://github.com/EricZimmerman/evtx
    
            d               Directory to process that contains evtx files. This or -f is required
            f               File to process. This or -d is required
    
            csv             Directory to save CSV formatted results to.
            csvf            File name to save CSV formatted results to. When present, overrides default name
            json            Directory to save JSON formatted results to.
            jsonf           File name to save JSON formatted results to. When present, overrides default name
            xml             Directory to save XML formatted results to.
            xmlf            File name to save XML formatted results to. When present, overrides default name
    
            dt              The custom date/time format to use when displaying time stamps. Default is: yyyy-MM-dd HH:mm:ss.fffffff
            inc             List of Event IDs to process. All others are ignored. Overrides --exc Format is 4624,4625,5410
            exc             List of Event IDs to IGNORE. All others are included. Format is 4624,4625,5410
            sd              Start date for including events (UTC). Anything OLDER than this is dropped. Format should match --dt
            ed              End date for including events (UTC). Anything NEWER than this is dropped. Format should match --dt
            fj              When true, export all available data when using --json. Default is FALSE.
            tdt             The number of seconds to use for time discrepancy detection. Default is 1 second
            met             When true, show metrics about processed event log. Default is TRUE.

            maps            The path where event maps are located. Defaults to 'Maps' folder where program was executed

            vss             Process all Volume Shadow Copies that exist on drive specified by -f or -d . Default is FALSE
            dedupe          Deduplicate -f or -d & VSCs based on SHA-1. First file found wins. Default is TRUE

            sync            If true, the latest maps from https://github.com/EricZimmerman/evtx/tree/master/evtx/Maps are downloaded and local maps updated. Default is FALSE

            debug           Show debug information during processing
            trace           Show trace information during processing


    Examples: EvtxECmd.exe -f "C:\Temp\Application.evtx" --csv "c:\temp\out" --csvf MyOutputFile.csv
              EvtxECmd.exe -f "C:\Temp\Application.evtx" --csv "c:\temp\out"
              EvtxECmd.exe -f "C:\Temp\Application.evtx" --json "c:\temp\jsonout"

              Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes

## Documentation

This project contains both the core parsing engine as well as a command line front end that uses it.

For documentation on creating maps, check out the [README](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!!!!README.md) in the Maps directory. 

Use the [Guide](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!Channel-Name_Provider-Name_EventID.guide) to learn how to make maps from the [Template](https://github.com/EricZimmerman/evtx/blob/master/evtx/Maps/!Channel-Name_Provider-Name_EventID.template) provided.

[Introducing EvtxECmd!!](https://binaryforay.blogspot.com/2019/04/introducing-evtxecmd.html)

[Introduction to EvtxECmd](https://www.youtube.com/watch?v=YvMg3p7O6ro)

[Enhancing Event Log Analysis with EvtxEcmd using KAPE](https://www.youtube.com/watch?v=BIkyWexMF0I)

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md). Use the [Get-ZimmermanTools](https://f001.backblazeb2.com/file/EricZimmermanTools/Get-ZimmermanTools.zip) PowerShell script to automate the download and updating of the EZ Tools suite. Additionally, you can automate each of these tools using [KAPE](https://www.kroll.com/en/services/cyber-risk/incident-response-litigation-support/kroll-artifact-parser-extractor-kape)!

# Special Thanks

Open Source Development funding and support provided by the following contributors: [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
