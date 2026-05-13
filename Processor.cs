using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;
using Newtonsoft.Json.Linq;

namespace dome_bt
{
	public class TorrentInfo
	{
		public string Core;
		public string Type;
		public string Name;
		public string Version;
		public string Hash;
		public string Magnet;

		public Torrent Torrent;
		public TorrentManager TorrentManager;

		public MagnetLink MagnetLink;
	}

	public class Globals
	{
		public static string AssemblyVersion;

		public static HttpClient HttpClient;

		public static string DirectoryRoot;
		public static string DirectoryCache;
		public static string DirectoryDownloads;

		public static List<TorrentInfo> TorrentInfos = new List<TorrentInfo>();

		public static string ListenAddress = "http://localhost:12381/";

		public static BitTorrent BitTorrent;

		public static DateTime StartTime = DateTime.Now;
		public static DateTime ReadyTime = StartTime;

		public static int Pid = Process.GetCurrentProcess().Id;

		public static List<string> Cores = new List<string>();

		public static Dictionary<string, string> Config = new Dictionary<string, string>();

		public static Dictionary<string, string> NameTypeLookup = new Dictionary<string, string>()
		{
			{ "ROMs (merged)", "mr" },
			{ "CHDs (merged)", "md" },
			{ "Software List ROMs (merged)", "sr" },
			{ "Software List CHDs (merged)", "sd" },
		};

		static Globals()
		{
			Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version ?? throw new ApplicationException("Can't find Assembly Version");
			AssemblyVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";

			DirectoryRoot = Environment.CurrentDirectory;
			Directory.CreateDirectory(DirectoryRoot);

			DirectoryCache = Path.Combine(DirectoryRoot, "_CACHE");
			Directory.CreateDirectory(DirectoryCache);

			DirectoryDownloads = Path.Combine(DirectoryRoot, "_DOWNLOADS");
			Directory.CreateDirectory(DirectoryDownloads);

			HttpClient = new HttpClient(new HttpClientHandler { UseCookies = false });
			HttpClient.DefaultRequestHeaders.Add("User-Agent", $"dome-bt/{AssemblyVersion} (https://github.com/sam-ludlow/dome-bt)");
			HttpClient.Timeout = TimeSpan.FromSeconds(180);     // metdata 3 minutes

		}
	}

	public class Processor
	{
		private readonly string WelcomeText = @"@VERSION

$$$$$$$\   $$$$$$\  $$\      $$\ $$$$$$$$\       $$$$$$$\ $$$$$$$$\ 
$$  __$$\ $$  __$$\ $$$\    $$$ |$$  _____|      $$  __$$\\__$$  __|
$$ |  $$ |$$ /  $$ |$$$$\  $$$$ |$$ |            $$ |  $$ |  $$ |   
$$ |  $$ |$$ |  $$ |$$\$$\$$ $$ |$$$$$\          $$$$$$$\ |  $$ |   
$$ |  $$ |$$ |  $$ |$$ \$$$  $$ |$$  __|         $$  __$$\   $$ |   
$$ |  $$ |$$ |  $$ |$$ |\$  /$$ |$$ |            $$ |  $$ |  $$ |   
$$$$$$$  | $$$$$$  |$$ | \_/ $$ |$$$$$$$$\       $$$$$$$  |  $$ |   
\_______/  \______/ \__|     \__|\________|      \_______/   \__|   

              See the README for more information
             https://github.com/sam-ludlow/dome-bt

";

		public Processor()
		{
			Console.Title = $"DOME-BT {Globals.AssemblyVersion}";

			Console.Write(WelcomeText.Replace("@VERSION", Globals.AssemblyVersion));

			string configFilename = Path.Combine(Globals.DirectoryRoot, "_config.txt");
			if (File.Exists(configFilename) == true)
			{
				using (StreamReader reader = new StreamReader(configFilename, Encoding.UTF8))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						string[] parts = line.Split('\t');
						if (parts.Length == 2)
							Globals.Config.Add(parts[0].ToLower(), parts[1]);
					}
				}
			}

