
using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Globalization;

// external references
using Mono.Unix;
using ServiceStack.Text;
using System.Threading;

namespace LogstashPurge
{
    class MainClass
    {

        public static void Main(string[] args)
        {
            bool _shutDown = false;
            bool _isMono;
            Thread signal_thread = null;

            _isMono = Type.GetType("Mono.Runtime") != null;

            if (_isMono)
            {
                //http://unixhelp.ed.ac.uk/CGI/man-cgi?signal+7
                UnixSignal[] signals;
                signals = new UnixSignal[] { 
                //new UnixSignal (Mono.Unix.Native.Signum.SIGHUP),
                new UnixSignal (Mono.Unix.Native.Signum.SIGINT),
                //new UnixSignal (Mono.Unix.Native.Signum.SIGQUIT),
                new UnixSignal (Mono.Unix.Native.Signum.SIGABRT),
                //new UnixSignal (Mono.Unix.Native.Signum.SIGKILL),
                new UnixSignal (Mono.Unix.Native.Signum.SIGTERM),
                //new UnixSignal (Mono.Unix.Native.Signum.SIGSTOP),
                new UnixSignal (Mono.Unix.Native.Signum.SIGTSTP)
            };



                signal_thread = new Thread(delegate()
                {
                    while (!_shutDown)
                    {
                        // Wait for a signal to be delivered
                        int index = UnixSignal.WaitAny(signals, -1);
                        Mono.Unix.Native.Signum signal = signals[index].Signum;
                        Console.WriteLine("shutdown signal recieved {0}" + signal.ToString());
                        _shutDown = true;
                    }
                });

            }

 

            Uri elasticSearchUrl;
            int daysToKeep = 30;
            if (args.Length == 0)
            {
                elasticSearchUrl = new Uri(Environment.GetEnvironmentVariable("elasticSearchUrl"));
                string environmentVariable = Environment.GetEnvironmentVariable("daysToKeep");
                if (environmentVariable != null)
                {
                    daysToKeep = int.Parse(environmentVariable);
                }


            }
            else
            {
                elasticSearchUrl = new Uri(args[0]);
                if (args.Length > 1)
                {
                    daysToKeep = int.Parse(args[1]);
                }
            }

            IPAddress ip = GetExternalIP();


            TimeSpan wait;
            while (!_shutDown)
            {
                Console.WriteLine("\n\nstarting purge from IP {0} at {1}", ip, DateTime.UtcNow);
                try
                {

                    var mapping = GetMappings(elasticSearchUrl);
                    var toDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day).Subtract(TimeSpan.FromDays(daysToKeep));
                    var toDateString = GetDayOnlyString(toDate);


                    mapping.Indices.Sort();
                    foreach (var i in mapping.Indices)
                    {

                        Console.WriteLine("checking index {0}", i);
                        if (IndexExists(elasticSearchUrl, i))
                        {
                            var count = GetCount(elasticSearchUrl, i, toDateString);
                            if (count != 0)
                            {
                                Console.WriteLine("deleting {0} records", count);
                                Uri uri = new Uri(elasticSearchUrl, i + "/_query");

                                string range = GetRangeString(toDateString);
                                var queryBytes = Encoding.UTF8.GetBytes(range);
                                /*string responseText = */
                                Console.WriteLine("delete query: {0}", range);
                                GetResponseText(uri, "DELETE", queryBytes);

                                //{"ok":true,"_indices":{"logstash-2013.07.21":{"_shards":{"total":4,"successful":4,"failed":0}}}}
                            }
                            else
                            {
                                Console.WriteLine("index {0} has no matching records", i);
                            }

                            // then clean it up if empty
                            if (DeleteIndexIfEmpty(elasticSearchUrl, i))
                            {
                                Console.WriteLine("deleted emtpy index {0}", i);
                            }
                        }
                        else
                        {
                            Console.WriteLine("index {0} does not exist", i);
                        }

                    }
                    wait = TimeSpan.FromDays(1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR:" + ex.ToString());
                    wait = TimeSpan.FromMinutes(1);
                }
                Console.WriteLine("\n\nwaiting {0} minutes until next purge", wait.TotalMinutes);
                Thread.Sleep(wait);
            }
            Console.WriteLine("shutting down");

            if (_isMono)
            {
                signal_thread.Join();
            }

            Console.WriteLine("shut down complete.");
        }

