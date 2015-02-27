using System;
using System.IO;
using System.Text;

namespace GenerateData
{
	class GenerateData
	{
		static void Main()
		{
			//GenerateData2("splunkData2.txt", 1200000 * 2);
			GenerateData3("splunkData3.txt", 1200000 * 4);
		}

		private static void GenerateData2(string fileName, int linesToInsert)
		{
			if (File.Exists(fileName))
			{
				File.Delete(fileName);
			}
			using (StreamWriter w = File.CreateText(fileName))
			{
				var r = new Random();
				DateTime time = new DateTime(1971, 9, 12);
				for (int i = 0; i < linesToInsert; i++)
				{
					var line = new StringBuilder();
					for (int j = 0; j < 60; j++)
					{
						line.AppendFormat("{0} ", r.Next(1000000));
					}
					line.Length -= 1;
					w.WriteLine("{0} {1}", time, line);
					time = time.AddSeconds(1);
				}
			}
		}

		private static void GenerateData3(string fileName, int linesToInsert)
		{
			using (StreamWriter w = File.CreateText(fileName))
			{
				var r = new Random();
				DateTime time = new DateTime(1971, 9, 12);
				for (int i = 0; i < linesToInsert; i++)
				{
					w.WriteLine("{0} {1}", time, r.Next(1000000));
					time = time.AddSeconds(1);
				}
			}
		}
	}
}