			if (Globals.Config.ContainsKey("cores") == true)
			{
				foreach (string core in Globals.Config["cores"].Split(','))
					Globals.Cores.Add(core.Trim());
			}
			else
			{
				Globals.Cores.Add("mame");
			}
		}

		public async Task<int> RunAsync()
		{
			WebServer webServer = new WebServer();
			webServer.StartListener();

			Globals.BitTorrent = new BitTorrent();
			await Globals.BitTorrent.RunAsync();

			return 0;
		}

		public async Task<int> ConvertAsync(string targetDirectory)
		{
			string[] cores = Globals.Cores.ToArray();
			string[] urls = Globals.Config["magnets"].Split(',').Select(x => x.Trim()).ToArray();

			Dictionary<string, TorrentInfo[]> coreMagnetInfos = new Dictionary<string, TorrentInfo[]>();

			for (int index = 0; index < cores.Length; ++index)
			{
				string core = cores[index];
				string url = urls[index];

				TorrentInfo[] magnetInfos = PleasureDome.ParseMagentLink(core, url);
				coreMagnetInfos.Add(core, magnetInfos);

				foreach (TorrentInfo magnetInfo in magnetInfos)
				{
					magnetInfo.MagnetLink = MagnetLink.Parse(magnetInfo.Magnet);
					magnetInfo.Hash = magnetInfo.MagnetLink.InfoHashes.V1OrV2.ToHex();
				}
			}

			BitTorrent bitTorrent = new BitTorrent();

			bitTorrent.Setup(coreMagnetInfos.Values.Select(x => x.Length).Sum());

			Tools.ConsoleHeading(1, new string[] { "Add Magnets" });

			foreach (string core in coreMagnetInfos.Keys)
			{
				foreach (var magnetInfo in coreMagnetInfos[core])
				{
					Console.Write($"{magnetInfo.Name} ...");
					await bitTorrent.Engine.AddAsync(magnetInfo.MagnetLink, Globals.DirectoryDownloads, bitTorrent.TorrentSettings);
					Console.WriteLine("...done");
				}
			}

			Tools.ConsoleHeading(1, new string[] { "Get Metadata" });

			int newCount = 0;

			foreach (var torrentManager in bitTorrent.Engine.Torrents)
			{
				if (torrentManager.HasMetadata == false)
				{
					Console.Write($"{torrentManager.Name} ...");
					await torrentManager.StartAsync();
					await torrentManager.WaitForMetadataAsync();
					await torrentManager.StopAsync();
					Console.WriteLine("...done");

					++newCount;
				}
				else
				{
					Console.WriteLine($"{torrentManager.Name} ...have");
				}
			}

			Console.Write("Stopping Engine ...");
			await bitTorrent.Engine.StopAllAsync();
			Console.WriteLine("...done");

			Tools.ConsoleHeading(1, new string[] { "Save Payloads" });

			Directory.CreateDirectory(targetDirectory);

			foreach (string core in coreMagnetInfos.Keys)
			{
				var array = new JArray();

				foreach (var magnetInfo in coreMagnetInfos[core])
				{
					Console.WriteLine($"{magnetInfo.Type}\t{magnetInfo.Name}\t{magnetInfo.Hash}");

					string sourceFilename = Path.Combine(Globals.DirectoryCache, "metadata", magnetInfo.Hash + ".torrent");

					string zipFilename = Path.Combine(targetDirectory, $"{magnetInfo.Hash}.torrent.zip");
					File.Delete(zipFilename);
					Tools.CompressSingleFile(sourceFilename, zipFilename);

					dynamic json = new JObject();

					json.name = magnetInfo.Name;
					json.type = magnetInfo.Type;
					json.version = magnetInfo.Version;
					json.hash = magnetInfo.Hash;
					json.magnet = magnetInfo.Magnet;
					json.torrent = System.Convert.ToBase64String(File.ReadAllBytes(zipFilename));

					array.Add(json);

					File.Delete(zipFilename);
				}

				string targetFilename = Path.Combine(targetDirectory, core + ".json");
				File.Delete(targetFilename);
				File.WriteAllText(targetFilename, array.ToString(), Encoding.ASCII);

				Console.WriteLine($"{core}\t{targetFilename}");
			}

			return newCount;
		}
	}
}
