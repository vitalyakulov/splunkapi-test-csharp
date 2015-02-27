using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using System.IO;

namespace SplunkTest
{
	class HttpInputGenerator
	{
		volatile static string token = "xxx";
		private static Random rnd = new Random();
		static void DeleteToken(SplunkApi splunk, string tokenName)
		{
			try
			{
				splunk.HttpDelete(string.Format("{0}/servicesNS/admin/system/data/inputs/http/{1}",  splunk.BaseUrl, tokenName));
			}
			catch (System.Net.WebException)
			{
			}
		}
		static string CreateToken(SplunkApi splunk, string tokenName)
		{
			try
			{
				var xmlDoc = splunk.HttpPost(splunk.BaseUrl + "/servicesNS/admin/system/data/inputs/http", new[] { "name", "description", "index", "indexes" }, new[] { tokenName, string.Format("description from thread '{0}'", Thread.CurrentThread.Name), "main", "main" });
				XmlNode node = xmlDoc.LastChild;
				while (node.LastChild != null)
					node = node.LastChild;
				return node.InnerText;
			}
			catch (System.Net.WebException)
			{
			}
			return "";
		}
		static bool SendData(SplunkApi splunk, string token, string postData)
		{
			WebRequest request = WebRequest.Create(splunk.BaseUrl + "/services/logging");
			request.Method = "POST";
			request.Headers.Add("Authorization", "Splunk " + token);
			byte[] byteArray = Encoding.UTF8.GetBytes(postData);
			request.ContentType = "application/x-www-form-urlencoded";
			request.ContentLength = byteArray.Length;
			using (Stream dataStream = request.GetRequestStream())
			{
				dataStream.Write(byteArray, 0, byteArray.Length);
			}

			try
			{
				using (WebResponse response = request.GetResponse())
				{
					string status = ((HttpWebResponse)response).StatusDescription;
					if (status != "OK")
					{
						Console.WriteLine("{1}\tFailed to insert data. Status is {0}", status, DateTime.Now);
						System.Environment.Exit(-1);
					}
					using (Stream dataStream = response.GetResponseStream())
					{
						using (StreamReader reader = new StreamReader(dataStream))
						{
							string responseFromServer = reader.ReadToEnd();
						}
					}
				}
			}
			catch (System.Net.WebException)
			{
				return false;
			}
			return true;
		}
		static void tokenAddRemove(SplunkApi splunk, string tokenName, int durationInSec)
		{
			var tStart = DateTime.Now;
			int i = 0;
			while ((DateTime.Now - tStart).TotalSeconds < durationInSec)
			{
				// Sleep 0.5 - 5 sec
				Thread.Sleep(rnd.Next(50, 500));
				token = CreateToken(splunk, tokenName);
				// Sleep 0.5 - 5 sec
				Thread.Sleep(rnd.Next(50, 500));
				DeleteToken(splunk, tokenName);
				i++;
				if (i % 5 == 0)
					Console.WriteLine("{0}\t{1} tokens were created in thread '{2}'", DateTime.Now, i, Thread.CurrentThread.Name);
			}
			Console.WriteLine("{0}\tTokens creation completed, total {1} were created in thread '{2}'", DateTime.Now, i, Thread.CurrentThread.Name);
		}
		static void GenerateData(SplunkApi splunk, int durationInSec)
		{
			var tStart = DateTime.Now;
			int i = 0;
			while ((DateTime.Now - tStart).TotalSeconds < durationInSec)
			{
				bool result = SendData(splunk, token, "{}");
				
				if (result)
				{
					i++;
					if (i % 500 == 0)
						Console.WriteLine("{0}\t{1} events were sent in thread '{2}'", DateTime.Now, i, Thread.CurrentThread.Name);
				}
			}
			Console.WriteLine("{0}\t{1} Data generation completed in thread '{2}'. Events were sent total.", DateTime.Now, i, Thread.CurrentThread.Name);
		}
		static void Main(string[] args)
		{
			string hostName = args.Length > 0 ? args[0] : "127.0.0.1";
			string user = args.Length > 1 ? args[1] : "admin";
			string password = args.Length > 2 ? args[2] : "notchangeme";
			int threadCount = args.Length > 3 ? Convert.ToInt32(args[3]) : 50;
			int durationInSec = args.Length > 4 ? Convert.ToInt32(args[4]) : 300;

			var splunk = new SplunkApi(hostName, user, password, 8089);
			Console.WriteLine("{0}\tStarted", DateTime.Now);
			string tokenName = "testtoken";
			splunk.Connect();
			Console.WriteLine("{0}\tConnected", DateTime.Now);
			// Delete token if it existed
			DeleteToken(splunk, tokenName);
			Console.WriteLine("{0}\tToken deleted", DateTime.Now);
			List<Thread> bgThreads = new List<Thread>();
			bgThreads.Add(new Thread(new ThreadStart(delegate { tokenAddRemove(splunk, tokenName, durationInSec); })));

			for (int i = 0; i < threadCount; i++)
			{
				string tname = string.Format("token{0}", i);
				var t = new Thread(new ThreadStart(delegate() { tokenAddRemove(splunk, tname, durationInSec); }));

				t.Name = string.Format("addremove thread {0}", i);
				bgThreads.Add(t);
			}
			for (int i = 0; i < threadCount/5; i++)
			{
				var t = new Thread(new ThreadStart(delegate { GenerateData(splunk, durationInSec); }));
				t.Name = string.Format("Thread {0}", i);
				bgThreads.Add(t);
			}
			foreach (var t in bgThreads)
			{
				t.Start();
			}
			foreach (var t in bgThreads)
			{
				t.Join();
			}
			Console.WriteLine("{0}\tAll data inserted", DateTime.Now);
		}
	}
}
