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

	internal class SplunkTest
	{
		private static Random _random = new Random();
		private static Object _searchCounterLock = new Object();
		private static long _numberOfSearches = 0;
		private static double _searchTimeSummarySec = 0;
		private static Queue searchQueries = new Queue();

		private static void Main(string[] args)
		{
			bool saveResult = false;
			Console.WriteLine("Usage: [/SAVERESULT] <hostName> <Number of threads> <Number of searches per thread> <portNumber> <query to run>");
			int argIndex = 0;
			if (args.Length > 0 && (args[0].ToLowerInvariant() == "/saveresult"))
			{
				saveResult = true;
				argIndex = 1;
			}
			string hostName = args.Length > 0 ? args[0 + argIndex] : "VAKULOV-VM1";
			int threads = argIndex + args.Length > 1 ? Convert.ToInt32(args[1 + argIndex]) : 10;
			int searches = argIndex + args.Length > 2 ? Convert.ToInt32(args[2 + argIndex]) : 10;
			int port = argIndex + args.Length > 3 ? Convert.ToInt32(args[3 + argIndex]) : 8089;
			string defaultQuery = argIndex + args.Length > 4 ? args[4 + argIndex] : null;
			Console.WriteLine("Host name:{0}, number of threads: {1}, number of searches:{2}, port number:{3}, query:'{4}'", hostName, threads, searches, port, defaultQuery);

			// Generate queries
			for (int i = 0; i < threads * searches; i++)
			{
				if (defaultQuery == null)
					searchQueries.Enqueue(GenerateRandomQuery());
				else
					searchQueries.Enqueue(defaultQuery);
			}

			// Launch X concurrent searches
			DateTime tStart = DateTime.Now;
			// Parallel.For(0, threads, i => SingleSearch(hostName, searches, port, defaultQuery, saveResult));
			List<Thread> tSearches = new List<Thread>(threads);
			for(int i=0;i<threads;i++)
			{
				Thread t = new Thread(new ThreadStart(delegate { SingleSearch(hostName, searches, port, defaultQuery, saveResult); }));
				t.Name=string.Format("Thread {0,4}", i);
				t.Start();
				tSearches.Add(t);
			}

			foreach (Thread t in tSearches)
			{
				t.Join();
			}
			TimeSpan elapsedTime = DateTime.Now - tStart;
			Console.WriteLine("It took {0:F2} seconds overall to complete operations. Search time for {1} searches was {2:F2} seconds, average search time is {3:F2} seconds", elapsedTime.TotalSeconds, _numberOfSearches, _searchTimeSummarySec, (_searchTimeSummarySec / _numberOfSearches));
		}

		private static string GenerateRandomQuery()
		{
			string result = string.Empty;
			int rndValue, subQuery;
			lock (_random)
			{
				rndValue = _random.Next(1000000);
				subQuery = _random.Next(5);
			}

			switch (subQuery)
			{
				case 0:
					// empty search string
					break;
				case 1:
					// Single number.
					result = string.Format("{0}", rndValue);
					break;
				case 2:
					// number OR number
					result = string.Format("({0} OR {1})", rndValue, GenerateRandomQuery());
					break;
				case 3:
					// number AND number
					result = string.Format("({0} AND {1})", rndValue, GenerateRandomQuery());
					break;
				case 4:
					// number*
					result = string.Format("{0}*", rndValue);
					break;
			}

			return result;
		}
		private static string GetQuery(string defaultQuery)
		{
			string query = null;
			lock (searchQueries.SyncRoot)
			{
				if (searchQueries.Count > 0)
					query = (string)searchQueries.Dequeue();
				else
					query = null;
			}
			//if (defaultQuery == null)
			//    query = GenerateRandomQuery();
			//else
			//    query = defaultQuery;
			return query;
		}

		private static void SingleSearch(string hostName, int searches,int port, string defaultQuery, bool saveResult)
		{
			try
			{
				var splunk = new SplunkApi(hostName, "admin", "notchangeme",port);
				splunk.Connect();

				int searchTimeOutInMinutes = (defaultQuery == null) ? 10 : 5;
				// Do X number of searches
				string query = GetQuery(defaultQuery);
				while(query != null)
				{
					try
					{
						DateTime tStart=DateTime.Now;
						splunk.Search(query, saveResult, searchTimeOutInMinutes);
						DateTime tEnd=DateTime.Now;
						lock (_searchCounterLock)
						{
							_numberOfSearches++;
							TimeSpan ts = tEnd - tStart;
							_searchTimeSummarySec += ts.TotalSeconds;
						}
					}
					catch (Exception e)
					{
						ConsoleColor c = Console.ForegroundColor;
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine("--- One of the threads '{2}' caught an exception {0}, {1} searches remaining", e.Message, searchQueries.Count, Thread.CurrentThread.Name);
						Console.ForegroundColor = c;
					}
					query = GetQuery(defaultQuery);
				}
			}
			catch (Exception e)
			{
				ConsoleColor c = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("--- One of the threads caught an exception {0}.Exiting thread '{1}'", e.Message, Thread.CurrentThread.Name);
				Console.ForegroundColor = c;
			}
		}
	}
}