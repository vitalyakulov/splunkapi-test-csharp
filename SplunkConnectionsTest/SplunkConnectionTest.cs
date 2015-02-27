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


	internal class SplunkTest
	{
		private static  long _connectionSucceeded = 0;
		private static  long _connectionFailed = 0;

		private static void Main(string[] args)
		{
			DateTime tStart = DateTime.Now;
			Parallel.For(0, 50000000, i =>
				{
					string s = i.ToString();
					int j = Convert.ToInt32(s);
					if (i != j)
					{
					}
				});
			DateTime tEnd = DateTime.Now;
			TimeSpan elapset = tEnd - tStart;

			for (int i = 0; i < 10000000; i++)
			{
				string s = i.ToString();
				int j = Convert.ToInt32(s);
				if (i != j)
				{
				}
			}
			Console.WriteLine("Usage: <hostName> <Number of threads> <Number of connections per thread>");
			string hostName = args.Length > 0 ? args[0] : "192.168.85.171";
			int threads = args.Length > 1 ? Convert.ToInt32(args[1]) : 1000;
			int numberOfConnectionsPerThread = args.Length > 2 ? Convert.ToInt32(args[2]) : 200;
			Console.WriteLine("Host name:{0}, number of threads: {1}, number of connections per thread:{2}", hostName, threads, numberOfConnectionsPerThread);

			// Launch X concurrent connections
			Thread tShow = new Thread(ShowStatus);
			tShow.Start();

			List<Thread> threadList = new List<Thread>();
			for (int i = 0; i < threads; i++)
			{
				Thread t = new Thread(ConnectionTest, 1);
				t.Start(new object[] { hostName, numberOfConnectionsPerThread, i });
				threadList.Add(t);
			}
	
			foreach (Thread t in threadList)
			{
				t.Join();
			}

			tShow.Abort();
			tShow.Join();
			Console.WriteLine();
		}

		private static void ShowStatus()
		{
			try
			{
				DateTime tStart = DateTime.Now;
				while (true)
				{
					TimeSpan elapsedTime = DateTime.Now - tStart;
					long totalCalls = _connectionSucceeded + _connectionFailed;
					string passRate = totalCalls > 0 ? Convert.ToString(100.0 * (_connectionSucceeded / totalCalls)) : "XX";
					string callRate = elapsedTime.TotalSeconds > 0 ? Convert.ToString(_connectionSucceeded / elapsedTime.TotalSeconds) : "XX";
					Console.Write("\r{0} {1} % ({2:D4}/{3:D4}). Connection rate {4} per sec                 ",
						DateTime.Now, passRate, _connectionSucceeded, totalCalls, callRate);
					Thread.Sleep(TimeSpan.FromSeconds(5));
				}
			}
			catch (ThreadAbortException)
			{
			}
			catch (ThreadInterruptedException)
			{
			}
		}
		private static void ConnectionTest(object info)
		{
			object[] arguments = info as object[];
			string hostName = (string)arguments[0];
			int numberOfAttempts = (int)arguments[1];
			int threadId = (int)arguments[2];

			try
			{
				var splunk = new SplunkApi(hostName, "admin", "admin");
				long connectionMade = 0,connectionFailed = 0;

				// Do X number of connections
				for (int i = 0; i < numberOfAttempts; i++)
				{
					// Batch status update to avoid spinlocks
					if (i % 50 == 0)
					{
						Interlocked.Add(ref _connectionSucceeded, connectionMade);
						Interlocked.Add(ref _connectionFailed, connectionFailed);
						connectionMade = connectionFailed = 0;
					}
					DateTime tStart = DateTime.Now;
					try
					{
						splunk.Connect();
						connectionMade++;
					}
					catch (Exception)
					{
						connectionFailed++;
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine();
				Auxilary.WriteLineColor(ConsoleColor.Red, "{0} [Thread {1:D4}] caught an exception '{2}'. Exiting thread", DateTime.Now, threadId, e.Message);
			}
		}
	}
}