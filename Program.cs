using System;
using System.Net;

using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Google.Api;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Monitoring.V3;
using Newtonsoft.Json.Linq;
using static Google.Api.MetricDescriptor.Types;
//using CommandLine;
using Google.Api.Gax;
using Google.Protobuf.WellKnownTypes;
using Google.Cloud.Compute.V1;
using Microsoft.Win32;




namespace GCPAnyMetric
{
    public class Monitoring
    {
        private static readonly DateTime s_unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);





        internal class Program
        {

            private bool KeyExists(RegistryKey baseKey, string subKeyName)
            {
               RegistryKey ret = baseKey.OpenSubKey(subKeyName);
               return ret != null;
            }
            static void Main()
            {
                const string MachineRoot = "HKEY_LOCAL_MACHINE";
                const string subkey = "SYSTEM\\CurrentControlSet\\Services";
                const string keyName = MachineRoot + "\\" + subkey;
                const string basekey = "GCPAnyMetric";
                RegistryKey rkCurrentmachine = Registry.Localmachine;

                RegistryKey key = rkCurrentmachine.OpenSubKey(subkey);
                bool keyfound = KeyExists(key,basekey);
                if(keyfound){
                    Console.WriteLine("Key Found");
                    System.Environment.Exit(0);
                }else{
                    Console.WriteLine("key not found");
                    System.Environment.Exit(0);
                }
                var projectid = "sharedvpc-339904";
                var myzone = "australia-southeast1-b";
                String hostName = Dns.GetHostName();
                var credential = GoogleCredential.FromFile("c:\\monitor\\sa.json");
                Google.Cloud.Compute.V1.InstancesClient client = Google.Cloud.Compute.V1.InstancesClient.Create();
                // Make the request
                GetInstanceRequest request = new GetInstanceRequest
                {
                    Zone = myzone,
                    Instance = hostName,
                    Project = projectid,
                };
                // Make the request
                Instance response = client.Get(request);
                var machineid = response.Id.ToString();
                PerformanceCounter Counter;
                Counter = new PerformanceCounter("Terminal Services", "Active Sessions");
                // Create client.
                MetricServiceClient metricServiceClient = MetricServiceClient.Create();
                // Initialize request argument(s).
                ProjectName name = new ProjectName(projectid);
                //*************************************************************************************
                bool OK = true;
                while(OK)
                {
                    var counterresult = Counter.NextValue();
                    // Prepare a data point. 
                    Point dataPoint = new Point();
                    TypedValue TerminalServerActiveConnections = new TypedValue();
                    TerminalServerActiveConnections.DoubleValue = counterresult;
                    dataPoint.Value = TerminalServerActiveConnections;
                    Google.Protobuf.WellKnownTypes.Timestamp timeStamp = new Google.Protobuf.WellKnownTypes.Timestamp();
                    timeStamp.Seconds = (long)(DateTime.UtcNow - s_unixEpoch).TotalSeconds;
                    TimeInterval interval = new TimeInterval();
                    interval.EndTime = timeStamp;
                    dataPoint.Interval = interval;

                    // Prepare custom metric.
                    Metric metric = new Metric();
                    metric.Type = "custom.googleapis.com/TerminalServices/ActiveConnections";
                    metric.Labels.Add("counter", "ActiveConnections");

                    // Prepare monitored resource.
                    MonitoredResource resource = new MonitoredResource();
                    resource.Type = "gce_instance";
                    resource.Labels.Add("project_id", projectid);
                    resource.Labels.Add("instance_id", machineid);
                    resource.Labels.Add("zone", myzone);

                    // Create a new time series using inputs.
                    TimeSeries timeSeriesData = new TimeSeries();
                    timeSeriesData.Metric = metric;
                    timeSeriesData.Resource = resource;
                    timeSeriesData.Points.Add(dataPoint);

                    // Add newly created time series to list of time series to be written.
                    IEnumerable<TimeSeries> timeSeries = new List<TimeSeries> { timeSeriesData };
                    // Write time series data.
                    try
                    {
                        metricServiceClient.CreateTimeSeries(name, timeSeries);
                        Thread.Sleep(60000);
                    }
                    catch
                    {
                        EventLog.WriteEntry("GCPAnyMetric", "Error occurred in processing metrics", EventLogEntryType.Error, 1, 1);
                        Thread.Sleep(180000);
                        OK = true;
                    }
                    // Console.WriteLine("Done writing time series data:");
                    // Console.WriteLine(JObject.Parse($"{timeSeriesData}").ToString());
                    
                }
            

                
            }
        }
    }
}
