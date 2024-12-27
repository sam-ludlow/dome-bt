using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
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
											writer.WriteLine("Hello");
											break;
									}
								}
							}

						}
						catch (Exception e)
						{
							if (e is TargetInvocationException && e.InnerException != null)
								e = e.InnerException;

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

			dynamic results = new JArray();

			foreach (var torrentManager in Globals.BitTorrent.Engine.Torrents)
			{
				dynamic result = new JObject();

				result.name = torrentManager.Name;
				result.file_count = torrentManager.Files.Count;
				result.state = torrentManager.State.ToString();

				if (torrentManager.Error != null)
					result.error = torrentManager.Error.Exception.ToString();

				dynamic files = new JArray();

				foreach (var fileInfo in torrentManager.Files.Where(f => f.Priority != Priority.DoNotDownload && f.Priority != Priority.Normal))
				{
					dynamic file = new JObject();

					file.path = fileInfo.Path;
					file.priority = fileInfo.Priority.ToString();
					file.percent_complete = fileInfo.BitField.PercentComplete;

					files.Add(file);

				}
				result.files = files;

				results.Add(result);
			}

			dynamic json = new JObject();
			json.offset = 0;
			json.limit = 0;
			json.total = results.Count;
			json.count = results.Count;
			json.results = results;

			writer.WriteLine(json.ToString(Formatting.Indented));
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
				throw new ApplicationException("File not found");

			ITorrentManagerFile fileInfo = files.Single();

			manager.SetFilePriorityAsync(fileInfo, Priority.Highest).Wait();

			dynamic file = new JObject();

			file.path = fileInfo.Path;
			file.priority = fileInfo.Priority.ToString();
			file.percent_complete = fileInfo.BitField.PercentComplete;
			file.length = fileInfo.Length;

			file.filename = Path.Combine(Globals.DirectoryDownloads, manager.Name, fileInfo.Path);

			writer.WriteLine(file.ToString(Formatting.Indented));

		}
	}
}
