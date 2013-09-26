using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nest;

namespace PurgeBot
{
    internal class Program
    {
        private static Timer _timer;
        private static bool _isRunning;

        private static void Main(string[] args)
        {



            // how often to run the purge
            TimeSpan frequency;

            // the date up to witch to purge records, inclusively
            DateTime toDate;


            Uri uri;


            // high freq just to test
            frequency = TimeSpan.FromMinutes(2);
            //TimeSpan frequency = TimeSpan.FromDays(1);

            toDate = PurgeRunner.TruncateDateTime(DateTime.UtcNow.Subtract(TimeSpan.FromDays(30)), TimeSpan.TicksPerDay);

            //http://logsearch.cityindextest5.co.uk
            // danny needs to do some IP port forwarding for NEST as it uses a '/' call to check connection
            uri = new Uri("http://ec2-79-125-57-123.eu-west-1.compute.amazonaws.com:9200");



            var runner = new PurgeRunner(frequency, toDate, uri);

            runner.Start();

            Console.WriteLine("press enter to exit."); // i get perverse pleasure from writing that
            Console.ReadLine();

            runner.Stop();
            

        }

        
    }
}