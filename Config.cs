using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace dome_bt
{
	public class Config
	{
		private string FileName;
		private Dictionary<string, string> Dictionary;

		public Config(string filename)
		{
			FileName = filename;
			Dictionary = new Dictionary<string, string>();

			Load();
		}

		public void Set(string key, string value)
		{
			if (Dictionary.ContainsKey(key) == true)
				Dictionary[key] = value;
			else
				Dictionary.Add(key, value);

			Save();
		}

		public bool ContainsKey(string key)
		{
			return Dictionary.ContainsKey(key);
		}

		public string Get(string key)
		{
			if (Dictionary.ContainsKey(key) == true)
				return Dictionary[key];
			else
				return null;
		}

		private void Load()
		{
			if (File.Exists(FileName) == true)
			{
				using (StreamReader reader = new StreamReader(FileName, Encoding.UTF8))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						line = line.Trim();

						if (line.Length == 0 || line.StartsWith("#") == true)
							continue;

						string[] parts = line.Split('\t');
						if (parts.Length != 2)
							throw new ApplicationException($"Bad config line: {line}");

						Dictionary.Add(parts[0].ToLower(), parts[1]);
					}
				}
			}
		}

		private void Save()
		{
			StringBuilder output = new StringBuilder();

			foreach (string key in Dictionary.Keys)
				output.AppendLine($"{key}\t{Dictionary[key]}");

			File.WriteAllText(FileName, output.ToString(), Encoding.UTF8);
		}
	}
}
