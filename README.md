GCPAnyMetric is designed to forward any Windows Performance Metrics to Google Cloud Monitoring. It is a simple C# application developed with Visual Studio 2022. The application is NOT a Windows Service natively at this point. It is expected that the end user will use the Windows 2003 Resource Kit tool srvany.exe to run this application as a service. Once the srvany.exe configuration has been done to create the Windows Service - which MUST be called - GCPAnyMetric - the registry parameters for the service must be configured.

The Application itself contained in this repo in the bin/release folder must be copied to a subfolder called GCPAnyMetric in the C:\Monitor folder.

There is a number of Registry Entries required that define the configuration.

**SRVANY Required Registry Keys.**
In the HKLM\CurrentControlSet\Services\GCPAnyMetric Key there should be a subkey called Parameters. In this subkey should exist a REG_SZ value called Application with a path of c:\monitor\gcpanymetric.exe


**GCPAnyMetric Required Registry Keys.**
A subkey of HKLM\CurrentControlSet\Services\GCPAnyMetric called Configuration should exist. Within this Configuration subkey is where the keys and Values are defined that define what Windows Performance Metrics you wish to monitor.
Highlevel Structure of Subkeys.
A Subkey should be created for each Category of Performance Metric that you wish to use. These Categories can be observed in Windows Perfromance Monitor, when you go to add a counter, the Categories are listed in the dialog box on the left of the pop up window. These are values such as:

    * Processor
    * Processor Information
    * RAS
    * RAS Port
    * etc...

It is these Category Names that will be the subkey names under the Configuration key. For example if we wish to monitor Terminal Services Active Sessions - the Category is Terminal Services, the Counter is Active Sessions. So we would create a subkey under the Configuration key called - Terminal Services.

Within the category we now define the Counters we wish to use. To define a counter in the Registry, find the Category subkey and add a REG_SZ value - the Value Name must equal the name of the Counter exactly. The Value assigned is the string that will appear in Google Cloud Monitoring. For example if I put the string TerminalServices/ActiveConnections as the value then in Google Cloud Monitoring I can expect to see under Custom metrics a metric called TerminalServices/ActiveConnections. So as to demonstrate the complete example; if I wished to send the Terminal Services Active Sessions to Google Cloud Monitoring and call it TerminalServices/ActiveConnections I would:

    1. Have a subkey under HKLM\CurrentControlSet\Services\GCPAnymetric called Configuration.
    2. Under Configuration I would have another subkey called Terminal Services (which is the category)
    3. I would have a REG_SZ Value with a label name called Active Sessions
    4. I would assign a value to this REG_SZ - TerminalServices/ActiveConnections

What if the Performance Counter has multiple instances such as Processor which has _Total and other instances.
To address Windows Performance Counters which have multiple instances, please use the "/" (no quotes) character in the REG_SZ label name. For example "% User Time/_Total" (no quotes). The string after the "/" is the instance name that MUST correspond to the instance name shown in Windows Performance Monitor.
