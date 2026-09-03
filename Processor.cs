using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;
using MonoTorrent;
using MonoTorrent.Client;

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
		public string DatFile;

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

		public static Config Config;

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

			Config = new Config(Path.Combine(Globals.DirectoryRoot, "_config.txt"));
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

			if (Globals.Config.ContainsKey("cores") == true)
			{
				foreach (string core in Globals.Config.Get("cores").Split(','))
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
			string[] urls = Globals.Config.Get("magnets").Split(',').Select(x => x.Trim()).ToArray();

			if (urls.Length != 4)
				throw new ApplicationException("magnets should be 4 items (mame, hbmame, pinmame, pinball)");

			Dictionary<string, TorrentInfo[]> coreMagnetInfos = new Dictionary<string, TorrentInfo[]>()
			{
				{ "mame", PleasureDome.ParseMameMagentLink("mame", urls[0]) },
				{ "hbmame", PleasureDome.ParseMameMagentLink("hbmame", urls[1]) },
				{ "pinmame", PleasureDome.ParsePinMAMEMagentLink(urls[2]) },
				{ "pinball", PleasureDome.ParsePinballMagentLink(urls[3]) },
			};

			BitTorrent bitTorrent = new BitTorrent();

			bitTorrent.Setup(coreMagnetInfos.Values.Select(x => x.Length).Sum());

			Tools.ConsoleHeading(1, new string[] { "Add Magnets" });

			foreach (string core in coreMagnetInfos.Keys)
			{
				foreach (var magnetInfo in coreMagnetInfos[core])
				{
					Console.Write($"{magnetInfo.Name} ...");
					var torrentManager = await bitTorrent.Engine.AddAsync(magnetInfo.MagnetLink, Globals.DirectoryDownloads, bitTorrent.TorrentSettings);
					magnetInfo.Name = torrentManager.Name;
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
					if (magnetInfo.DatFile != null)
						json.dat = magnetInfo.DatFile;
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
