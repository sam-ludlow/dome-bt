using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;

using MonoTorrent;
using MonoTorrent.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace dome_bt
{
	public class BitTorrent
	{
		public ClientEngine Engine;

		public Dictionary<string, TorrentManager> TorrentManagers = new Dictionary<string, TorrentManager>();

		public object _Lock = new object();

		public bool AskStop = false;

		private int MaximumConnectionsPerTorrent = 100;

		private readonly double MegaBitsToBytes = 125000.0;
        private double MaximumDownloadRate = 0;
		private double MaximumUploadRate = 0;

		public TorrentSettings TorrentSettings;

		public BitTorrent()
		{
			var torrentSettings = new TorrentSettingsBuilder
			{
				MaximumConnections = MaximumConnectionsPerTorrent,
				AllowPeerExchange = true,
				AllowDht = true,
			};
			TorrentSettings = torrentSettings.ToSettings();
		}

		public void Setup(int count)
		{
			if (Globals.Config.ContainsKey("maximum-connections-per-torrent") == true)
				MaximumConnectionsPerTorrent = Int32.Parse(Globals.Config["maximum-connections-per-torrent"]);

			if (Globals.Config.ContainsKey("maximum-download-rate-mbps") == true)
				MaximumDownloadRate = Double.Parse(Globals.Config["maximum-download-rate-mbps"]);

			if (Globals.Config.ContainsKey("maximum-upload-rate-mbps") == true)
				MaximumUploadRate = Double.Parse(Globals.Config["maximum-upload-rate-mbps"]);

			//
			// Setup Engine
			//
			int portNumber = 55123;

			var engineSettings = new EngineSettingsBuilder
			{
				AllowPortForwarding = true,
				AllowLocalPeerDiscovery = false,
				AutoSaveLoadDhtCache = true,
				AutoSaveLoadFastResume = true,
				AutoSaveLoadMagnetLinkMetadata = true,
				FastResumeMode = FastResumeMode.BestEffort,

				CacheDirectory = Globals.DirectoryCache,

				ListenEndPoints = new Dictionary<string, IPEndPoint>() {
					{ "ipv4", new IPEndPoint (IPAddress.Any, portNumber) },
					{ "ipv6", new IPEndPoint (IPAddress.IPv6Any, portNumber) }
				},

				DhtEndPoint = new IPEndPoint(IPAddress.Any, portNumber),

				MaximumConnections = count * MaximumConnectionsPerTorrent,
				MaximumDownloadRate = (int)(MaximumDownloadRate * MegaBitsToBytes),
				MaximumUploadRate = (int)(MaximumUploadRate * MegaBitsToBytes),

				MaximumHalfOpenConnections = 16,
			};

			Tools.ConsoleHeading(1, new string[] { "Engine Settings", "(0 = No limit)" });

			Console.WriteLine($"Maximum Connections   :{engineSettings.MaximumConnections} (Magnets:{count} X Max Per Torrent: {MaximumConnectionsPerTorrent})");
			Console.WriteLine($"Maximum Download Rate : {engineSettings.MaximumDownloadRate} B/s ({MaximumDownloadRate} Mbit/s)");
			Console.WriteLine($"Maximum Upload Rate   : {engineSettings.MaximumUploadRate} B/s ({MaximumUploadRate} Mbit/s)");

            Engine = new ClientEngine(engineSettings.ToSettings());

		}

		public void Run()
		{
			var task = Worker();

			Exception error = null;
			try
			{
				task.Wait();
			}
			catch (Exception e)
			{
				Tools.ReportError(e, "Fatal Error");
				error = e;
			}
			finally
			{
				ShutDown();
			}

			if (error != null)
			{
				Console.WriteLine();
				Console.WriteLine("Press any key to continue, program has crashed and will exit.");
				Console.ReadKey();
				Environment.Exit(1);
			}
		}

		public void ShutDown()
		{
            Tools.ConsoleHeading(1, $"Shutting down...");

            List<Task> managerTasks = new List<Task>();

            foreach (TorrentManager manager in Engine.Torrents)
            {
                 managerTasks.Add(Task.Run(async () =>
                {
                    Console.WriteLine($"{manager.Name}	STOPPING	{manager.Files.Count}");

                    var stoppingTask = manager.StopAsync();
                    while (manager.State != TorrentState.Stopped)
                    {
                        Task.WhenAll(stoppingTask, Task.Delay(250)).Wait();
                    }
                    stoppingTask.Wait();

                    Console.WriteLine($"{manager.Name}	STOPPED	{manager.Files.Count}");
                }));
            }

            Task.WhenAll(managerTasks).Wait();
        }

		private static readonly string MagnetKey = "RRt08v+YWc2+910RGOhZO7DrNVnHKae8MDJyJNOd950=";

		public async Task Worker()
		{
			int pad = 0;

			//
			// Download torrents
			//
			Tools.ConsoleHeading(1, new string[] { "Obtain Torrents" });

			var torrents = new Dictionary<AssetType, Torrent>();

			foreach (string core in Globals.Cores)
			{
				AssetType[] assetTypes;
				List<string> names;

				switch (core)
				{
					case "mame":
						assetTypes = new AssetType[] { AssetType.MachineRom, AssetType.MachineDisk, AssetType.SoftwareRom, AssetType.SoftwareDisk };
						names = new List<string>(new string[] { "ROMs (merged)", "CHDs (merged)", "Software List ROMs (merged)", "Software List CHDs (merged)" });
						break;

					case "hbmame":
						assetTypes = new AssetType[] { AssetType.HbMameMachineRom, AssetType.HbMameSoftwareRom };
						names = new List<string>(new string[] { "ROMs (merged)", "Software List ROMs (merged)" });
						break;

					default:
						throw new ApplicationException($"Unknown core: {core}");
				}
				string url = $"https://data.spludlow.co.uk/api/torrents/{core}";

				dynamic json = JsonConvert.DeserializeObject<dynamic>(Tools.FetchCached(url) ?? throw new ApplicationException("Can't fetch Torrents"));

				string body;
				using (var aes = Aes.Create())
				{
					aes.Key = System.Convert.FromBase64String(MagnetKey);
					aes.IV = System.Convert.FromBase64String((string)json.iv);
					aes.Mode = CipherMode.CBC;
					aes.Padding = PaddingMode.PKCS7;

					using (var decryptor = aes.CreateDecryptor())
						using (var stream = new MemoryStream(System.Convert.FromBase64String((string)json.body)))
							using (var cryStream = new CryptoStream(stream, decryptor, CryptoStreamMode.Read))
								using (var reader = new StreamReader(cryStream))
									body = reader.ReadToEnd();
				}

				foreach (dynamic item in JArray.Parse(body))
				{
					Torrent torrent;
					using (var targetStream = new MemoryStream())
					{
						using (var sourceStream = new MemoryStream(System.Convert.FromBase64String((string)item.torrent)))
							using (var zipArchive = new ZipArchive(sourceStream))
								using (var zipStream = zipArchive.Entries[0].Open())
									zipStream.CopyTo(targetStream);


						targetStream.Position = 0;
						torrent = Torrent.Load(targetStream);
					}

					string text = item.name;
					int index;

					index = text.IndexOf(' ');
					text = text.Substring(index + 1);

					index = text.IndexOf(' ');
					string version = text.Substring(0, index);
					text = text.Substring(index + 1);

					index = names.IndexOf(text);
					if (index != -1)
					{
						string magnet = item.magnet;

						AssetType assetType = assetTypes[index];
						Globals.Magnets.Add(assetType, new MagnetInfo((string)item.name, version, magnet, null));
						torrents.Add(assetType, torrent);
						Console.WriteLine($"{core}\t{assetType}\t{version}\t{text}");
					}
				}
			}

			//
			// Setup Engine
			//
			Setup(torrents.Count);

			Tools.ConsoleHeading(1, new string[] { "Setup Torrents" });
			//
			// Setup Magnets - TODO dont need magnets any more
			//
			foreach (AssetType assetType in Globals.Magnets.Keys)
			{
				MagnetInfo magnetInfo = Globals.Magnets[assetType];
				Torrent torrent = torrents[assetType];

				magnetInfo.MagnetLink = MagnetLink.Parse(magnetInfo.Magnet);
				magnetInfo.Hash = magnetInfo.MagnetLink.InfoHashes.V1OrV2.ToHex();

				var torrentSettings = new TorrentSettingsBuilder
				{
					MaximumConnections = MaximumConnectionsPerTorrent,
					AllowPeerExchange = true,
					AllowDht = true,
				};

				//	TODO Use either ....
				//magnetInfo.TorrentManager = await Engine.AddAsync(magnetInfo.MagnetLink, Globals.DirectoryDownloads, torrentSettings.ToSettings());

				Console.Write($"{magnetInfo.Name} ...");
				magnetInfo.TorrentManager = await Engine.AddAsync(torrent, Globals.DirectoryDownloads, torrentSettings.ToSettings());
				Console.WriteLine("...done");

				pad = Math.Max(pad, magnetInfo.Name.Length);
			}

			Tools.ConsoleHeading(1, new string[] { "Starting Torrents" });

			//
			// Clear old directories
			//
			if (Directory.Exists(Globals.DirectoryDownloads) == true)
			{
				List<string> currentNames = new List<string>(Globals.Magnets.Values.Select(info => info.Name));

				foreach (string directory in Directory.GetDirectories(Globals.DirectoryDownloads))
				{
					if (currentNames.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase) == false)
					{
						Console.Write($"Remove old directory {directory} ...");
						Directory.Delete(directory, true);
						Console.WriteLine("...done");
					}
				}
			}

			//
			// Start Torrents
			//
			foreach (TorrentManager manager in Engine.Torrents)
			{
				string name = manager.Name.PadRight(pad);

				Console.WriteLine($"{name}	Starting	{manager.Files.Count}");

				await manager.StartAsync();

				if (manager.HasMetadata == false)
				{
					Console.WriteLine($"{name}	Waiting for Metadata");
					await manager.WaitForMetadataAsync();
					Console.WriteLine($"{name}	Metadata	{manager.Files.Count}	{manager.Files[0].Priority}");
				}

				string hex = manager.MagnetLink.InfoHashes.V1OrV2.ToHex();

				if (hex == null || hex.Length != 40)
					throw new ApplicationException($"{name} Bad Hash HashChecked:{manager.HashChecked}");

				lock (_Lock)
					TorrentManagers.Add(hex, manager);

				Console.WriteLine($"{name}	Ready	{hex}");
			}

			//
			// Clear old torrent cache files
			//
			foreach (string directory in new string[] { Path.Combine(Globals.DirectoryCache, "fastresume"), Path.Combine(Globals.DirectoryCache, "metadata") })
			{
				if (Directory.Exists(directory) == true)
				{
					foreach (string filename in Directory.GetFiles(directory))
					{
						lock (_Lock)
						{
							if (TorrentManagers.ContainsKey(Path.GetFileNameWithoutExtension(filename)) == false)
							{
								Console.Write($"Remove old cache file {filename} ...");
								File.Delete(filename);
								Console.WriteLine("...done");
							}
						}
					}
				}
			}

			Globals.ReadyTime = DateTime.Now;

			//
			// Processing
			//
			Tools.ConsoleHeading(1, $"All Torrents Ready");

			while (AskStop == false)
			{
				await Task.Delay(5000);

				Console.Clear();

				long dataBytesReceived = 0;
				long dataBytesSent = 0;
				foreach (TorrentManager manager in Engine.Torrents)
				{
					dataBytesReceived += manager.Monitor.DataBytesReceived;
					dataBytesSent += manager.Monitor.DataBytesSent;
				}

				Tools.ConsoleHeading(1, new string[] {
					$"DOME-BT {Globals.AssemblyVersion}    start:{Globals.StartTime}    now:{DateTime.Now}    run:{Tools.TimeTookText(DateTime.Now - Globals.StartTime)}",
					"",
					$"connections:{Engine.ConnectionManager.OpenConnections}    download:{Tools.DataSizeText(Engine.TotalDownloadRate)}/s    upload:{Tools.DataSizeText(Engine.TotalUploadRate)}/s",
					"",
					$"received:{Tools.DataSizeText(dataBytesReceived)}    sent:{Tools.DataSizeText(dataBytesSent)}",
				});

				foreach (TorrentManager manager in Engine.Torrents)
				{
					Console.WriteLine($"{manager.Name.PadRight(pad)}   {manager.State.ToString().PadRight(12)}   {manager.OpenConnections.ToString().PadLeft(3)}   " +
						$"{Tools.DataSizeText(manager.Monitor.DataBytesReceived).PadLeft(24)}   {Tools.DataSizeText(manager.Monitor.DataBytesSent).PadLeft(24)}");
				}
			}

			Console.WriteLine("Asked to stop.");
		}
	}
}
