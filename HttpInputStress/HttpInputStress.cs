//-----------------------------------------------------------------------
// <copyright file="SplunkTest.cs" company="VitalyAkulov">
//     Stress test to crash Splunk server. Copyright by Vitaly Akulov.
// </copyright>
//-----------------------------------------------------------------------

namespace SplunkTest
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Collections;
    using System.Xml;
    using System.Text;

    internal class SplunkTest
    {
        private static Random _random = new Random();
        private static Object _searchCounterLock = new Object();
        private static long totalAddedEvents = 0;
        private static long totalFailedEvents = 0;

        static SplunkApi getConnectedSplunk(string hostName, int durationInSec = 120)
        {
            bool useSSL = true;
            int startIndex = 0;
            if (hostName.StartsWith("http://"))
            {
                useSSL = false;
                startIndex = hostName.IndexOf(':') + 3;
            }
            else if (hostName.StartsWith("https://"))
            {
                startIndex = hostName.IndexOf(':') + 3;
            }
            hostName = hostName.Substring(startIndex);
            var splunk = new SplunkApi(hostName, "admin", "changeme", useSSL: useSSL);

            var tStart = DateTime.Now;
            while ((DateTime.Now - tStart).TotalSeconds < durationInSec)
            {
                try
                {
                    splunk.Connect();
                    return splunk;
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
            Console.WriteLine("Failed to connect to {0} in {1} seconds. Exiting ...", hostName, durationInSec);
            Environment.Exit(1);
            return null;
        }
        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: [-hostname:<hostName>] [-threads:<Number of threads>] [-testduration:<test duration in seconds>] [-token:<http token>] [-datasize:<size in bytes>] [-waitforindexcompletion:<any value is true>]");
                Console.WriteLine("Missing arguments");
                return;
            }
            string hostName = string.Empty;
            int threads = 1;
            int testDuration = 60;
            string token = string.Empty;
            bool waitForIndexCompletion = false;
            int dataSize = 100;
            foreach (string arg in args)
            {
                string[] words = arg.Split(':');
                switch (words[0].ToLower())
                {
                    case "-hostname":
                        hostName = words[1];
                        if (words.Length > 2)
                            hostName = hostName + ":" + words[2];
                        break;
                    case "-threads":
                        threads = Convert.ToInt32(words[1]);
                        break;
                    case "-testduration":
                        testDuration = Convert.ToInt32(words[1]);
                        break;
                    case "-token":
                        token = words[1];
                        break;
                    case "-waitforindexcompletion":
                        waitForIndexCompletion = true;
                        break;
                    case "-datasize":
                        dataSize = Convert.ToInt32(words[1]);
                        break;
                    default:
                        Console.WriteLine("Unknown argument '{0}', exiting", words[0]);
                        System.Environment.Exit(1);
                        break;
                }
            }

            // Validate input parameters
            if (string.IsNullOrEmpty(hostName))
            {
                Console.WriteLine("Unspecified host name, exiting");
                System.Environment.Exit(1);
            }
            var splunk = getConnectedSplunk(hostName);
            string tokenName = "inputstresstest";
            if (string.IsNullOrEmpty(token))
            {
                splunk.EnableHttpInput();
                splunk.DeleteHttpToken(tokenName);
                token = splunk.CreateHttpToken(tokenName);
            }
            Console.WriteLine("Will run test for {0} seconds using {1} threads to server: '{2}', packet size {3} bytes", testDuration, threads, hostName, dataSize);
            List<Thread> tSenders = new List<Thread>(threads);
            DateTime tStart = DateTime.Now;
            for (int i = 0; i < threads; i++)
            {
                Thread t = new Thread(new ThreadStart(delegate { SendDataViaHttpInput(splunk, hostName, token, testDuration, dataSize); }));
                t.Name = string.Format("Thread {0,4}", i);
                t.Start();
                tSenders.Add(t);
            }
            foreach (Thread t in tSenders)
            {
                t.Join();
            }
            TimeSpan elapsedTime = DateTime.Now - tStart;
            Console.WriteLine("It took {0:F2} seconds overall to generate data, {4:F2} events/sec. {1} ({3} failed) events were generated by {2} threads", elapsedTime.TotalSeconds, totalAddedEvents, threads, totalFailedEvents, totalAddedEvents / elapsedTime.TotalSeconds);
            // Remove after debugging
            if (!waitForIndexCompletion)
                return;
            bool showSearchOutput = true;
            // Now wait for index to complete
            XmlDocument searchResults = splunk.Search("*|stats count", false, 10, showStats: showSearchOutput);
            var context = new XmlNamespaceManager(searchResults.NameTable);
            int eventCount = int.Parse(searchResults.SelectSingleNode("/results/result/field[@k='count']/value", context).InnerText);
            for (int i = 0; i < 4; i++)
            {
                do
                {
                    if (showSearchOutput)
                        Console.WriteLine("\tWaiting for indexing to complete, {0} events so far", eventCount);
                    Thread.Sleep(10000);
                    searchResults = splunk.Search("*|stats count", false, 10, showStats: showSearchOutput);
                    context = new XmlNamespaceManager(searchResults.NameTable);
                    int updatedEventCount = int.Parse(searchResults.SelectSingleNode("/results/result/field[@k='count']/value", context).InnerText);
                    if (updatedEventCount == eventCount)
                        break;
                    eventCount = updatedEventCount;
                } while (true);
                if (showSearchOutput)
                    Console.WriteLine("\tCompleted wait for iteration {0}", i);
            }
        }

        private static void SendDataViaHttpInput(SplunkApi splunk, string hostName, string token, int durationInSec, int dataSize)
        {
            long addedEvents = 0, failedEvents = 0;
            var tStart = DateTime.Now;
            int i = 0;
            string messageStart = "{ \"event\": { \"data\": \"Thread: " + Thread.CurrentThread.Name + ", event: ";
            string filer = new string('*', dataSize - messageStart.Length - 19);
            StringBuilder sb = new StringBuilder();
            while ((DateTime.Now - tStart).TotalSeconds < durationInSec)
            {
                sb.Clear();
                sb.Append(messageStart);
                sb.Append(" ");
                sb.Append(i);
                sb.Append(", filler:'");
                sb.Append(filer);
                sb.Append("' \" } }");
                try
                {
                    bool result = splunk.SendDataViaHttp(token, sb.ToString());
                    if (result)
                        addedEvents++;
                    i++;
                }
                catch (Exception)
                {
                    failedEvents++;
                }
            }
            Interlocked.Add(ref totalAddedEvents, addedEvents);
            Interlocked.Add(ref totalFailedEvents, failedEvents);
        }
    }
}