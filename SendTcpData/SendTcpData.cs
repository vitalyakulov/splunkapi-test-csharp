using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using SplunkTest;

namespace SendTcpData
{
	public class TcpConnection
	{
		private int _port { get; set; }
		private string _serverName { get; set; }
		private TcpClient _client { get; set; }

		public TcpConnection(string serverName, int port)
		{
			this._serverName = serverName;
			this._port = port;
		}

		public void Connect()
		{
			for (int retry = 0; retry < 10; retry++)
			{
				try
				{
					// Create a TcpClient. 
					_client = new TcpClient(this._serverName, this._port);
					return;
				}
				catch
				{
					Thread.Sleep(5000);
				}
			}
			_client = new TcpClient(this._serverName, this._port);
		}

		public void Send(string message)
		{
			// Translate the passed message into ASCII and store it as a Byte array.
			Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);

			// Get a client stream for reading and writing. 
			NetworkStream stream = _client.GetStream();

			// Send the message to the connected TcpServer. 
			stream.Write(data, 0, data.Length);
		}

		public string Receive(int maxSize)
		{
			// Receive the TcpServer.response. 
			// Get a client stream for reading and writing. 
			NetworkStream stream = _client.GetStream();

			// Buffer to store the response bytes.
			Byte[] data = new Byte[maxSize];

			// String to store the response ASCII representation.
			String responseData = String.Empty;

			// Read the first batch of the TcpServer response bytes.
			Int32 bytes = stream.Read(data, 0, data.Length);
			responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
			return responseData;
		}

		public void Close()
		{
			// Get a client stream for reading and writing. 
			NetworkStream stream = _client.GetStream();

			// Close everything.
			stream.Close();
			_client.Close();
		}
	}

	class SendTcpData
	{
		static void SendData(int order, string serverName, int portNumber, int numberOfEventsPerThread)
		{
			IntPtr connectedEvent = InteropEvents.CreateEvent(IntPtr.Zero, true, false, string.Format("thread{0}connected", order));
			TcpConnection connection = new TcpConnection(serverName, portNumber);
			connection.Connect();
			InteropEvents.SetEvent(connectedEvent);

			IntPtr startEvent = InteropEvents.OpenEvent(InteropEvents.EVENT_ALL_ACCESS | InteropEvents.EVENT_MODIFY_STATE, true, "startevent");
			uint waitResult = InteropEvents.WaitForSingleObject(startEvent, -1);

			// Generate data and send it down the road

			var r = new Random();
			DateTime time = new DateTime(1971 + order * 10, 9, 12);
			for (int i = 0; i < numberOfEventsPerThread; i++)
			{
				var line = new StringBuilder();
				for (int j = 0; j < 60; j++)
				{
					line.AppendFormat("field{0}={0}, ", j, r.Next(1000000));
				}
				line.Length -= 2;
				connection.Send(string.Format("{0} {1}\n", time, line));
				time = time.AddSeconds(1);
			}

			connection.Close();
			//Console.WriteLine("{0} Thread {1} is done with sending data", DateTime.Now, order);
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
			IntPtr startEvent = InteropEvents.CreateEvent(IntPtr.Zero, true, false, "startevent");

			// Launch X concurrent threads
			Thread[] threads = new Thread[numberOfThreads];
			for (int i = 0; i < numberOfThreads; i++)
			{
				int order = i;
				ThreadStart start = delegate { SendData(order, serverName, portNumber, numberOfEventsPerThread); };
				Thread t = new Thread(start);
				t.Name = string.Format("Thread {0}", i);
				t.Start();
				threads[i] = t;
			}

			Console.WriteLine("{0} Started all threads", DateTime.Now);
			Thread.Sleep(5000);
			Console.WriteLine("{0} Waiting for all threads to get connected", DateTime.Now);
			// Wait for all threads to connect
			for (int i = 0; i < numberOfThreads; i++)
			{
				IntPtr handle = InteropEvents.OpenEvent(InteropEvents.EVENT_ALL_ACCESS | InteropEvents.EVENT_MODIFY_STATE, true, string.Format("thread{0}connected", i));
				uint waitResult = InteropEvents.WaitForSingleObject(handle, -1);
			}
			Console.WriteLine("{0} All threads connected", DateTime.Now);
			InteropEvents.SetEvent(startEvent);
			Console.WriteLine("{0} Fired starting event", DateTime.Now);

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