Map files are read in order, alphabetically. This means you can create your own alternative maps to the default by doing the following:

1. make a copy of the map you want to modify
2. name it the same as the map you are interested in, but prepend 1_ to the front of the filename.
3. edit the new map to meet your needs

Example:

4624.map is copied and renamed to:

1_4624.map

Edit 1_4624.map and make your changes

When the maps are loaded, since 1_4624.map comes before 4624.map, only the one with your changes will be loaded.

This also allows you to update default maps without having your customizations blown away every time there is an update.

The following is a brief tutorial in map making

In the example below, there are 3 header properties that descrive the map: who wrote it, what its for, and the event id the map corresponds to. 

The EventId property is what matters here, not the name of the file.

The Maps collection contains configurations for how to look for data in an events EventData and extract out particular properties into variables. These variables are then combined and mapped to the event record's first class properties.

For example, consider the first map, for Username, below.

The PropertyValue defines the pattern that will be used to build the final value that will be assigned to the Username field in the CSV. Variables in patterns are surrouned by % on both sides, so we see two variables defined: %domain% and %user%

In the map entries Values collection, we actually populate these variables by giving the value a name (domain in the first case) and an xpath query that will be used to set the value for the variable ("/Event/EventData/Data[@Name=\"SubjectDomainName\"]" in the first case).

When a map is processed, each map entry has its Values items processed so the variables are populated with data. Then the PropertyValue is updated and the variables are replaced with the actual values. This final PropertyValue is then updated in the event record which then ends up in the CSV/json, etc.

It is that simple! Be sure to surround things in double quotes and/or escape quotes as in the examples. When in doubt, test your map against real data!


---- START MAP HERE ----

Author: Eric Zimmerman saericzimmerman@gmail.com
Description: "4624 event"
EventId: 4624
Maps: 
  - 
    Property: Username
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

# Valid properties include:
# UserName
# RemoteHost
# ExecutableInfo --> used for things like process command line, scheduled task, info from service install, etc.
# PayloadData1 through PayloadData6

# Example payload data
#   </System>
#   <EventData>
#     <Data Name="SubjectUserSid">S-1-5-18</Data>
#     <Data Name="SubjectUserName">DESKTOP-9L1HKC9$</Data>
#     <Data Name="SubjectDomainName">WORKGROUP</Data>
#     <Data Name="SubjectLogonId">E7-03-00-00-00-00-00-00</Data>
#     <Data Name="TargetUserSid">S-1-5-18</Data>
#     <Data Name="TargetUserName">SYSTEM</Data>
#     <Data Name="TargetDomainName">NT AUTHORITY</Data>
#     <Data Name="TargetLogonId">E7-03-00-00-00-00-00-00</Data>
#     <Data Name="LogonType">5</Data>
#     <Data Name="LogonProcessName">Advapi  </Data>
#     <Data Name="AuthenticationPackageName">Negotiate</Data>
#     <Data Name="WorkstationName">-</Data>
#     <Data Name="LogonGuid">00000000-0000-0000-0000-000000000000</Data>
#     <Data Name="TransmittedServices">-</Data>
#     <Data Name="LmPackageName">-</Data>
#     <Data Name="KeyLength">0</Data>
#     <Data Name="ProcessId">FC-02-00-00-00-00-00-00</Data>
#     <Data Name="ProcessName">C:\Windows\System32\services.exe</Data>
#     <Data Name="IpAddress">-</Data>
#     <Data Name="IpPort">-</Data>
#     <Data Name="ImpersonationLevel">%%1833</Data>
#     <Data Name="RestrictedAdminMode">-</Data>
#     <Data Name="TargetOutboundUserName">-</Data>
#     <Data Name="TargetOutboundDomainName">-</Data>
#     <Data Name="VirtualAccount">%%1843</Data>
#     <Data Name="TargetLinkedLogonId">00-00-00-00-00-00-00-00</Data>
#     <Data Name="ElevatedToken">%%1842</Data>
#   </EventData>