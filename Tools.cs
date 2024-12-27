using System.Text;

namespace dome_bt
{
	public class Tools
	{
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

	}
}
