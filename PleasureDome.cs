using System;
using System.Collections.Generic;
using System.Linq;

using HtmlAgilityPack;
using MonoTorrent;

namespace dome_bt
{
	public class PleasureDome
	{
		public static TorrentInfo[] ParseMameMagentLink(string core, string url)
		{
			Tools.ConsoleHeading(1, new string[] { "Magnet Scrape" });

			List<TorrentInfo> results = new List<TorrentInfo>();

			Console.WriteLine($"{core}\t{url}");

			string html = Tools.FetchCached(url) ?? throw new ApplicationException("Can't fetch Magnet page");

			HtmlDocument doc = new HtmlDocument();
			doc.LoadHtml(html);

			foreach (string[] textHref in ExtractLinks(url))
			{
				string text = textHref[0];
				string href = textHref[1];

				if (href.StartsWith("magnet:") == false)
					continue;

				string name = text;

				int index;

				index = text.IndexOf(' ');
				if (text.Substring(0, index).ToLower() != core)
					throw new ApplicationException("core mismatch");
				text = text.Substring(index + 1);

				index = text.IndexOf(' ');
				string version = text.Substring(0, index);
				text = text.Substring(index + 1);

				if (Globals.NameTypeLookup.ContainsKey(text) == false)
					continue;

				string type = Globals.NameTypeLookup[text];

				Console.WriteLine($"\t{type}\t{version}\t{name}");

				results.Add(new TorrentInfo()
				{
					Name = name,
					Type = type,
					Version = version,
					Magnet = href,
				});
			}

			foreach (var info in results)
			{
				info.MagnetLink = MagnetLink.Parse(info.Magnet);
				info.Hash = info.MagnetLink.InfoHashes.V1OrV2.ToHex();
			}

			return results.ToArray();
		}

		public static TorrentInfo[] ParsePinMAMEMagentLink(string url)
		{
			var info = new TorrentInfo();

			foreach (string[] textHref in ExtractLinks(url))
			{
				string text = textHref[0];
				string href = textHref[1];

				if (text.StartsWith("PinMAME ") == false)
					continue;

				if (href.StartsWith("magnet:") == true)
					info.Magnet = href;
				else
					info.DatFile = href;

				if (info.Version == null)
				{
					info.Name = text;

					info.Version = text;
					int index = info.Version.IndexOf(" ");
					info.Version = info.Version.Substring(index + 1);
					index = info.Version.IndexOf(" ");
					info.Version = info.Version.Substring(0, index);
				}
			}

			info.Type = "pinmame";

			info.MagnetLink = MagnetLink.Parse(info.Magnet);
			info.Hash = info.MagnetLink.InfoHashes.V1OrV2.ToHex();

			return new TorrentInfo[] { info };
		}

		public static TorrentInfo[] ParsePinballMagentLink(string url)
		{
			var infos = new Dictionary<string, TorrentInfo>()
			{
				{ "Future", new TorrentInfo() },
				{ "Visual", new TorrentInfo() },
			};

			foreach (string[] textHref in ExtractLinks(url))
			{
				string text = textHref[0];
				string href = textHref[1];

				if (text.Contains(" Pinball (") == false)
					continue;

				foreach (string key in infos.Keys)
				{
					if (text.StartsWith(key) == true)
					{
						var info = infos[key];

						if (href.StartsWith("magnet:") == true)
							info.Magnet = href;
						else
							info.DatFile = href;

						if (info.Version == null)
						{
							info.Name = text;

							info.Version = text;
							int index = info.Version.IndexOf("(");
							info.Version = info.Version.Substring(index + 1);
							index = info.Version.IndexOf(")");
							info.Version = info.Version.Substring(0, index);
						}
					}
				}
			}

			foreach (string key in infos.Keys)
			{
				var info = infos[key];
				info.Type = key.ToLower();
				info.MagnetLink = MagnetLink.Parse(info.Magnet);
				info.Hash = info.MagnetLink.InfoHashes.V1OrV2.ToHex();
			}

			return infos.Values.ToArray();
		}

		private static string[][] ExtractLinks(string url)
		{
			List<string[]> results = new List<string[]>();

			string html = Tools.FetchCached(url) ?? throw new ApplicationException("Can't fetch Magnet page");

			HtmlDocument doc = new HtmlDocument();
			doc.LoadHtml(html);

			foreach (HtmlNode node in doc.DocumentNode.Descendants())
			{
				if (node.Name != "a")
					continue;

				string text = node.InnerText;
				string href = node.Attributes["href"].DeEntitizeValue;

				results.Add(new string[] { text, href });
			}

			return results.ToArray();
		}
	}
}
