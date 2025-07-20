using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.PortForwarding;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace dome_bt
{
	public class WebServer
	{
		private readonly byte[] _FavIcon = Convert.FromBase64String(@"
			AAABAAEAEBAAAAAAGABoAwAAFgAAACgAAAAQAAAAIAAAAAEAGAAAAAAAAAMAAAAAAAAAAAAAAAAA
			AAAAAAD0tgDzuQDzsgD2xgD99NT++OP++OX++OX/+OPA67QA6t3j6KL/9tr++OP9+OX9+OX0vQD0
			vgD99dj///T/75P/6m7/6mv/6Wz/4ne+3G4A7Obg2EL/3F7/3Vv/32v84nnysAD99+P/9MThrQCV
			aACCXQCCXQCgcgDyoQC9vwAA8PesvwCDyQB/ygDQswD/rQD0uwD//e/vsgBEMgAJDiUdGh8bGh8H
			DCZzTADEwwAA8/8A8/8A8/8A8/8A8fjBwwD+/PX/1gC+hgAUFiLCjQDvrQDysACgdgAsGgyxtQAA
			+P873pbetQDbtQAN5LcA79X//vv2uwDkogDQlwDoqADdoADlpwCRawAtGwuwtgAA9v7AvAD/qgD/
			qQCpwgAA+f/+/PXztQD9tQCqfQAgHBwUFiIWFiIFCid8UgDAwwAA8PfXtgD3rQD7rAC+vQAA9//+
			/PX4ugDYmwAbGR9cRgCZcQCRagCtfwD/swC9wQAA8PvUtwD5rQD8rAC9vQAA+P///fn+wgC2gwAX
			FyHqqgD/xAD/xADcnwB8UwCytwAA9/+MywD/qAD/qAB10ToA9////fX7zwDYmAAeGx5vVACgdgCi
			dwBRPgA2IQG5vAAA9v8A8f9z0URv0kkA9v9p2Vj76Jv977v7sgCQaQASEyITFCISEyIdGh+6fwDH
			xQAA7uwg4a4A8/8A9P9U12/7swDzuQD//fn1wAD2rgDbngDUmQDTmQDhowD6swDqsQDSuADyrwDX
			tgDVswD5sgD/7KDxrgD977/98MbzsAD3sAD4swD4swD2sgDyrwD0rgD5rQD0rwD3qQD5swD+8MD/
			/vPxrADysAD+/fX75Y7ysgDxqwDyrgDyrwDyrwDyrwDyrgDxqgDztQD977n99+D0swDyrwDxqwDz
			sgD//fn98sz0vwDyrgDxqwDxqgDxqwDyrwD1xQD9+OL+/PXysgD1rADyrwDyrwDxrQDztQD889D/
			/fn989P75pT53mj76J399dv//fn87rjzswDyrADxrAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
			AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
		");

		public void StartListener()
		{
			if (HttpListener.IsSupported == false)
			{
				Console.WriteLine("!!! Http Listener Is not Supported");
				return;
			}

			HttpListener listener = new HttpListener();
			listener.Prefixes.Add(Globals.ListenAddress);
			listener.Start();

			Task listenTask = new Task(() => {

				while (true)
				{
					HttpListenerContext context = listener.GetContext();

					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

					context.Response.Headers["Content-Type"] = "application/json; charset=utf-8";

					string path = context.Request.Url.AbsolutePath.ToLower();

					using (StreamWriter writer = new StreamWriter(context.Response.OutputStream, new UTF8Encoding(false)))
					{
						try
						{
							if (context.Request.HttpMethod == "OPTIONS")
							{
								context.Response.Headers.Add("Allow", "OPTIONS, GET");
							}
							else
							{
								if (path.StartsWith("/api/") == true)
								{
									MethodInfo method = GetType().GetMethod(path.Replace("/", "_"));

									if (method == null)
									{
										ApplicationException exception = new ApplicationException($"Not found: {path}");
										exception.Data.Add("status", 404);
										throw exception;
									}

									method.Invoke(this, new object[] { context, writer });

								}
								else
								{
									switch (path)
									{
										case "/favicon.ico":
											context.Response.Headers["Content-Type"] = "image/x-icon";
											context.Response.OutputStream.Write(_FavIcon, 0, _FavIcon.Length);
											break;

										default:
											context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
											writer.WriteLine(HTML);
											break;
									}
								}
							}

						}
						catch (Exception e)
						{
							if (e is TargetInvocationException && e.InnerException != null)
								e = e.InnerException;

							Tools.ConsoleHeading(2, new string[] { "Web Request Error", context.Request.Url.PathAndQuery, e.Message });
							Console.WriteLine(e.ToString());

							ErrorResponse(context, writer, e);
						}
					}
				}
			});

			listenTask.Start();
		}

		private void ErrorResponse(HttpListenerContext context, StreamWriter writer, Exception e)
		{
			int status = 500;

			if (e is ApplicationException)
				status = 400;

			if (e.Data["status"] != null)
				status = (int)e.Data["status"];

			context.Response.StatusCode = status;

			dynamic json = new JObject();

			json.status = status;
			json.message = e.Message;
			json.error = e.ToString();

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_stop(HttpListenerContext context, StreamWriter writer)
		{
			Globals.BitTorrent.AskStop = true;

			dynamic json = new JObject();

			json.message = "OK";

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_info(HttpListenerContext context, StreamWriter writer)
		{
			//	http://localhost:12381/api/info

			ClientEngine engine = Globals.BitTorrent.Engine;

			dynamic json = new JObject();

			json.version = Globals.AssemblyVersion;
			json.pid = Globals.Pid;
			json.is_running = engine.IsRunning;

			json.half_open_connections = engine.ConnectionManager.HalfOpenConnections;
			json.open_connections = engine.ConnectionManager.OpenConnections;

			json.total_download_rate = engine.TotalDownloadRate;
			json.total_upload_rate = engine.TotalUploadRate;

			json.total_download_rate_text = Tools.DataSizeText(engine.TotalDownloadRate);
			json.total_upload_rate_text = Tools.DataSizeText(engine.TotalUploadRate);

			long dataBytesReceived = 0;
			long dataBytesSent = 0;
			foreach (TorrentManager manager in engine.Torrents)
			{
				dataBytesReceived += manager.Monitor.DataBytesReceived;
				dataBytesSent += manager.Monitor.DataBytesSent;
			}

			json.total_bytes_received = dataBytesReceived;
			json.total_bytes_sent = dataBytesSent;

			json.total_bytes_received_text = Tools.DataSizeText(dataBytesReceived);
			json.total_bytes_sent_text = Tools.DataSizeText(dataBytesSent);

			json.start_time = Globals.StartTime;

			if (Globals.ReadyTime != Globals.StartTime)
				json.ready_minutes = (Globals.ReadyTime - Globals.StartTime).TotalMinutes;

			json.time_now = DateTime.Now;
			json.run_time_text = Tools.TimeTookText(DateTime.Now - Globals.StartTime);

			json.dht_state = engine.Dht.State.ToString();

			json.open_files = engine.DiskManager.OpenFiles;
			
			json.cache_directory = engine.Settings.CacheDirectory;

			json.priorities = new JArray(Enum.GetNames(typeof(Priority)));

			//
			// Magnets
			//

			dynamic magnets = new JArray();
			foreach (AssetType magnetType in Globals.Magnets.Keys)
			{
				MagnetInfo info = Globals.Magnets[magnetType];

				dynamic result = new JObject();

				result.type = magnetType.ToString();
				result.name = info.Name;
				result.version = info.Version;
				result.hash = info.Hash;
				result.magnet_link = info.Magnet;

				result.torrent_available = info.TorrentManager != null ? true : false;

				if (info.MagnetLink != null)
				{
					result.announce_urls = new JArray(info.MagnetLink.AnnounceUrls);
					result.webseeds = new JArray(info.MagnetLink.Webseeds);
				}

				magnets.Add(result);
			}
			json.magnets = magnets;

			//
			// Peer Listeners
			//
			JArray peerListeners = new JArray();
			foreach (var listener in engine.PeerListeners)
			{
				dynamic listen = new JObject();
				if (listener.LocalEndPoint != null)
				{
					listen.local_address = listener.LocalEndPoint.Address.ToString();
					listen.local_port = listener.LocalEndPoint.Port;
				}
				if (listener.PreferredLocalEndPoint != null)
				{
					listen.preferred_local_address = listener.PreferredLocalEndPoint.Address.ToString();
					listen.preferred_local_port = listener.PreferredLocalEndPoint.Port;
				}
				listen.status = listener.Status.ToString();
				peerListeners.Add(listen);
			}
			json.peer_listeners = peerListeners;

			//
			// Port Mappings
			//
			string[] portMappingsNames = new string[] { "Created", "Pending", "Failed" };
			IReadOnlyList<Mapping>[] mappingsList = new IReadOnlyList<Mapping>[] { engine.PortMappings.Created, engine.PortMappings.Pending, engine.PortMappings.Failed };

			JArray portMappings = new JArray();
			for (int index = 0; index < mappingsList.Length; index++)
			{
				string portMappingsName = portMappingsNames[index];
				IReadOnlyList<Mapping> mappings = mappingsList[index];

				JArray mapsArray = new JArray();

				foreach (Mapping mapping in mappings)
				{
					dynamic map = new JObject();
					map.public_port = mapping.PublicPort;
					map.public_port = mapping.PublicPort;
					map.protocol = mapping.Protocol.ToString();
					mapsArray.Add(map);
				}

				dynamic maps = new JObject();
				maps.name = portMappingsName;
				maps.mappings = mapsArray;
				portMappings.Add(maps);
			}
			json.port_mappings = portMappings;

			//
			// Torrents
			//
			dynamic torrents = new JArray();
			foreach (var torrentManager in engine.Torrents)
			{
				dynamic result = new JObject();

				result.name = torrentManager.Name;
				result.hash = torrentManager.MagnetLink.InfoHashes.V1OrV2.ToHex();
				result.file_count = torrentManager.Files.Count;
				result.state = torrentManager.State.ToString();
				result.has_metadata = torrentManager.HasMetadata;

				if (torrentManager.Error != null)
					result.error = torrentManager.Error.Exception.ToString();

				result.open_connections = torrentManager.OpenConnections;

				result.peers_available = torrentManager.Peers.Available;
				result.peers_leechs = torrentManager.Peers.Leechs;
				result.peers_seeds = torrentManager.Peers.Seeds;

				result.bytes_received = torrentManager.Monitor.DataBytesReceived;
				result.bytes_sent = torrentManager.Monitor.DataBytesSent;

				result.bytes_received_text = Tools.DataSizeText(torrentManager.Monitor.DataBytesReceived);
				result.bytes_sent_text = Tools.DataSizeText(torrentManager.Monitor.DataBytesSent);

				torrents.Add(result);
			}
			json.torrents = torrents;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_files(HttpListenerContext context, StreamWriter writer)
		{
			string hash = context.Request.QueryString["hash"] ?? throw new ApplicationException("hash not passed");

			string priority = context.Request.QueryString["priority"];

			TorrentManager manager;
			lock (Globals.BitTorrent._Lock)
			{
				if (Globals.BitTorrent.TorrentManagers.ContainsKey(hash) == false)
					throw new ApplicationException("hash not found");

				manager = Globals.BitTorrent.TorrentManagers[hash];
			}

			var managerFiles = manager.Files;
			if (priority != null)
				managerFiles = manager.Files.Where(f => f.Priority == (Priority)Enum.Parse(typeof(Priority), priority)).ToList();

			JArray files = new JArray();

			foreach (var fileInfo in managerFiles)
			{
				dynamic file = new JObject();

				file.path = fileInfo.Path;
				file.priority = fileInfo.Priority.ToString();
				file.length = fileInfo.Length;
				file.piece_count = fileInfo.PieceCount;
				file.percent_complete = fileInfo.BitField.PercentComplete;

				files.Add(file);
			}

			writer.WriteLine(files.ToString(Formatting.Indented));
		}

		public void _api_file(HttpListenerContext context, StreamWriter writer)
		{
			//	MachineRom		http://localhost:12381/api/file?machine=@
			//	MachineDisk		http://localhost:12381/api/file?machine=@&disk=@
			//	SoftwareRom		http://localhost:12381/api/file?list=@&software=@
			//	SoftwareDisk	http://localhost:12381/api/file?list=@&software=@&disk=@

			//	HbMameMachineRom		http://localhost:12381/api/file?core=hbmame&machine=@
			//	HbMameSoftwareRom		http://localhost:12381/api/file?core=hbmame&list=@&software=@

			Dictionary<string, string> parameters = new Dictionary<string, string>();

			foreach (string valid in new string[] { "core", "machine", "list", "disk", "software" })
			{
				string qs = context.Request.QueryString[valid];
				if (qs != null)
					parameters.Add(valid, qs);
			}
			if (parameters.ContainsKey("core") == false)
				parameters.Add("core", "mame");

			bool isMame = parameters["core"] == "mame";	//	else HBMAME for now

			AssetType type;
			string path;

			if (parameters.Count == 2 && parameters.ContainsKey("machine"))
			{
				type = isMame ? AssetType.MachineRom : AssetType.HbMameMachineRom;
				path = parameters["machine"] + ".zip";
			}
			else
			{
				if (parameters.Count == 3 && parameters.ContainsKey("machine") && parameters.ContainsKey("disk"))
				{
					type = AssetType.MachineDisk;
					path = Path.Combine(parameters["machine"], parameters["disk"] + ".chd");
				}
				else
				{
					if (parameters.Count == 3 && parameters.ContainsKey("list") && parameters.ContainsKey("software"))
					{
						type = isMame ? AssetType.SoftwareRom : AssetType.HbMameSoftwareRom;
						path = Path.Combine(parameters["list"], parameters["software"] + ".zip");
					}
					else
					{
						if (parameters.Count == 4 && parameters.ContainsKey("list") && parameters.ContainsKey("software") && parameters.ContainsKey("disk"))
						{
							type = AssetType.SoftwareDisk;
							path = Path.Combine(parameters["list"], parameters["software"], parameters["disk"] + ".chd");
						}
						else
						{
							throw new ApplicationException("Bad Parameters");
						}
					}
				}
			}

			TorrentManager manager = Globals.Magnets[type].TorrentManager ?? throw new ApplicationException("Torrent Manager Not available");

			// TODO: May be threading issues here (very intermittant)

			IEnumerable<ITorrentManagerFile> files = manager.Files.Where(f => f.Path == path);

			if (files.Count() == 0)
			{
				ApplicationException exception = new ApplicationException($"Not found {type}\t{path}");
				exception.Data["status"] = 404;
				throw exception;
			}

			ITorrentManagerFile fileInfo = files.Single();

			if (fileInfo.Priority != Priority.Highest)
				manager.SetFilePriorityAsync(fileInfo, Priority.Highest).Wait();

			if (fileInfo.BitField.PercentComplete == 100.0D)
				Globals.BitTorrent.Engine.DiskManager.FlushAsync(manager).Wait();

			dynamic file = new JObject();

			file.path = fileInfo.Path;
			file.percent_complete = fileInfo.BitField.PercentComplete;
			file.length = fileInfo.Length;
			file.priority = fileInfo.Priority.ToString();
			file.filename = fileInfo.FullPath;
			file.piece_start_index = fileInfo.StartPieceIndex;
			file.piece_count = fileInfo.PieceCount;

			writer.WriteLine(file.ToString(Formatting.Indented));

		}

		private string HTML = @"
<html>
<head>
<title>DOME-BT</title>
</head>
<body>

<h1>DOME-BT</h1>

<p>Welcome to DOME-BT a Bit Torrent client for obtaining MAME assets from the Pleasuredome Torrents.</p>

<p><a href=""https://github.com/sam-ludlow/dome-bt"" target=""_blank"" >https://github.com/sam-ludlow/dome-bt</a></p>

<p><a href=""https://pleasuredome.github.io/pleasuredome/mame/index.html"" target=""_blank"" >https://pleasuredome.github.io/pleasuredome/mame/index.html</a></p>

<p><a href=""https://pleasuredome.github.io/pleasuredome/nonmame/hbmame/index.html"" target=""_blank"" >https://pleasuredome.github.io/pleasuredome/nonmame/hbmame/index.html</a></p>

<p>When DOME-BT is running with MAME-AO the assets will be automatically obtained from Bit Torrents rather than using archive.org</p>

<a href=""https://github.com/sam-ludlow/mame-ao"" target=""_blank"" >https://github.com/sam-ludlow/mame-ao</a>

<h2>DOME-BT URLS</h2>

<h3>Root (this page)</h3>
<a href=""http://localhost:12381/"" target=""_blank"" >http://localhost:12381/</a>

<h3>Torrents' info</h3>
<a href=""http://localhost:12381/api/info"" target=""_blank"" >http://localhost:12381/api/info</a>

<hr />

<h3>Download</h3>
<p>To begin a download, perform a GET you can then poll the same URL until the file is downloaded.</p>

<hr />

<h4>MAME - Machine Rom</h4>
<a href=""http://localhost:12381/api/file?machine=@"" target=""_blank"" >http://localhost:12381/api/file?machine=@</a>

<h4>MAME - Machine Disk</h4>
<a href=""http://localhost:12381/api/file?machine=@&disk=@"" target=""_blank"" >http://localhost:12381/api/file?machine=@&disk=@</a>

<h4>MAME - Software Rom</h4>
<a href=""http://localhost:12381/api/file?list=@&software=@"" target=""_blank"" >http://localhost:12381/api/file?list=@&software=@</a>

<h4>MAME - Software Disk</h4>
<a href=""http://localhost:12381/api/file?list=@&software=@&disk=@"" target=""_blank"" >http://localhost:12381/api/file?list=@&software=@&disk=@</a>

<hr />

<h4>HBMAME - Machine Rom</h4>
<a href=""http://localhost:12381/api/file?core=hbmame&machine=@"" target=""_blank"" >http://localhost:12381/api/file?core=hbmame&machine=@</a>

<h4>HBMAME - Software Rom</h4>
<a href=""http://localhost:12381/api/file?core=hbmame&list=@&software=@"" target=""_blank"" >http://localhost:12381/api/file?core=hbmame&list=@&software=@</a>

<hr />

<h4>Torrent File List</h4>
<a href=""http://localhost:12381/api/files?hash=@&priority=@"" target=""_blank"" >http://localhost:12381/api/files?hash=@&priority=@</a>

</body>
</html>
";
	}
}
