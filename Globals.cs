using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace dome_bt
{
	public class Globals
	{
		static Globals()
		{
			Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version ?? throw new ApplicationException("Can't find Assembly Version");
			AssemblyVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";

			HttpClient = new HttpClient(new HttpClientHandler { UseCookies = false });
			HttpClient.DefaultRequestHeaders.Add("User-Agent", $"dome-bt/{AssemblyVersion} (https://github.com/sam-ludlow/dome-bt)");
			HttpClient.Timeout = TimeSpan.FromSeconds(180);     // metdata 3 minutes

			DirectoryRoot = Environment.CurrentDirectory;
			Directory.CreateDirectory(DirectoryRoot);

			DirectoryCache = Path.Combine(DirectoryRoot, "_CACHE");
			Directory.CreateDirectory(DirectoryCache);

		}

		public static string AssemblyVersion;

		public static HttpClient HttpClient;

		public static string DirectoryRoot;

		public static string DirectoryCache;

	}
}
