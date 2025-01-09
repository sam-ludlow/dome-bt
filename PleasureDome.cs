using HtmlAgilityPack;

namespace dome_bt
{
	public class PleasureDome
	{
		public static void ParseMagentLinks()
		{
			string url = "https://pleasuredome.github.io/pleasuredome/mame/index.html";

			Tools.ConsoleHeading(1, ["Pleasuredome", url ]);

			string html = Tools.FetchCached(url) ?? throw new ApplicationException("Can't fetch HTML");

			HtmlDocument doc = new ();
			doc.LoadHtml(html);

			foreach (HtmlNode node in doc.DocumentNode.Descendants())
			{
				if (node.Name != "a")
					continue;

				string? href = node.Attributes["href"].DeEntitizeValue;

				if (href.StartsWith("magnet:") == false)
					continue;

				string text = node.InnerText;

				if (text.Contains("(merged)") == false)
					continue;

				if (text.StartsWith("MAME 0.") == false)
					throw new ApplicationException("Bad text");

				string version = text.Substring(7);
				int index = version.IndexOf(' ');
				string name = version.Substring(index + 1);
				version = version.Substring(0, index);
				name = name.Replace("(merged)", "").Trim();

				AssetType assetType;

				switch (name.ToLower())
				{
					case "roms":
						assetType = AssetType.MachineRom;
						break;

					case "chds":
						assetType = AssetType.MachineDisk;
						break;

					case "software list roms":
						assetType = AssetType.SoftwareRom;
						break;

					case "software list chds":
						assetType = AssetType.SoftwareDisk;
						break;

					default:
						throw new ApplicationException($"Bad magnet link text");
				}

				if (Globals.Magnets.ContainsKey(assetType) == true)
					throw new ApplicationException($"Duplicate magnet types {assetType}");

				Globals.Magnets.Add(assetType, new MagnetInfo(node.InnerText, version, href));
			}

			if (Globals.Magnets.Count != 4)
				throw new ApplicationException("Did not find all 4 Magnets");
		}
	}
}
