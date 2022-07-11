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





        internal class GCPAnymetric
        {
            public static string[,,,] perfcounters = new string[10,2,2,2];
            public static int numcounters = 0;
            private static bool KeyExists(RegistryKey baseKey, string subkey)
            {

               
                RegistryKey ret = baseKey.OpenSubKey(subkey);
                return ret != null;
            }

            private static void processValueNames(RegistryKey Key, string sub)
            { //function to process the valueNames for a given key
                string[] valuenames = Key.GetValueNames();
                if (valuenames == null || valuenames.Length <= 0) //has no values
                    return;
                foreach (string valuename in valuenames)
                {
                    object obj = Key.GetValue(valuename);
                    if (obj != null)
                    {
                        if (valuename.IndexOf('/') > 0)   //if we find an underscore, then we have a performance counter object that supports instances
                        {
                            string[] strList = valuename.Split('/');
                            perfcounters[numcounters, 1, 0, 0] = strList[0];
                            perfcounters[numcounters, 0, 0, 1] = valuename.Substring(valuename.IndexOf("/") + 1);
                            perfcounters[numcounters, 0, 1, 0] = obj.ToString();
                            numcounters++;
                            perfcounters[numcounters, 0, 0, 0] = sub;
                           
                            
                        }
                        else
                        {
                            perfcounters[numcounters, 1, 0, 0] = valuename;
                            perfcounters[numcounters, 0, 1, 0] = obj.ToString();
                            numcounters++;
                            perfcounters[numcounters, 0, 0, 0] = sub;
                        }
                    }
                }
            }
            private static void GetSubKeys(RegistryKey SubKey)
            {
                foreach (string sub in SubKey.GetSubKeyNames())
                {
                    //Console.WriteLine(sub);
                    perfcounters[numcounters,0,0,0] = sub;
                   
                    RegistryKey local = Registry.LocalMachine;
                    local = SubKey.OpenSubKey(sub,false);
                    GetSubKeys(local); // By recalling itself it makes sure it get all the subkey names
                    numcounters++;
                }
                processValueNames(SubKey, perfcounters[numcounters, 0, 0,0]);
               
            }

          
           




            static void Main()
            {
                
                const string subkey = "SYSTEM\\CurrentControlSet\\Services";
                const string basekey = "GCPAnyMetric";
                string keyName = subkey + "\\" + basekey;
               
                RegistryKey rkCurrentmachine = Registry.LocalMachine;

                
               
                bool keyfound = KeyExists(rkCurrentmachine, keyName);
                if (keyfound)
                {
                    keyName = subkey + "\\" + basekey + "\\" + "Configuration";
                    keyfound = KeyExists(rkCurrentmachine, keyName);
                    if (keyfound)
                    {
                        // We are good to go
                        RegistryKey regkey = rkCurrentmachine.OpenSubKey(keyName);
                        var projectid = regkey.GetValue("projectid").ToString();
                        var myzone = regkey.GetValue("zone").ToString();
                        var sajson = regkey.GetValue("serviceaccountpath").ToString();
                        String hostName = Dns.GetHostName();
                        var credential = GoogleCredential.FromFile(sajson);
                        Google.Cloud.Compute.V1.InstancesClient client = Google.Cloud.Compute.V1.InstancesClient.Create();
                        // Make the request
                        if(projectid != null & myzone != null & sajson != null & credential != null)
                        {
                            // we will continue, all good
                            EventLog.WriteEntry("GCPAnyMetric", "GCPanyMetric is started", EventLogEntryType.Information, 2, 1);
                        }
                        else
                        {
                            EventLog.WriteEntry("GCPAnyMetric", "Missing registry key parameter, failed to start", EventLogEntryType.Error, 1, 1);
                            System.Environment.Exit(0);
                        }
                        GetInstanceRequest request = new GetInstanceRequest
                        {
                            Zone = myzone,
                            Instance = hostName,
                            Project = projectid,
                        };
                        // Make the request
                        Instance response = client.Get(request);
                        var machineid = response.Id.ToString();
                       
                        ProjectName name = new ProjectName(projectid);
                        //*************************************************************************************
                        GetSubKeys(regkey);

                        bool OK = true;
                        while (OK)
                        {
                            for (int i = 0; i < perfcounters.GetLength(0); i++)
                            {
                                var tmpstr = perfcounters[i, 0, 0,0];
                                var tmpstr1 = perfcounters[i, 1, 0,0];
                                if (tmpstr != null & tmpstr1 != null)
                                {
                                    PerformanceCounter Counter;
                                    if (perfcounters[i, 0, 0, 1] != null)
                                    {

                                        Counter = new PerformanceCounter(perfcounters[i, 0, 0, 0], perfcounters[i, 1, 0, 0], perfcounters[i, 0, 0, 1]);
                                        
                                    }
                                    else
                                    {
                                        Counter = new PerformanceCounter(perfcounters[i, 0, 0, 0], perfcounters[i, 1, 0, 0]);
                                    }
                                    // Create client.
                                    MetricServiceClient metricServiceClient = MetricServiceClient.Create();
                                    // Initialize request argument(s).
                                    Counter.NextValue(); //Run the Counter Next Value once for those counters that need to be run twice to get a comparitive
                                    Thread.Sleep(1000);
                                    var counterresult = Counter.NextValue();
                                    // Prepare a data point. 
                                    Point dataPoint = new Point();
                                    TypedValue gcpmetric = new TypedValue();
                                    gcpmetric.DoubleValue = counterresult;
                                    dataPoint.Value = gcpmetric;
                                    Google.Protobuf.WellKnownTypes.Timestamp timeStamp = new Google.Protobuf.WellKnownTypes.Timestamp();
                                    timeStamp.Seconds = (long)(DateTime.UtcNow - s_unixEpoch).TotalSeconds;
                                    TimeInterval interval = new TimeInterval();
                                    interval.EndTime = timeStamp;
                                    dataPoint.Interval = interval;
                                    // Prepare custom metric.
                                    Metric metric = new Metric();
                                    metric.Type = "custom.googleapis.com/" + perfcounters[i, 0, 1,0];
                                    metric.Labels.Add("counter", perfcounters[i, 0, 1,0]);

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
                                       
                                    }
                                    catch
                                    {
                                        EventLog.WriteEntry("GCPAnyMetric", "Error occurred in processing metrics", EventLogEntryType.Error, 1, 1);
                                        //Thread.Sleep(180000);
                                        OK = true;
                                    }
                                    
                                }
                            }
                            Thread.Sleep(30000);


                        }
                    }
                    else
                    {
                        Console.WriteLine("GCPAnyMetric failed to start as it is not configured");
                        EventLog.WriteEntry("GCPanyMetric","GCPAnyMetric failed to start as it is not configured", EventLogEntryType.Error, 1, 1);
                    }
                        System.Environment.Exit(0);
                }
                else
                {
                   
                    Console.WriteLine("GCPAnyMetric is not installed correctly");
                    EventLog.WriteEntry("GCPanyMetric", "GCPAnyMetric failed to start the installation is incomplete", EventLogEntryType.Error, 1, 1);
                    System.Environment.Exit(0);
                }
                               
                
               
            

                
            }
        }
    }
}
