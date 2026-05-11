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
            Tools.ConsoleHeading(1, $"Shutdown");

			Console.Write("stop engine ...");
			Engine.StopAllAsync().GetAwaiter().GetResult();
			Console.WriteLine("...done");
		}

		private static readonly string PayloadKey = "RRt08v+YWc2+910RGOhZO7DrNVnHKae8MDJyJNOd950=";

		private static string Decrypt(string input, string iv)
		{
			using (var aes = Aes.Create())
			{
				aes.Key = System.Convert.FromBase64String(PayloadKey);
				aes.IV = System.Convert.FromBase64String(iv);
				aes.Mode = CipherMode.CBC;
				aes.Padding = PaddingMode.PKCS7;

				using (var decryptor = aes.CreateDecryptor())
				using (var stream = new MemoryStream(System.Convert.FromBase64String(input)))
				using (var cryStream = new CryptoStream(stream, decryptor, CryptoStreamMode.Read))
				using (var reader = new StreamReader(cryStream))
					return reader.ReadToEnd();
			}
		}

		public async Task Worker()
		{
			int pad = 0;

			//
			// Existing Torrents
			//
			HashSet<string> existingHashes = new HashSet<string>();
			string metadataDirectory = Path.Combine(Globals.DirectoryCache, "metadata");
			if (Directory.Exists(metadataDirectory) == true)
				existingHashes = Directory.GetFiles(metadataDirectory, "*.torrent").Select(filename => Path.GetFileNameWithoutExtension(filename)).ToHashSet();

			//
			// Download torrents
			//
			Tools.ConsoleHeading(1, new string[] { "Obtain Torrents" });

			List<TorrentInfo> torrentInfos = new List<TorrentInfo>();

			foreach (string core in Globals.Cores)
			{
				for (int pass = 0; pass < 2; ++pass)
				{
					string url = $"https://data.spludlow.co.uk/api/torrents/{core}";
					if (pass == 0)
						url += ".peek";

					dynamic json = JsonConvert.DeserializeObject<dynamic>(Tools.FetchCached(url) ?? throw new ApplicationException("Can't fetch Torrents"));

					string body = Decrypt((string)json.body, (string)json.iv);

					if (pass == 0)
					{
						JArray items = JArray.Parse(body);

						HashSet<string> currentHashes = new HashSet<string>(items.Select(item => (string)item["hash"]));

						if (existingHashes.IsSupersetOf(currentHashes) == true)
						{
							foreach (dynamic item in items)
							{
								Torrent torrent = await Torrent.LoadAsync(Path.Combine(metadataDirectory, $"{item.hash}.torrent"));

								Console.WriteLine($"HAVE\t{item.type}\t{item.name}");

								TorrentInfo torrentInfo = new TorrentInfo()
								{
									Core = core,
									Type = (string)item.type,
									Name = (string)item.name,
									Version = (string)item.version,
									Hash = (string)item.hash,
									Magnet = (string)item.magnet,

									Torrent = torrent,
								};
								torrentInfos.Add(torrentInfo);
							}
							break;
						}
					}
					else
					{
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

							Console.WriteLine($"NEW\t{item.type}\t{item.name}");

							TorrentInfo torrentInfo = new TorrentInfo()
							{
								Core = core,
								Type = (string)item.type,
								Name = (string)item.name,
								Version = (string)item.version,
								Hash = (string)item.hash,
								Magnet = (string)item.magnet,

								Torrent = torrent,
							};
							torrentInfos.Add(torrentInfo);
						}
					}
				}
			}

			//
			// Setup Engine
			//
			Setup(torrentInfos.Count);

			//
			// Setup Torrents
			//
			Tools.ConsoleHeading(1, new string[] { "Setup Torrents" });

			foreach (TorrentInfo torrentInfo in torrentInfos)
			{
				//	Torrent torrent
				Console.Write($"{torrentInfo.Name} ...");
				var torrentManager = await Engine.AddAsync(torrentInfo.Torrent, Globals.DirectoryDownloads, TorrentSettings);
				torrentInfo.TorrentManager = torrentManager;
				Console.WriteLine("...done");

				pad = Math.Max(pad, torrentInfo.Name.Length);
			}

			Tools.ConsoleHeading(1, new string[] { "Starting Torrents" });

			//
			// Clear old downloads
			//
			if (Directory.Exists(Globals.DirectoryDownloads) == true)
			{
				List<string> currentNames = new List<string>(torrentInfos.Select(x => x.Name));

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
			HashSet<string> hashes = new HashSet<string>();
			foreach (TorrentInfo torrentInfo in torrentInfos)
			{
				var manager = torrentInfo.TorrentManager;

				string name = manager.Name.PadRight(pad);

				Console.WriteLine($"{name}	Starting	{manager.Files.Count}");

				await manager.StartAsync();

				if (manager.HasMetadata == false)
				{
					Console.WriteLine($"{name}	Waiting for Metadata");
					await manager.WaitForMetadataAsync();
					Console.WriteLine($"{name}	Metadata	{manager.Files.Count}	{manager.Files[0].Priority}");
				}

				if (manager.InfoHashes.V1OrV2.ToHex() != torrentInfo.Hash)
					throw new ApplicationException("hash mismatch");

				hashes.Add(torrentInfo.Hash);

				lock (Globals.TorrentInfos)
					Globals.TorrentInfos.Add(torrentInfo);

				Console.WriteLine($"{name}	Ready	{torrentInfo.Hash}");
			}

			//
			// Clear old cache
			//
			foreach (string directory in new string[] { Path.Combine(Globals.DirectoryCache, "fastresume"), Path.Combine(Globals.DirectoryCache, "metadata") })
			{
				if (Directory.Exists(directory) == true)
				{
					foreach (string filename in Directory.GetFiles(directory))
					{
						if (hashes.Contains(Path.GetFileNameWithoutExtension(filename)) == false)
						{
							Console.Write($"Remove old cache file {filename} ...");
							File.Delete(filename);
							Console.WriteLine("...done");
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
