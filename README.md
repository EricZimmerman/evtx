# EVTX parser

This project contains both the core parsing engine as well as a command line front end that uses it.


MAPS NEEDS. If you have any of these event IDs in your logs, send me the XML or the evtx file and i can finish. This is a work in progress so expect this list to change often

-------------------------- I DONT HAVE LOGS FOR THESE

Round one missing stuff

Security: 4657, Security ID,Account Name, Logon ID, Object Name,Object Value Name, Old Value, New Value reg key created/modified/deleted

Security: 4652/4653: An IPsec Main Mode negotiation failed. 
Security: For Kerberos authentication, 4771. 

-------------------------- I DONT HAVE LOGS FOR THESE

NEEDS REGEX WORK

System: 6100, all of the wireless networks the device can see, typically happens when network adapter starts/restarts. Its not easy to parse in the format you're looking for, but "BSSID" is there in every language I've encountered, so you can grep for it. (link: https://www.brimorlabsblog.com/2014/04/you-dont-know-where-that-device-has-been.html) brimorlabsblog.com/2014/04/you-do…

NEEDS REGEX WORK



------------------TO DO TO DO TO DO--------------------------------

------------------------------------------------

Audit against
https://github.com/keydet89/Tools/blob/master/exe/eventmap.txt


8003 & 8006; #AppLocker audited event IDs. Many orgs don't want (read: can't manage) application whitelisting blocking policies, but that doesn't mean you can't build a policy in audit-only mode and on-board the relevant logs.  Provides an excellent ROI.


Sysmon event ids 1,3, and 5,  




Microsoft-Windows-PowerShell%4Operational.evtx
4103, 4104 – Script Block logging
Logs suspicious scripts by default in PS v5 Logs all scripts if configured
53504 Records the authenticating user

powershell 400,500,501,800, and 4104


Windows PowerShell.evtx
400/403 "ServerRemoteHost" indicates start/end of Remoting session
800 Includes partial script code
Microsoft-Windows-WinRM%4Operational.evtx
91 Session creation
168 Records the authenticating user

Microsoft-Windows-PowerShell%4Operational.evtx
40961, 40962
Records the local initiation of powershell.exe and associated user account
8193 & 8194
Session created

8197 - Connect
Session closed



Microsoft-Windows-WinRM%4Operational.evtx
6 – WSMan Session initialize
Session created
Destination Host Name or IP
Current logged-on User Name
8, 15, 16, 33 – WSMan Session deinitialization
Closing of WSMan session
Current logged-on User Name


Microsoft-Windows-SmbClient%4Security.evtx
31001 – Failed logon todestination
Destination Host Name
User Name for failed logon
Reason code for failed destination logon (e.g. bad password)
