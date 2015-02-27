using System;
using System.Diagnostics;

namespace CreateCustomLog
{
	public class ClsEventLog
	{
		public void DeleteLog(string sourceName)
		{
			EventLog.DeleteEventSource(sourceName);
		}

		public void CreateLog(string sourceName, string logName)
		{
			EventLog.CreateEventSource(sourceName, logName);
			this.WriteToEventLog(sourceName, logName, 1, string.Format("Event log '{0}' was successfully created", sourceName));
		}

		public void WriteToEventLog(string sourceName, string logName, int counter, string message)
		{
			using (EventLog myEventLog = new EventLog())
			{
				for (int i = 0; i < counter; i++)
				{
					myEventLog.Source = sourceName;
					myEventLog.Log = logName;
					myEventLog.WriteEntry(message, EventLogEntryType.Information);
				}
			}
		}
	}

	class CreateCustomLog
	{
		static void ShowUsage()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("\tCreate <eventSourceName> <eventLogName>");
			Console.WriteLine("\tDelete <eventSourceName>");
			Console.WriteLine("\tWrite <number of copies> <eventSourceName> <eventLogName> <message>");
		}
		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				ShowUsage();
				return;
			}
			ClsEventLog cls = new ClsEventLog();
			switch (args[0].ToLowerInvariant())
			{
				case "create":
					cls.CreateLog(args[1], args[2]);
					Console.WriteLine("Event log '{0}' was successfully created", args[1]);
					break;
				case "delete":
					cls.DeleteLog(args[1]);
					Console.WriteLine("Event log '{0}' was successfully deleted", args[1]);
					break;
				case "write":
					int counter = Convert.ToInt32(args[1]);
					cls.WriteToEventLog(args[2], args[3], counter, args[4]);
					Console.WriteLine("Wrote {0} messages to event log '{1}'", args[1], args[2]);
					break;
				default:
					throw new ArgumentOutOfRangeException(string.Format("Unknown argument '{0}'", args[0]));
			}
		}
	}
}