using System;
using System.Collections.Generic;

using HtmlAgilityPack;

namespace dome_bt
{
	public class PleasureDome
	{
		public static MagnetInfo[] ParseMagentLink(string core, string url)
		{
			Tools.ConsoleHeading(1, new string[] { "Magnet Scrape" });

			List<MagnetInfo> results = new List<MagnetInfo>();

			Console.WriteLine($"{core}\t{url}");

			string html = Tools.FetchCached(url) ?? throw new ApplicationException("Can't fetch Magnet page");

			HtmlDocument doc = new HtmlDocument();
			doc.LoadHtml(html);

			foreach (HtmlNode node in doc.DocumentNode.Descendants())
			{
				if (node.Name != "a")
					continue;

				string href = node.Attributes["href"].DeEntitizeValue;

				if (href.StartsWith("magnet:") == false)
					continue;

				string name = node.InnerText;

				string text = node.InnerText;
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

				results.Add(new MagnetInfo(name, version, href, type));
			}

			return results.ToArray();
		}
	}
}
