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

namespace dome_bt
{
	public class BitTorrent
	{
		public ITorrentManagerFile requiredFile = null;

		public BitTorrent()
		{

		}

		public void Start()
		{
			foreach (AssetType assetType in Globals.Magnets.Keys)
			{

			}

			string magnetLink = Globals.Magnets[AssetType.MachineRom].Magnet;



			CancellationTokenSource cancellation = new CancellationTokenSource();

			var task = MainAsync(magnetLink, cancellation.Token);

			Console.CancelKeyPress += delegate { cancellation.Cancel(); task.Wait(); };
			AppDomain.CurrentDomain.ProcessExit += delegate { cancellation.Cancel(); task.Wait(); };

			AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); cancellation.Cancel(); task.Wait(); };
			Thread.GetDomain().UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); cancellation.Cancel(); task.Wait(); };

			task.Wait();
		}

		async Task MainAsync(string magnetLink, CancellationToken token)
		{
			IPAddress externalIPAddress = IPAddress.Parse("217.40.212.83");
			int portNumber = 55123;

			var settingBuilder = new EngineSettingsBuilder
			{
				AllowPortForwarding = false,
				AutoSaveLoadDhtCache = true,
				AutoSaveLoadFastResume = true,
				AutoSaveLoadMagnetLinkMetadata = true,

				//CacheDirectory

				ListenEndPoints = new Dictionary<string, IPEndPoint> {
			{ "ipv4", new IPEndPoint (IPAddress.Any, portNumber) },
			{ "ipv6", new IPEndPoint (IPAddress.IPv6Any, portNumber) }
		},

				DhtEndPoint = new IPEndPoint(IPAddress.Any, portNumber),

				ReportedListenEndPoints = new Dictionary<string, IPEndPoint> {
			{ "ipv4", new IPEndPoint( externalIPAddress, portNumber) }
		},

				HttpStreamingPrefix = "http://127.0.0.1:55125/"
			};

			using var engine = new ClientEngine(settingBuilder.ToSettings());


			MagnetLink link;
			if (MagnetLink.TryParse(magnetLink, out link) == false)
				throw new ApplicationException($"Bad magnet link: {magnetLink}");

			Task task = DownloadAsync(engine, link, token);

			try
			{
				await task;
			}
			catch (OperationCanceledException)
			{

			}

			foreach (var manager in engine.Torrents)
			{
				var stoppingTask = manager.StopAsync();
				while (manager.State != TorrentState.Stopped)
				{
					Console.WriteLine("{0} is {1}", manager.Torrent.Name, manager.State);
					await Task.WhenAll(stoppingTask, Task.Delay(250));
				}
				await stoppingTask;
				if (engine.Settings.AutoSaveLoadFastResume)
					Console.WriteLine($"FastResume data for {manager.Torrent?.Name ?? manager.InfoHashes.V1?.ToHex() ?? manager.InfoHashes.V2?.ToHex()} has been written to disk.");
			}

			if (engine.Settings.AutoSaveLoadDhtCache)
				Console.WriteLine($"DHT cache has been written to disk.");

			if (engine.Settings.AllowPortForwarding)
				Console.WriteLine("uPnP and NAT-PMP port mappings have been removed");
		}

		async Task DownloadAsync(ClientEngine engine, MagnetLink link, CancellationToken token)
		{
			var settingsBuilder = new TorrentSettingsBuilder
			{
				MaximumConnections = 60,
			};

			await engine.AddStreamingAsync(link, "downloads", settingsBuilder.ToSettings());

			Top10Listener listener = new Top10Listener(10);

			foreach (TorrentManager manager in engine.Torrents)
			{
				manager.PeersFound += (o, e) =>
				{
					listener.WriteLine(string.Format($"{e.GetType().Name}: {e.NewPeers} peers for {e.TorrentManager.Name}"));
				};
				manager.PeerConnected += (o, e) =>
				{
					lock (listener)
						listener.WriteLine($"Connection succeeded: {e.Peer.Uri}");
				};
				manager.ConnectionAttemptFailed += (o, e) =>
				{
					lock (listener)
						listener.WriteLine(
							$"Connection failed: {e.Peer.ConnectionUri} - {e.Reason}");
				};
				manager.PieceHashed += delegate (object o, PieceHashedEventArgs e)
				{
					lock (listener)
						listener.WriteLine($"Piece Hashed: {e.PieceIndex} - {(e.HashPassed ? "Pass" : "Fail")}");
				};
				manager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e)
				{
					lock (listener)
						listener.WriteLine($"OldState: {e.OldState} NewState: {e.NewState}");
				};
				manager.TrackerManager.AnnounceComplete += (sender, e) =>
				{
					listener.WriteLine($"{e.Successful}: {e.Tracker}");
				};


				string requiredPath = "005.zip";

				if (manager.Files.Count == 0)
				{
					Console.Write("First time get files...");
					await manager.StartAsync();
					Console.WriteLine("...done. A");
					await manager.WaitForMetadataAsync(token);
					Console.WriteLine("...done. B");
					await manager.StopAsync();
					Console.WriteLine("...done.");
				}

				Console.Write("Setting Priorities...");

				requiredFile = manager.Files.Where(f => f.Path == requiredPath).Single();
				await manager.SetFilePriorityAsync(requiredFile, Priority.Highest);

				foreach (var file in manager.Files.Where(f => f.Path != requiredPath))
					await manager.SetFilePriorityAsync(file, Priority.DoNotDownload);

				Console.WriteLine("...done.");

				Console.Write("Starting...");
				await manager.StartAsync();
				Console.WriteLine("...done.");
			}


			StringBuilder sb = new StringBuilder(1024);
			while (engine.IsRunning && requiredFile.BitField.PercentComplete != 100)
			{
				sb.Remove(0, sb.Length);

				AppendFormat(sb, $"Transfer Rate:      {engine.TotalDownloadRate / 1024.0:0.00}kB/sec ↓ / {engine.TotalUploadRate / 1024.0:0.00}kB/sec ↑");
				AppendFormat(sb, $"Memory Cache:       {engine.DiskManager.CacheBytesUsed / 1024.0:0.00}/{engine.Settings.DiskCacheBytes / 1024.0:0.00} kB");
				AppendFormat(sb, $"Disk IO Rate:       {engine.DiskManager.ReadRate / 1024.0:0.00} kB/s read / {engine.DiskManager.WriteRate / 1024.0:0.00} kB/s write");
				AppendFormat(sb, $"Disk IO Total:      {engine.DiskManager.TotalBytesRead / 1024.0:0.00} kB read / {engine.DiskManager.TotalBytesWritten / 1024.0:0.00} kB written");
				AppendFormat(sb, $"Open Files:         {engine.DiskManager.OpenFiles} / {engine.DiskManager.MaximumOpenFiles}");
				AppendFormat(sb, $"Open Connections:   {engine.ConnectionManager.OpenConnections}");
				AppendFormat(sb, $"DHT State:          {engine.Dht.State}");

				// Print out the port mappings
				foreach (var mapping in engine.PortMappings.Created)
					AppendFormat(sb, $"Successful Mapping    {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
				foreach (var mapping in engine.PortMappings.Failed)
					AppendFormat(sb, $"Failed mapping:       {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");
				foreach (var mapping in engine.PortMappings.Pending)
					AppendFormat(sb, $"Pending mapping:      {mapping.PublicPort}:{mapping.PrivatePort} ({mapping.Protocol})");

				foreach (TorrentManager manager in engine.Torrents)
				{
					AppendSeparator(sb);
					AppendFormat(sb, $"State:              {manager.State}");
					AppendFormat(sb, $"Name:               {(manager.Torrent == null ? "MetaDataMode" : manager.Torrent.Name)}");
					AppendFormat(sb, $"Progress:           {manager.Progress:0.00}");
					AppendFormat(sb, $"Transferred:        {manager.Monitor.DataBytesReceived / 1024.0 / 1024.0:0.00} MB ↓ / {manager.Monitor.DataBytesSent / 1024.0 / 1024.0:0.00} MB ↑");
					AppendFormat(sb, $"Tracker Status");
					foreach (var tier in manager.TrackerManager.Tiers)
						AppendFormat(sb, $"\t{tier.ActiveTracker} : Announce Succeeded: {tier.LastAnnounceSucceeded}. Scrape Succeeded: {tier.LastScrapeSucceeded}.");

					if (manager.PieceManager != null)
						AppendFormat(sb, "Current Requests:   {0}", await manager.PieceManager.CurrentRequestCountAsync());

					var peers = await manager.GetPeersAsync();
					AppendFormat(sb, "Outgoing:");
					foreach (PeerId p in peers.Where(t => t.ConnectionDirection == Direction.Outgoing))
					{
						AppendFormat(sb, $"\t{p.AmRequestingPiecesCount} - {(p.Monitor.DownloadRate / 1024.0):0.00}/{(p.Monitor.UploadRate / 1024.0):0.00}kB/sec - {p.Uri} - {p.EncryptionType}");
					}
					AppendFormat(sb, "");
					AppendFormat(sb, "Incoming:");
					foreach (PeerId p in peers.Where(t => t.ConnectionDirection == Direction.Incoming))
					{
						AppendFormat(sb, $"\t{p.AmRequestingPiecesCount} - {(p.Monitor.DownloadRate / 1024.0):0.00}/{(p.Monitor.UploadRate / 1024.0):0.00}kB/sec - {p.Uri} - {p.EncryptionType}");
					}

					AppendFormat(sb, "", null);
					if (manager.Torrent != null)
					{
						//foreach (var file in manager.Files)

						AppendFormat(sb, "{1:0.00}% - {0}", requiredFile.Path, requiredFile.BitField.PercentComplete);
					}

				}
				//Console.Clear();
				Console.WriteLine(sb.ToString());

				listener.ExportTo(Console.Out);

				await Task.Delay(5000, token);

			}

			Console.Write("Stopping...");
			await engine.StopAllAsync();
			Console.WriteLine("...done.");

		}

		static void AppendFormat(StringBuilder sb, string str, params object[] formatting)
		{
			if (formatting != null && formatting.Length > 0)
				sb.AppendFormat(str, formatting);
			else
				sb.Append(str);
			sb.AppendLine();
		}

		static void AppendSeparator(StringBuilder sb)
		{
			AppendFormat(sb, "");
			AppendFormat(sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");
			AppendFormat(sb, "");
		}
	}

	public class Top10Listener : TraceListener
	{
		private readonly int capacity;
		private readonly LinkedList<string> traces;

		public Top10Listener(int capacity)
		{
			this.capacity = capacity;
			this.traces = new LinkedList<string>();
		}

		public override void Write(string message)
		{
			lock (traces)
				traces.Last.Value += message;
		}

		public override void WriteLine(string message)
		{
			lock (traces)
			{
				if (traces.Count >= capacity)
					traces.RemoveFirst();

				traces.AddLast(message);
			}
		}

		public void ExportTo(TextWriter output)
		{
			lock (traces)
				foreach (string s in this.traces)
					output.WriteLine(s);
		}
	}

	public class TextWriterLogger : IRootLogger
	{
		TextWriter Writer { get; }

		public TextWriterLogger(TextWriter writer)
			=> Writer = writer;

		public void Debug(string prefix, string message)
		{
			Writer?.WriteLine($"DEBUG:{prefix}:{message}");
		}

		public void Error(string prefix, string message)
		{
			Writer?.WriteLine($"ERROR:{prefix}:{message}");
		}

		public void Info(string prefix, string message)
		{
			Writer?.WriteLine($"INFO: {prefix}:{message}");
		}
	}

	public interface IRootLogger
	{
		void Info(string name, string message);
		void Debug(string name, string message);
		void Error(string name, string message);
	}
}
