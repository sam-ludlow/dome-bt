using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Client;
using MonoTorrent;
using MonoTorrent.Connections;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace dome_bt
{
	public class BitTorrent
	{
		public ITorrentManagerFile requiredFile = null;

		public ClientEngine Engine;

		private CancellationTokenSource Cancellation = new CancellationTokenSource();

		public BitTorrent()
		{

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
			// Setup Engine
			//

			IPAddress externalIPAddress = IPAddress.Parse("217.40.212.83");
			int portNumber = 55123;

			var engineSettings = new EngineSettingsBuilder
			{
				AllowPortForwarding = false,
				AutoSaveLoadDhtCache = true,
				AutoSaveLoadFastResume = true,
				AutoSaveLoadMagnetLinkMetadata = true,

				CacheDirectory = Globals.DirectoryCache,

				ListenEndPoints = new Dictionary<string, IPEndPoint> {
					{ "ipv4", new IPEndPoint (IPAddress.Any, portNumber) },
					{ "ipv6", new IPEndPoint (IPAddress.IPv6Any, portNumber) }
				},

				DhtEndPoint = new IPEndPoint(IPAddress.Any, portNumber),

				ReportedListenEndPoints = new Dictionary<string, IPEndPoint> {
					{ "ipv4", new IPEndPoint( externalIPAddress, portNumber) }
				},
			};

			Engine = new ClientEngine(engineSettings.ToSettings());

			//
			// Existing Torrents
			//
			Tools.ConsoleHeading(1, $"Existing Torrents count {Engine.Torrents.Count}");
			foreach (TorrentManager manager in Engine.Torrents)
			{
				Console.WriteLine(manager.Torrent.Name);
			}

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

				//if (manager.Name != Globals.Magnets[AssetType.MachineRom].Name)
				//	continue;


				Tools.ConsoleHeading(1, new string[] { $"Setup Torrent", manager.Name });

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
					//	await
					manager.SetFilePriorityAsync(file, Priority.DoNotDownload);
					if (++count % 1000 == 0)
						Console.Write(".");
				}
				Console.WriteLine("...done.");


				Console.Write("Starting...");
				await manager.StartAsync();
				Console.WriteLine("...done.");

	
			}

			//
			// Processing
			//


			while (Engine.IsRunning)
			{
				await Task.Delay(5000, Cancellation.Token);

				Console.WriteLine("IsRunning");
			}


		}

	}
}
