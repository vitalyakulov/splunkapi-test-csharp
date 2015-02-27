//-----------------------------------------------------------------------
// <copyright file="SplunkTest.cs" company="VitalyAkulov">
//     Stress test to crash Splunk server. Copyright by Vitaly Akulov.
// </copyright>
//-----------------------------------------------------------------------

namespace SplunkTest
{
	using System;
	using System.Threading.Tasks;
	using System.Collections.Generic;
	using System.Threading;

	internal class SplunkTest
	{
		private static Random _random = new Random();
		private static string hostName;
		private static int threads, searches;


		private static void Main(string[] args)
		{
			Console.WriteLine("Usage: <hostName> <Number of threads> <Number of searches per thread>");
			hostName = args.Length > 0 ? args[0] : "192.168.85.171";
			threads = args.Length > 1 ? Convert.ToInt32(args[1]) : 1000;
			searches = args.Length > 2 ? Convert.ToInt32(args[2]) : 1000;
			Console.WriteLine("Host name:{0}, number of threads: {1}, number of searches:{2}", hostName, threads, searches);

			// Launch X concurrent searches
			List<Thread> tList = new List<Thread>();
			for (int i = 0; i < threads; i++)
			{
				Thread t = new Thread(SingleSearch);
				t.Start();
				tList.Add(t);
			}

			foreach (Thread t in tList)
			{
				t.Join();
			}
		}

		private static void SingleSearch()
		{
			try
			{
				var splunk = new SplunkApi(hostName, "admin", "admin");
				int numberOfConnections = 0;
				// Do X number of searches
				for (int i = 0; i < searches; i++)
				{
					try
					{
						splunk.Connect();
						numberOfConnections++;
						if (i % 50 == 0)
						{
							Auxilary.WriteLineColor(ConsoleColor.Gray, "{0} {1} connections made so far, {2} remaining", DateTime.Now, numberOfConnections, searches - i);
						}
					}
					catch (Exception e)
					{
						ConsoleColor c = Console.ForegroundColor;
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine("--- One of the threads caught an exception {0}, {1} searches remaining", e.Message, searches - i);
						Console.ForegroundColor = c;
					}
				}
			}
			catch (Exception e)
			{
				ConsoleColor c = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("--- One of the threads caught an exception {0}.Exiting thread", e.Message);
				Console.ForegroundColor = c;
			}
		}
	}
}