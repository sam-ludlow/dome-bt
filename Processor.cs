﻿using System.Reflection;
using MonoTorrent.Client;
using MonoTorrent;
using MonoTorrent.Connections;

namespace dome_bt
{
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

	public enum AssetType
	{
		MachineRom,
		MachineDisk,
		SoftwareRom,
		SoftwareDisk,
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

		public TorrentManager? TorrentManager;
	}

	public class Processor
	{

		public void Run()
		{
			PleasureDome.ParseMagentLinks();

			WebServer webServer = new WebServer();
			webServer.StartListener();

			Globals.BitTorrent = new BitTorrent();
			Globals.BitTorrent.Run();

		}
	}
}
