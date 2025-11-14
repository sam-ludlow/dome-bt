using System;
using System.Collections.Generic;

using HtmlAgilityPack;

namespace dome_bt
{
	public class PleasureDome
	{
		public static void ParseMagentLinks()
		{
			Tools.ConsoleHeading(1, new string[] { "Pleasuredome Magnet Scrape" });

			if (Globals.Cores.Contains("mame"))
				ParseMagentLinks("https://pleasuredome.github.io/pleasuredome/mame/index.html",
					new AssetType[] { AssetType.MachineRom, AssetType.MachineDisk, AssetType.SoftwareRom, AssetType.SoftwareDisk },
					new List<string>(new string[] { "ROMs (merged)", "CHDs (merged)", "Software List ROMs (merged)", "Software List CHDs (merged)" }),
					Globals.Magnets);

			if (Globals.Cores.Contains("hbmame"))
				ParseMagentLinks("https://pleasuredome.github.io/pleasuredome/nonmame/hbmame/index.html",
					new AssetType[] { AssetType.HbMameMachineRom, AssetType.HbMameSoftwareRom, },
					new List<string>(new string[] { "ROMs (merged)", "Software List ROMs (merged)", }),
					Globals.Magnets);
		}

		public static void ParseMagentLinks(string url, AssetType[] assetTypes, List<string> names, Dictionary<AssetType, MagnetInfo> magnets)
		{
			Console.WriteLine(url);

			string html = Tools.FetchCached(url) ?? throw new ApplicationException("Can't fetch HTML");

			HtmlDocument doc = new HtmlDocument();
			doc.LoadHtml(html);

			foreach (HtmlNode node in doc.DocumentNode.Descendants())
			{
				if (node.Name != "a")
					continue;

				string href = node.Attributes["href"].DeEntitizeValue;

				if (href.StartsWith("magnet:") == false)
					continue;

				string text = node.InnerText;
				int index;

				index = text.IndexOf(' ');
				string core = text.Substring(0, index).ToLower();
				text = text.Substring(index + 1);

				index = text.IndexOf(' ');
				string version = text.Substring(0, index);
				text = text.Substring(index + 1);

				index = names.IndexOf(text);
				if (index != -1)
				{
					magnets.Add(assetTypes[index], new MagnetInfo(node.InnerText, version, href));
					Console.WriteLine($"\t{assetTypes[index]}\t{node.InnerText}\t{version}");
				}
			}
		}
	}
}
