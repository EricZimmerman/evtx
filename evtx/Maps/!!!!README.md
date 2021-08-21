# EvtxECmd Maps

Map files are used to convert the EventData (the unique part of an event) to a more standardized format. Map files are specific to a certain type of event log, such as Security, Application, etc.

Because different event logs may reuse Event IDs, Maps need to be specific to a certain kind of log. This specificity is done by using a unique identifier for a given event log, the Channel. We will see more about this in a moment.

Once you know what event log and Event ID you want to make a Map for, the first thing to do is dump the log's records to XML, using EvtxECmd.exe as follows:

```
EvtxECmd.exe -f <your eventlog> --xml c:\temp\xml
```

When the command finishes, open the generated xml file in c:\temp\ and find your Event ID of interest. Let's say its from the Security log and its Event ID 4624, It might look like this:

```xml
<Event>
  <System>
    <Provider Name="Microsoft-Windows-Security-Auditing" Guid="54849625-5478-4994-a5ba-3e3b0328c30d" />
    <EventID>4624</EventID>
    <Version>2</Version>
    <Level>0</Level>
    <Task>12544</Task>
    <Opcode>0</Opcode>
    <Keywords>0x8020000000000000</Keywords>
    <TimeCreated SystemTime="2018-05-04 22:14:31.3192953" />
    <EventRecordID>495</EventRecordID>
    <Correlation ActivityID="69aa9df0-e3d4-0007-f39d-aa69d4e3d301" />
    <Execution ProcessID="716" ThreadID="6724" />
    <Channel>Security</Channel>
    <Computer>win10-test</Computer>
    <Security />
  </System>
  <EventData>
    <Data Name="SubjectUserSid">S-1-5-18</Data>
    <Data Name="SubjectUserName">WIN10-TEST$</Data>
    <Data Name="SubjectDomainName">TEMP</Data>
    <Data Name="SubjectLogonId">0x3E7</Data>
    <Data Name="TargetUserSid">S-1-5-18</Data>
    <Data Name="TargetUserName">SYSTEM</Data>
    <Data Name="TargetDomainName">NT AUTHORITY</Data>
    <Data Name="TargetLogonId">0x3E7</Data>
    <Data Name="LogonType">5</Data>
    <Data Name="LogonProcessName">Advapi  </Data>
    <Data Name="AuthenticationPackageName">Negotiate</Data>
    <Data Name="WorkstationName">-</Data>
    <Data Name="LogonGuid">00000000-0000-0000-0000-000000000000</Data>
    <Data Name="TransmittedServices">-</Data>
    <Data Name="LmPackageName">-</Data>
    <Data Name="KeyLength">0</Data>
    <Data Name="ProcessId">0x2C4</Data>
    <Data Name="ProcessName">C:\Windows\System32\services.exe</Data>
    <Data Name="IpAddress">-</Data>
    <Data Name="IpPort">-</Data>
    <Data Name="ImpersonationLevel">%%1833</Data>
    <Data Name="RestrictedAdminMode">-</Data>
    <Data Name="TargetOutboundUserName">-</Data>
    <Data Name="TargetOutboundDomainName">-</Data>
    <Data Name="VirtualAccount">%%1843</Data>
    <Data Name="TargetLinkedLogonId">0x0</Data>
    <Data Name="ElevatedToken">%%1842</Data>
  </EventData>
</Event>
```

Just about everything in the `<System>` element is normalized by default, but if you want to include anything from there you can do so using the techniques we will see below.

In most cases, the data in the `<EventData>` block is what you want to process. This is where xpath queries come into play.

So let's take a look at a Map to make things a bit more clear.

In the example below, there are four header properties that describe the Map: who wrote it, what its for, the Channel, and the Event ID the Map corresponds to.

The Channel and Event ID property are what make a Map unique, not the name of the file. As long as the Map ends with '.Map' it will be processed.

The Channel is a useful identifier for a given log type. It can be seen in the `<Channel>` element ("Security" in the example above).

The Maps collection contains configurations for how to look for data in an events EventData and extract out particular properties into variables. These variables are then combined and Mapped to the event record's first class properties.

For example, consider the first Map, for `UserName`, below.

The `PropertyValue` defines the pattern that will be used to build the final value that will be assigned to the UserName field in the CSV. Variables in patterns are surrounded by % on both sides, so we see two variables defined: `%domain%` and `%user%`

In the Map entries `Values` collection, we actually populate these variables by giving the value a name (domain in the first case) and an xpath query that will be used to set the value for the variable (`"/Event/EventData/Data[@Name=\"SubjectDomainName\"]"` in the first case).

When a Map is processed, each Map entry has its `Values` items processed so the variables are populated with data. Then the `PropertyValue` is updated and the variables are replaced with the actual values. This final PropertyValue is then updated in the event record which then ends up in the CSV/JSON, etc.

It is that simple! Be sure to surround things in double quotes and/or escape quotes as in the examples. When in doubt, test your Map against real data!

NOTE! The filenames for Maps should be in the following format:

`Channel-Name_Provider-Name_EventID.Map`

