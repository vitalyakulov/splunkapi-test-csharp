using System;

namespace SplunkTest
{
	public class Auxilary
	{
		private static object _lockColor = new object();
		public static void WriteLineColor(ConsoleColor color, string format, params object[] arg)
		{
			lock (_lockColor)
			{
				ConsoleColor c = Console.ForegroundColor;
				Console.ForegroundColor = color;
				Console.WriteLine(format, arg);
				Console.ForegroundColor = c;
			}
		}

	}
}
