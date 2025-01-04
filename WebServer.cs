﻿using System.Net;
using System.Reflection;
using System.Text;
using System.Web;

using MonoTorrent;
using MonoTorrent.Client;

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
									MethodInfo? method = GetType().GetMethod(path.Replace("/", "_"));

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

							Tools.ConsoleHeading(2, ["Web Request Error", context.Request.Url.PathAndQuery, e.Message]);
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

		public void _api_info(HttpListenerContext context, StreamWriter writer)
		{
			//	http://localhost:12381/api/info

			dynamic json = new JObject();

			json.version = Globals.AssemblyVersion;

			json.half_open_connections = Globals.BitTorrent.Engine.ConnectionManager.HalfOpenConnections;
			json.open_connections = Globals.BitTorrent.Engine.ConnectionManager.OpenConnections;

			json.start_time = Globals.StartTime;

			if (Globals.ReadyTime != Globals.StartTime)
				json.ready_minutes = (Globals.ReadyTime - Globals.StartTime).TotalMinutes;

			dynamic magnets = new JArray();
			foreach (AssetType magnetType in Globals.Magnets.Keys)
			{
				dynamic result = new JObject();

				result.type = magnetType.ToString();
				result.name = Globals.Magnets[magnetType].Name;
				result.version = Globals.Magnets[magnetType].Version;
				result.magnet_link = Globals.Magnets[magnetType].Magnet;
				result.torrent_available = Globals.Magnets[magnetType].TorrentManager != null ? "true" : "false";

				magnets.Add(result);
			}
			json.magnets = magnets;

			dynamic torrents = new JArray();
			foreach (var torrentManager in Globals.BitTorrent.Engine.Torrents)
			{
				dynamic result = new JObject();

				result.name = torrentManager.Name;
				result.file_count = torrentManager.Files.Count;
				result.state = torrentManager.State.ToString();

				if (torrentManager.Error != null)
					result.error = torrentManager.Error.Exception.ToString();

				result.open_connections = torrentManager.OpenConnections;

				result.peers_available = torrentManager.Peers.Available;
				result.peers_leechs = torrentManager.Peers.Leechs;
				result.peers_seeds = torrentManager.Peers.Seeds;

				dynamic files = new JArray();

				foreach (var fileInfo in torrentManager.Files.Where(f => f.Priority != Priority.DoNotDownload && f.Priority != Priority.Normal))
				{
					dynamic file = new JObject();

					file.path = fileInfo.Path;
					file.priority = fileInfo.Priority.ToString();
					file.length = fileInfo.Length;
					file.percent_complete = fileInfo.BitField.PercentComplete;

					files.Add(file);

				}
				result.files = files;

				torrents.Add(result);
			}
			json.torrents = torrents;

			writer.WriteLine(json.ToString(Formatting.Indented));
		}

		public void _api_download(HttpListenerContext context, StreamWriter writer)
		{
			string filename = context.Request.QueryString["filename"] ?? throw new ApplicationException("filename not passed");

			context.Response.Headers["Content-Type"] = "application/octet-stream";
			context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{Path.GetFileName(filename)}\"";

			using (FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
				fileStream.CopyTo(context.Response.OutputStream);
		}

		public void _api_file(HttpListenerContext context, StreamWriter writer)
		{
			//	MachineRom		http://localhost:12381/api/file?machine=@
			//	MachineDisk		http://localhost:12381/api/file?machine=@&disk=@
			//	SoftwareRom		http://localhost:12381/api/file?list=@&software=@
			//	SoftwareDisk	http://localhost:12381/api/file?list=@&software=@&disk=@

			Dictionary<string, string> parameters = new Dictionary<string, string>();

			foreach (string valid in new string[] { "machine", "list", "disk", "software" })
			{
				string? qs = context.Request.QueryString[valid];
				if (qs != null)
					parameters.Add(valid, qs);
			}

			AssetType type;
			string path;

			if (parameters.Count == 1 && parameters.ContainsKey("machine"))
			{
				type = AssetType.MachineRom;
				path = parameters["machine"] + ".zip";
			}
			else
			{
				if (parameters.Count == 2 && parameters.ContainsKey("machine") && parameters.ContainsKey("disk"))
				{
					type = AssetType.MachineDisk;
					path = Path.Combine(parameters["machine"], parameters["disk"] + ".chd");
				}
				else
				{
					if (parameters.Count == 2 && parameters.ContainsKey("list") && parameters.ContainsKey("software"))
					{
						type = AssetType.SoftwareRom;
						path = Path.Combine(parameters["list"], parameters["software"] + ".zip");
					}
					else
					{
						if (parameters.Count == 3 && parameters.ContainsKey("list") && parameters.ContainsKey("software") && parameters.ContainsKey("disk"))
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

			IEnumerable<ITorrentManagerFile> files = manager.Files.Where(f => f.Path == path);

			if (files.Count() == 0)
			{
				ApplicationException exception = new ApplicationException($"Not found {type}\t{path}");
				exception.Data["status"] = 404;
				throw exception;
			}

			ITorrentManagerFile fileInfo = files.Single();

			manager.SetFilePriorityAsync(fileInfo, Priority.Highest).Wait();

			string filename = Path.Combine(Globals.DirectoryDownloads, manager.Name, fileInfo.Path);

			dynamic file = new JObject();

			file.path = fileInfo.Path;
			file.priority = fileInfo.Priority.ToString();
			file.length = fileInfo.Length;
			file.percent_complete = fileInfo.BitField.PercentComplete;

			file.filename = filename;
			file.url = $"{Globals.ListenAddress}api/download?filename={HttpUtility.UrlEncode(filename)}";

			writer.WriteLine(file.ToString(Formatting.Indented));

		}

		private string HTML = @"
<html>
<head>
<title>DOME-BT</title>
</head>
<body>

<h1>DOME-BT</h1>

<p>Welcome to DOME-BT a Bit Torrent client for obtaining MAME assets from the Pleasure Dome Torrents.</p>

<p><a href=""https://github.com/sam-ludlow/dome-bt"" target=""_blank"" >https://github.com/sam-ludlow/dome-bt</a></p>

<p><a href=""https://pleasuredome.github.io/pleasuredome/mame/index.html"" target=""_blank"" >https://pleasuredome.github.io/pleasuredome/mame/index.html</a></p>

<p>When DOME-BT is running with MAME-AO the assets will be automatically obtained from Bit Torrents rather than using archive.org</p>

<a href=""https://github.com/sam-ludlow/mame-ao"" target=""_blank"" >https://github.com/sam-ludlow/mame-ao</a>


<h2>DOME-BT URLS</h2>

<h3>Root (this page)</h3>
<a href=""http://localhost:12381/"" target=""_blank"" >http://localhost:12381/</a>

<h3>Torrents' info</h3>
<a href=""http://localhost:12381/api/info"" target=""_blank"" >http://localhost:12381/api/info</a>

<h3>Download</h3>
<p>To begin a download, perform a GET you can then poll the same URL until the file is downloaded.</p>

<h4>Machine Rom</h4>
<a href=""http://localhost:12381/api/file?machine=@"" target=""_blank"" >http://localhost:12381/api/file?machine=@</a>

<h4>Machine Disk</h4>
<a href=""http://localhost:12381/api/file?machine=@&disk=@"" target=""_blank"" >http://localhost:12381/api/file?machine=@&disk=@</a>

<h4>Software Rom</h4>
<a href=""http://localhost:12381/api/file?list=@&software=@"" target=""_blank"" >http://localhost:12381/api/file?list=@&software=@</a>

<h4>Software Disk</h4>
<a href=""http://localhost:12381/api/file?list=@&software=@&disk=@"" target=""_blank"" >http://localhost:12381/api/file?list=@&software=@&disk=@</a>

</body>
</html>
";
	}
}