Where Channel is EXACTLY what is in the XML `<Channel>` element with any '/' characters, hyphens, or spaces replaced with a hyphen. Hyphens are the catch all for each element of the Map filename.

Only underscores should separate each element (Channel Name, Provider Name, EventID). Hyphens separates words. Underscores separate elements.

For example, for Event ID `201` and Channel `Microsoft-Windows-TaskScheduler/Operational` the file should be named:

`Microsoft-Windows-TaskScheduler-Operational_Microsoft-Windows-TaskScheduler_201.Map`

`Provider` is now mandatory. Provider is used at the header level and looks like this:

`Provider: "Microsoft-Windows-Power-Troubleshooter"`

This lets you further narrow down when a Map will be used. Every Map will have a working example of this now.

As of v06 or so, you can also add optional properties such as `Lookups`.

Lookups allow you to define lookup tables that match one value and replace them with another. Here is an example, also from `System_Microsoft-Windows-Kernel-General_1.Map`:

Lookups:
```
  -
    Name: WakeSourceType
    Default: Unknown code
    Values:
        0: Unknown
        1: Power button
        3: Waking from sleep to hibernate
        5: Device (See WakeSourceText for details)
        6: Timer (See WakeSourceText for details)
```

The name of the lookup table determines when it will be used and should match the name of the property you want to apply the lookup to. Example:

```
  -
    Property: PayloadData2
    PropertyValue: Wake source "%WakeSourceType%"
    Values:
      -
        Name: WakeSourceType
        Value: "/Event/EventData/Data[@Name=\"WakeSourceType\"]"
```

Here, when the Map is applied, the numerical value for WakeSourceType is filtered through the Lookup with the same name, and the value is updated to reflect the more human readable version. If you want BOTH the original value and  the lookup value, simply reference the original using a different Name under Values, then reference that adjusted name as a variable, like this:
```
  -
    Property: PayloadData2
    PropertyValue: Wake source "%WakeSourceType%" (%WakeSourceTypeOrg%)
    Values:
      -
        Name: WakeSourceType
        Value: "/Event/EventData/Data[@Name=\"WakeSourceType\"]"
      -
        Name: WakeSourceTypeOrg
        Value: "/Event/EventData/Data[@Name=\"WakeSourceType\"]"
```

here is another example of a Map:

---- START MAP HERE ----
```
Author: Eric Zimmerman saericzimmerman@gmail.com
Description: Security 4624 event
EventId: 4624
Channel: Security
Maps:
  -
    Property: UserName
    PropertyValue: "%domain%\\%user%"
    Values:
      -
        Name: domain
        Value: "/Event/EventData/Data[@Name=\"SubjectDomainName\"]"
      -
        Name: user
        Value: "/Event/EventData/Data[@Name=\"SubjectUserName\"]"
  -
    Property: RemoteHost
    PropertyValue: "%workstation% (%ipAddress%)"
    Values:
      -
        Name: ipAddress
        Value: "/Event/EventData/Data[@Name=\"IpAddress\"]"
      -
        Name: workstation
        Value: "/Event/EventData/Data[@Name=\"WorkstationName\"]"
  -
    Property: PayloadData1
    PropertyValue: LogonType %LogonType%
    Values:
      -
        Name: LogonType
        Value: "/Event/EventData/Data[@Name=\"LogonType\"]"

# Valid values for the Property field include:
# UserName
# RemoteHost
# ExecutableInfo --> used for things like process command line, scheduled task, info from service install, etc.
# PayloadData1 through PayloadData6
```
---- END MAP HERE ----

Map files are read in order, alphabetically. This means you can create your own alternative Maps to the default by doing the following:

1. make a copy of the Map you want to modify
2. name it the same as the Map you are interested in, but prepend 1_ to the front of the filename.
3. edit the new Map to meet your needs

Example:

`Security_Microsoft-Windows-Security-Auditing_4624.Map` is copied and renamed to:

`1_Security_Microsoft-Windows-Security-Auditing_4624.Map`

Edit `1_Security_Microsoft-Windows-Security-Auditing_4624.Map` and make your changes

When the Maps are loaded, since `1_Security_Microsoft-Windows-Security-Auditing_4624.Map` comes before `Security_Microsoft-Windows-Security-Auditing_4624.Map`, only the one with your changes will be loaded.

This also allows you to update default Maps without having your customizations blown away every time there is an update.

TIPS:

If you are looking to make an Application.evtx Map, please include a Provider as they are many instances where the same event ID number is used for multiple providers. I've personally observed 4 Providers use Event ID 1 which without a Provider being listed for that Map it made all 4 events, regardless of Provider, be Mapped incorrectly. When in doubt, add a Provider to your Map. Follow a template from a previously created Map to ensure it's made correctly.

UPDATE: As of December 2020, Provider is now mandatory to avoid the above issue!

# Updating Documentation

If you are looking for a way to contribute without making a Map, search across the contents of all Maps for "N/A" and try to find documentation for any of the Maps in the repository. Ideally, each Map will have as much documentation as possible that exists for that specific event. This can serve as a good reference for anyone using the tool as well as a learning tool for students and those new to the field.
