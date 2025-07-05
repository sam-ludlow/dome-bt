using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;

using MonoTorrent;
using MonoTorrent.Client;

namespace dome_bt
{
	public enum AssetType
	{
		MachineRom,
		MachineDisk,
		SoftwareRom,
		SoftwareDisk,
		HbMameMachineRom,
		HbMameSoftwareRom,
	}

	public class MagnetInfo
	{
		public MagnetInfo(string name, string version, string magnet)
		{
			Name = name;
			Version = version;
			Magnet = magnet;
		}
		public string Name;
		public string Version;
		public string Magnet;
		public string Hash;
		public MagnetLink MagnetLink;

		public TorrentManager TorrentManager;
	}

	public class Globals
	{
		public static string AssemblyVersion;

		public static HttpClient HttpClient;

		public static string DirectoryRoot;
		public static string DirectoryCache;
		public static string DirectoryDownloads;

		public static Dictionary<AssetType, MagnetInfo> Magnets = new Dictionary<AssetType, MagnetInfo>();

		public static string ListenAddress = "http://localhost:12381/";

		public static BitTorrent BitTorrent;

		public static DateTime StartTime = DateTime.Now;
		public static DateTime ReadyTime = StartTime;

		public static int Pid = Process.GetCurrentProcess().Id;

		public static List<string> Systems = new List<string>();

		public static Dictionary<string, string> Config = new Dictionary<string, string>();

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

		public void Run()
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

			if (Globals.Config.ContainsKey("systems") == true)
			{
				foreach (string system in Globals.Config["systems"].Split(','))
					Globals.Systems.Add(system.Trim());
			}
			else
			{
				Globals.Systems.Add("mame");
			}


			PleasureDome.ParseMagentLinks();

			WebServer webServer = new WebServer();
			webServer.StartListener();

			Globals.BitTorrent = new BitTorrent();
			Globals.BitTorrent.Run();

		}
	}
}
