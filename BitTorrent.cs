using System.Net;

using MonoTorrent.Client;
using MonoTorrent;

namespace dome_bt
{
	public class BitTorrent
	{
		public ClientEngine Engine;

		private CancellationTokenSource Cancellation = new ();

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

				CacheDirectory = Globals.DirectoryCache,

				ListenEndPoints = new Dictionary<string, IPEndPoint> {
					{ "ipv4", new IPEndPoint (IPAddress.Any, portNumber) },
					{ "ipv6", new IPEndPoint (IPAddress.IPv6Any, portNumber) }
				},

				DhtEndPoint = new IPEndPoint(IPAddress.Any, portNumber),
			};

			Engine = new ClientEngine(engineSettings.ToSettings());
		}

		public void Run()
		{
			var task = Worker();

			Console.CancelKeyPress += delegate { Cancellation.Cancel(); task.Wait(); };
			AppDomain.CurrentDomain.ProcessExit += delegate { Cancellation.Cancel(); task.Wait(); };

			AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); Cancellation.Cancel(); task.Wait(); };
			Thread.GetDomain().UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); Cancellation.Cancel(); task.Wait(); };

			try
			{
				task.Wait();
			}
			catch (OperationCanceledException)                                                                
			{

			}

			foreach (var manager in Engine.Torrents)
			{
				var stoppingTask = manager.StopAsync();
				while (manager.State != TorrentState.Stopped)
				{
					Task.WhenAll(stoppingTask, Task.Delay(250)).Wait();
				}
				stoppingTask.Wait();
			}
		}

		public async Task Worker()
		{
			//
			// Add Magnets
			//
			Tools.ConsoleHeading(1, $"Add Magnets");
			foreach (AssetType assetType in Globals.Magnets.Keys)
			{
				MagnetInfo magnetInfo = Globals.Magnets[assetType];

				Console.WriteLine(magnetInfo.Name);

				MagnetLink? magnetLink;
				if (MagnetLink.TryParse(magnetInfo.Magnet, out magnetLink) == false)
					throw new ApplicationException($"Bad magnet link: {magnetInfo.Magnet}");

				var torrentSettings = new TorrentSettingsBuilder
				{
					MaximumConnections = 60,
				};

				magnetInfo.TorrentManager = await Engine.AddAsync(magnetLink, Globals.DirectoryDownloads, torrentSettings.ToSettings());
			}

			//
			// Setup Torrents
			//
			foreach (TorrentManager manager in Engine.Torrents)
			{
				Tools.ConsoleHeading(1, new string[] { $"Setup Torrent", manager.Name });

				manager.PeerConnected += (o, e) =>
				{
					Console.WriteLine($"{manager.Name}\tPeerConnected\t{e.Peer.Uri}");
				};

				if (manager.Files.Count == 0)
				{
					Console.Write("First time get files...");
					await manager.StartAsync();
					Console.WriteLine("...done. A");
					await manager.WaitForMetadataAsync(Cancellation.Token);
					Console.WriteLine("...done. B");
					await manager.StopAsync();
					Console.WriteLine("...done.");
				}

				Console.Write($"Setting Priorities {manager.Files.Count} ...");

				int count = 0;
				foreach (var file in manager.Files)
				{
					await manager.SetFilePriorityAsync(file, Priority.DoNotDownload);
					if (++count % 1000 == 0)
						Console.WriteLine($"{count}/{manager.Files.Count}");
				}
				Console.WriteLine("...done.");


				Console.Write("Starting...");
				await manager.StartAsync();
				Console.WriteLine("...done.");

			}

			Globals.ReadyTime = DateTime.Now;

			//
			// Processing				Thread.Sleep(Timeout.Infinite);
			//


			while (Engine.IsRunning)
			{
				await Task.Delay(5000, Cancellation.Token);

				Console.WriteLine("IsRunning");
			}


		}

	}
}
