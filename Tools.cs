using System.Text;

namespace dome_bt
{
	public class Tools
	{
		private static readonly char[] _HeadingChars = new char[] { ' ', '#', '=', '-' };

		public static void ConsoleRule(int head)
		{
			Console.WriteLine(new String(_HeadingChars[head], Console.WindowWidth - 1));
		}

		public static void ConsoleHeading(int head, string line)
		{
			ConsoleHeading(head, new string[] { line });
		}
		public static void ConsoleHeading(int head, string[] lines)
		{
			ConsoleRule(head);

			char ch = _HeadingChars[head];

			foreach (string line in lines)
			{
				int pad = Console.WindowWidth - 3 - line.Length;
				if (pad < 1)
					pad = 1;
				int odd = pad % 2;
				pad /= 2;

				Console.Write(ch);
				Console.Write(new String(' ', pad));
				Console.Write(line);
				Console.Write(new String(' ', pad + odd));
				Console.Write(ch);
				Console.WriteLine();
			}

			ConsoleRule(head);
		}

		public static string? FetchCached(string url)
		{
			string filename = Path.Combine(Globals.DirectoryCache, Tools.ValidFileName(url.Substring(8)));

			string? result = null;

			if (File.Exists(filename) == false || (DateTime.Now - File.GetLastWriteTime(filename) > TimeSpan.FromHours(3)))
			{
				try
				{
					Console.Write($"Downloading {url} ...");
					result = Fetch(url);
					Console.WriteLine("...done");
				}
				catch (TaskCanceledException e)
				{
					Console.WriteLine($"ERROR Fetch client timeout: {url} {e.Message}");
				}
				catch (HttpRequestException e)
				{
					Console.WriteLine($"ERROR Fetch request: {url} {e.Message} {e.InnerException?.Message}");
				}
				catch (Exception e)
				{
					Console.WriteLine($"ERROR Fetch: {url} {e.Message} {e.InnerException?.Message}");
				}

				if (result != null)
					File.WriteAllText(filename, result, Encoding.UTF8);
			}

			if (result == null && File.Exists(filename) == true)
				result = File.ReadAllText(filename, Encoding.UTF8);

			return result;
		}

		public static string Fetch(string url)
		{
			try
			{
				using (HttpRequestMessage requestMessage = new(HttpMethod.Get, url))
				{
					Task<HttpResponseMessage> requestTask = Globals.HttpClient.SendAsync(requestMessage);
					requestTask.Wait();
					HttpResponseMessage responseMessage = requestTask.Result;

					responseMessage.EnsureSuccessStatusCode();

					Task<string> responseMessageTask = responseMessage.Content.ReadAsStringAsync();
					responseMessageTask.Wait();
					string responseBody = responseMessageTask.Result;

					return responseBody;
				}
			}
			catch (AggregateException e)
			{
				throw e.InnerException ?? e;
			}
		}

		private static readonly List<char> _InvalidFileNameChars = new List<char>(Path.GetInvalidFileNameChars());
		public static string ValidFileName(string name)
		{
			return ValidName(name, _InvalidFileNameChars, "_");
		}
		public static string ValidName(string name, List<char> invalidChars, string replaceBadWith)
		{
			StringBuilder sb = new StringBuilder();

			foreach (char c in name)
			{
				if (invalidChars.Contains(c) == true)
					sb.Append(replaceBadWith);
				else
					sb.Append(c);
			}

			return sb.ToString();
		}

		private static string[] _SystemOfUnits =
		{
			"Bytes",
			"Kilobytes (KB)",
			"Megabytes (MB)",
			"Gigabytes (GB)",
			"Terabytes (TB)",
			"Petabytes (PB)",
			"Exabytes (EB)"
		};
		public static string DataSizeText(ulong sizeBytes)
		{
			for (int index = 0; index < _SystemOfUnits.Length; ++index)
			{
				ulong nextUnit = (ulong)Math.Pow(2, (index + 1) * 10);

				if (sizeBytes < nextUnit || nextUnit == 0 || index == (_SystemOfUnits.Length - 1))
				{
					ulong unit = (ulong)Math.Pow(2, index * 10);
					decimal result = (decimal)sizeBytes / (decimal)unit;
					int decimalPlaces = 0;
					if (result <= 9.9M)
						decimalPlaces = 1;
					result = Math.Round(result, decimalPlaces);
					return result.ToString() + " " + _SystemOfUnits[index];
				}
			}

			throw new ApplicationException($"Failed to find Data Size {sizeBytes}");
		}

		public static string TimeTookText(TimeSpan span)
		{
			StringBuilder text = new StringBuilder();

			if (((int)span.TotalDays) > 0)
			{
				text.Append((int)span.TotalDays);
				text.Append("d");
				if (span.Hours > 0)
				{
					text.Append(" ");
					text.Append(span.Hours);
					text.Append("h");
				}
				if (span.Minutes > 0)
				{
					text.Append(" ");
					text.Append(span.Minutes);
					text.Append("m");
				}

				return text.ToString();
			}

			if (((int)span.TotalHours) > 0)
			{
				text.Append(span.Hours);
				text.Append("h");
				if (span.Minutes > 0)
				{
					text.Append(" ");
					text.Append(span.Minutes);
					text.Append("m");
				}

				return text.ToString();
			}

			if (((int)span.TotalMinutes) > 0)
			{
				text.Append(span.Minutes);
				text.Append("m");
				if (span.Seconds > 0)
				{
					text.Append(" ");
					text.Append(span.Seconds);
					text.Append("s");
				}

				return text.ToString();
			}

			if (((int)span.TotalSeconds) > 0)
			{
				text.Append(span.Seconds);
				text.Append("s");
				if (span.Milliseconds > 0)
				{
					text.Append(" ");
					text.Append(span.Milliseconds);
					text.Append("ms");
				}

				return text.ToString();
			}

			text.Append(span.Milliseconds);
			text.Append("ms");

			return text.ToString();
		}

	}
}
