using System;
using System.Net;
using System.Threading;
using System.Xml;
using SplunkTest;
using System.IO;

namespace CreateSlowConnections
{
	class CreateSlowConnections
	{
		static void CreateConnection(SplunkApi splunk, int numberOfEventsPerThread)
		{
			string url = string.Format("{0}/services/data/inputs", splunk.BaseUrl);

			for (int i = 0; i < numberOfEventsPerThread; i++)
			{
				var request = WebRequest.Create(url) as HttpWebRequest;
				request.Method = "GET";
				request.ContentType = "application/x-www-form-urlencoded";
				request.Headers.Add("Authorization", "Splunk " + splunk.SessionKey);
				// Pick up the response:

				using (var response = request.GetResponse() as HttpWebResponse)
				{
					using (var reader = new StreamReader(response.GetResponseStream()))
					{
						if (!reader.EndOfStream)
						{
							string result = reader.ReadToEnd();
						}
					}
				}
				Thread.Sleep(9000);
			}
		}

		static void ShowUsage()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("\t<Servername> <PortNumber> <numberOfThreads> <numberOfEventsPerThread>");
		}

		static void Main(string[] args)
		{
			if (args.Length < 4)
			{
				ShowUsage();
				return;
			}

			// Parse the arguments
			string serverName = args[0];
			int portNumber = Convert.ToInt32(args[1]);
			int numberOfThreads = Convert.ToInt32(args[2]);
			int numberOfEventsPerThread = Convert.ToInt32(args[3]);

			SplunkApi[] api = new SplunkApi[numberOfThreads];
			// Launch X concurrent threads
			Thread[] threads = new Thread[numberOfThreads];
			for (int i = 0; i < numberOfThreads; i++)
			{
				SplunkApi splunk = new SplunkApi(serverName, "admin", "changeme", portNumber);
				splunk.Connect();
				api[i] = splunk;
				ThreadStart start = delegate { CreateConnection(splunk, numberOfEventsPerThread); };
				Thread t = new Thread(start);
				t.Name = string.Format("Thread {0}", i);
				t.Start();
				threads[i] = t;
			}
			Console.WriteLine("{0} Started all threads", DateTime.Now);
			DateTime tStart = DateTime.Now;
			for (int i = 0; i < numberOfThreads; i++)
			{
				threads[i].Join();
			}
			DateTime tEnd = DateTime.Now;
			Console.WriteLine("It took {0:F2} seconds to send {1} events", (tEnd - tStart).TotalSeconds, numberOfThreads * numberOfEventsPerThread);
		}
	}
}
