using System;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using MonoTorrent.Client;
using MonoTorrent;

namespace dome_bt
{
	public class BitTorrent
	{
		public ClientEngine Engine;

		public Dictionary<string, TorrentManager> TorrentManagers = new Dictionary<string, TorrentManager>();

		public object _Lock = new object();

		public bool AskStop = false;

		private int MaximumConnectionsPerTorrent = 100;

		public BitTorrent()
		{
			//
			// Setup Engine
			//
			int portNumber = 55123;

			var engineSettings = new EngineSettingsBuilder
			{
				AllowPortForwarding = true,

				AutoSaveLoadDhtCache = true,
				AutoSaveLoadFastResume = true,
				AutoSaveLoadMagnetLinkMetadata = true,
				FastResumeMode = FastResumeMode.BestEffort,

				CacheDirectory = Globals.DirectoryCache,

				ListenEndPoints = new Dictionary<string, IPEndPoint> {
					{ "ipv4", new IPEndPoint (IPAddress.Any, portNumber) },
					{ "ipv6", new IPEndPoint (IPAddress.IPv6Any, portNumber) }
				},

				DhtEndPoint = new IPEndPoint(IPAddress.Any, portNumber),

				MaximumConnections = 4 * MaximumConnectionsPerTorrent,
			};

			//	TODO: Impliment rate limiting

			//double downloadLimit = 20.0;
			//double uploadLimit = 5.0;

			//double megaBitsToBytes = 125000.0;

			//engineSettings.MaximumDownloadRate = (int)(downloadLimit * megaBitsToBytes);
			//engineSettings.MaximumUploadRate = (int)(uploadLimit * megaBitsToBytes);

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
			foreach (var manager in Engine.Torrents)
			{
				Console.Write($"Stopping Torrent: {manager.Name} ...");

				var stoppingTask = manager.StopAsync();
				while (manager.State != TorrentState.Stopped)
				{
					Task.WhenAll(stoppingTask, Task.Delay(250)).Wait();
				}
				stoppingTask.Wait();

				Console.WriteLine("...done");
			}
		}

		public async Task Worker()
		{
			int pad = 0;

			//
			// Clear old directories
			//
			List<string> currentNames = new List<string>(Globals.Magnets.Values.Select(info => info.Name));

			if (Directory.Exists(Globals.DirectoryDownloads) == true)
			{
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
			// Add Magnets
			//
			Tools.ConsoleHeading(1, $"Add Magnets");

			foreach (AssetType assetType in Globals.Magnets.Keys)
			{
				MagnetInfo magnetInfo = Globals.Magnets[assetType];

				MagnetLink magnetLink;
				if (MagnetLink.TryParse(magnetInfo.Magnet, out magnetLink) == false)
					throw new ApplicationException($"Bad magnet link: {magnetInfo.Magnet}");

				magnetInfo.MagnetLink = magnetLink;

				var torrentSettings = new TorrentSettingsBuilder
				{
					MaximumConnections = MaximumConnectionsPerTorrent,
				};

				magnetInfo.TorrentManager = await Engine.AddAsync(magnetLink, Globals.DirectoryDownloads, torrentSettings.ToSettings());
				magnetInfo.Hash = magnetLink.InfoHashes.V1OrV2.ToHex();

				Console.WriteLine($"{assetType}	{magnetInfo.Version}	{magnetInfo.Name}	{magnetInfo.Hash}");

				pad = Math.Max(pad, magnetInfo.Name.Length);
			}

			//
			// Setup Torrents
			//
			Tools.ConsoleHeading(1, $"Start Torrents");

			List<Task> managerTasks = new List<Task>();

			foreach (TorrentManager manager in Engine.Torrents)
			{
				string name = manager.Name.PadRight(pad);

				managerTasks.Add(Task.Run(async () =>
				{
					Console.WriteLine($"{name}	START	{manager.Files.Count}");

					if (manager.Files.Count == 0)
					{
						await manager.StartAsync();
						await manager.WaitForMetadataAsync();
						await manager.StopAsync();
					}

					Console.WriteLine($"{name}	META	{manager.Files.Count}	{manager.Files[0].Priority}");

					if (manager.Files[0].Priority == Priority.Normal)
					{
						int count = 0;
						foreach (var file in manager.Files)
						{
							await manager.SetFilePriorityAsync(file, Priority.DoNotDownload);

							if (++count % 1000 == 0)
								Console.WriteLine($"{name}	{count}/{manager.Files.Count}");
						}
					}

					await manager.StartAsync();
					
					string hex = manager.MagnetLink.InfoHashes.V1OrV2.ToHex();

					if (hex == null || hex.Length != 40)
						throw new ApplicationException($"Bad Hash HashChecked:{manager.HashChecked}");

					lock (_Lock)
						TorrentManagers.Add(hex, manager);

					Console.WriteLine($"{name}	READY	{manager.Files.Count}");
				}));
			}

			await Task.WhenAll(managerTasks);

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

			Console.WriteLine("Clean Exit.");
		}

	}
}