        public static bool DeleteIndexIfEmpty(Uri baseUri, string index)
        {
            var count = GetCount(baseUri, index, null);
            if (count == 0)
            {
                Uri uri = new Uri(baseUri, index);
                /*string responseText = */
                GetResponseText(uri, "DELETE", null);
                // {"ok":true,"acknowledged":true}
                return true;
            }
            else
            {
                return false;
            }
        }

        public static int GetCount(Uri baseUri, string index, string toDateString)
        {
            Uri uri = new Uri(baseUri, index + "/_count");

            byte[] queryBytes = null;
            if (!string.IsNullOrEmpty(toDateString))
            {
                string range = GetRangeString(toDateString);
                queryBytes = Encoding.UTF8.GetBytes(range);
            }


            string responseText = GetResponseText(uri, "POST", queryBytes);

            Dictionary<string, object> responseObject = JsonSerializer.DeserializeFromString<Dictionary<string, object>>(responseText);
            return int.Parse(responseObject["count"].ToString());

        }

        public static string GetRangeString(string toDateString)
        {
            return "{" +
                "    \"range\": {" +
                "      \"@timestamp\": {" +
                "        \"to\": \"" + toDateString + "\"" +
                "      }" +
                "    }" +
                "}";
        }

        public static string GetQueryString(string toDateString)
        {
            return "{" + GetRangeString(toDateString) + "}";

        }

        public static bool IndexExists(Uri uri, string index)
        {
            string method = "HEAD";
            var u = new Uri(uri, index + "/");

            try
            {
                GetResponseText(u, method, null);
                return true;
            }
            catch
            {
                return false;
            }

        }

        public static string GetResponseText(Uri uri, string method, byte[] queryBytes)
        {
            string responseText;
            var req = (HttpWebRequest)WebRequest.Create(uri);

            req.Method = method;
            if (method.ToLower() == "post" || method.ToLower() == "delete")
            {
                if (queryBytes != null)
                {
                    using (var reqStream = req.GetRequestStream())
                    {
                        reqStream.Write(queryBytes, 0, queryBytes.Length);
                    }
                }
            }

            using (var response = (HttpWebResponse)req.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception(string.Format("status {0}", response.StatusCode));
                }
                using (var resStream = response.GetResponseStream())
                {
                    using (var streamReader = new StreamReader(resStream))
                    {
                        responseText = streamReader.ReadToEnd();
                    }
                }
            }
            return responseText;
        }

        public static Mapping GetMappings(Uri uri)
        {

            string json = GetResponseText(new Uri(uri, "/_mapping?pretty"), "GET", null);



            Dictionary<string, Dictionary<string, object>> obj = JsonSerializer.DeserializeFromString<Dictionary<string, Dictionary<string, object>>>(json);

            var mapping = new Mapping();

            foreach (string k in obj.Keys)
            {
                if (!string.IsNullOrEmpty(k))
                {
                    if (k.StartsWith("logstash-"))
                    {
                        mapping.Indices.Add(k);
                        var index = (Dictionary<string, object>)obj[k];
                        foreach (string t in index.Keys)
                        {
                            if (!mapping.Types.Contains(t))
                            {
                                mapping.Types.Add(t);
                            }
                        }


                    }
                }
            }

            mapping.Types.Sort();
            return mapping;

        }

        public static string GetDayOnlyString(DateTime toDate)
        {
            //2013-09-06T00:00:00
            var date1 = new DateTime(toDate.Year, toDate.Month, toDate.Day);
            var ci = CultureInfo.InvariantCulture;
            var toDateString = date1.ToString("yyyy-MM-dd", ci) + "T00:00:00";
            return toDateString;
        }

        public static IPAddress GetExternalIP()
        {
            IPAddress ip = IPAddress.Parse(GetResponseText(new Uri("http://api.externalip.net/ip"), "GET", null));
            return ip;
        }
    }

    public class Mapping
    {
        public Mapping()
        {
            Indices = new List<string>();
            Types = new List<string>();
        }

        public List<string> Indices { get; set; }

        public List<string> Types { get; set; }
    }
}
