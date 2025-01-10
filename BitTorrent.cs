﻿using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MonoTorrent.Client;
using MonoTorrent;

namespace dome_bt
{
	public class BitTorrent
	{
		public ClientEngine Engine;

		private CancellationTokenSource Cancellation = new CancellationTokenSource();

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

			//Console.CancelKeyPress += Console_CancelKeyPress;
			//AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			//AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;


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

		//private void BitTorrent_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		//{
		//	throw new NotImplementedException();
		//}

		//private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		//{
		//	throw new NotImplementedException();
		//}

		//private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
		//{
		//	throw new NotImplementedException();
		//}

		//private void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
		//{
		//	throw new NotImplementedException();
		//}

		public async Task Worker()
		{
			int pad = 0;

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

				var torrentSettings = new TorrentSettingsBuilder
				{
					MaximumConnections = 60,
				};

				magnetInfo.TorrentManager = await Engine.AddAsync(magnetLink, Globals.DirectoryDownloads, torrentSettings.ToSettings());

				Console.WriteLine($"{assetType}	{magnetInfo.Version}	{magnetInfo.Name}");

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
						await manager.WaitForMetadataAsync(Cancellation.Token);
						await manager.StopAsync();
					}

					Console.WriteLine($"{name}	Starting Priority	{manager.Files[0].Priority}");

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

					Console.WriteLine($"{name}	READY	{manager.Files.Count}");
				}));
			}

			await Task.WhenAll(managerTasks);

			Globals.ReadyTime = DateTime.Now;

			//
			// Processing
			//
			Tools.ConsoleHeading(1, $"All Torrents Ready");

			while (Engine.IsRunning)
			{
				await Task.Delay(5000, Cancellation.Token);

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
					$"",
					$"connections:{Engine.ConnectionManager.OpenConnections}    download rate:{Engine.TotalDownloadRate}    upload rate:{Engine.TotalUploadRate}    received:{dataBytesReceived}    sent:{dataBytesSent}",
				});

				foreach (TorrentManager manager in Engine.Torrents)
				{
					string name = manager.Name.PadRight(pad);

					Console.WriteLine($"{name}	{manager.State}	{manager.Monitor.DataBytesReceived}	{manager.Monitor.DataBytesSent}");
				}
			}


		}

	}
}
