using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace dome_bt
{
	internal class Program
	{
		static async Task<int> Main(string[] args)
		{
			return await MainAsync(args);
		}

		private static async Task<int> MainAsync(string[] args)
		{
			if (args.Length > 0 && args[0].Contains("=") == false)
				args[0] = $"operation={args[0]}";

			Dictionary<string, string> arguments = new Dictionary<string, string>();

			foreach (string arg in args)
			{
				int index = arg.IndexOf('=');
				if (index == -1)
					throw new ApplicationException($"Bad argument, expecting key=value: {arg}");

				arguments.Add(arg.Substring(0, index).ToLower().Trim(), arg.Substring(index + 1).Trim());
			}

			Processor processor = new Processor();

			if (arguments.ContainsKey("operation") == true)
			{
				switch (arguments["operation"])
				{
					case "convert":
						return await processor.ConvertAsync(arguments["target"]);

					default:
						throw new ApplicationException($"Unknown operation: {arguments["operation"]}");
				}
			}
			else
			{
				return await processor.RunAsync();
			}
		}
	}
}
