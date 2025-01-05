using System.Reflection;
using System.Diagnostics;

using MonoTorrent.Client;

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

		public static DateTime StartTime = DateTime.Now;
		public static DateTime ReadyTime = StartTime;

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
		private readonly string WelcomeText = @"@VERSION
'########:::'#######::'##::::'##:'########::::::::::'########::'########:
 ##.... ##:'##.... ##: ###::'###: ##.....::::::::::: ##.... ##:... ##..::
 ##:::: ##: ##:::: ##: ####'####: ##:::::::::::::::: ##:::: ##:::: ##::::
 ##:::: ##: ##:::: ##: ## ### ##: ######:::'#######: ########::::: ##::::
 ##:::: ##: ##:::: ##: ##. #: ##: ##...::::........: ##.... ##:::: ##::::
 ##:::: ##: ##:::: ##: ##:.:: ##: ##:::::::::::::::: ##:::: ##:::: ##::::
 ########::. #######:: ##:::: ##: ########:::::::::: ########::::: ##::::
........::::.......:::..:::::..::........:::::::::::........::::::..:::::

              See the README for more information
             https://github.com/sam-ludlow/dome-bt

            More information can be found at MAME-AO
https://github.com/sam-ludlow/mame-ao?tab=readme-ov-file#bittorrent

";

		public void Run()
		{
			Console.Title = $"DOME-BT {Globals.AssemblyVersion}";

			Console.Write(WelcomeText.Replace("@VERSION", Globals.AssemblyVersion));

			PleasureDome.ParseMagentLinks();

			WebServer webServer = new ();
			webServer.StartListener();

			Process.Start(new ProcessStartInfo(Globals.ListenAddress) { UseShellExecute = true });

			Globals.BitTorrent = new ();
			Globals.BitTorrent.Run();

		}
	}
}
