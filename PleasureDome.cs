using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

using HtmlAgilityPack;
using Newtonsoft.Json;

namespace dome_bt
{
	public class PleasureDome
	{
		private static string MagnetKey = "RRt08v+YWc2+910RGOhZO7DrNVnHKae8MDJyJNOd950=";

		public static void ParseMagentLinks()
		{
			Tools.ConsoleHeading(1, new string[] { "Magnet Scrape" });

			if (Globals.Cores.Contains("mame"))
				ParseMagentLinks("https://data.spludlow.co.uk/api/magnets/mame",
					new AssetType[] { AssetType.MachineRom, AssetType.MachineDisk, AssetType.SoftwareRom, AssetType.SoftwareDisk },
					new List<string>(new string[] { "ROMs (merged)", "CHDs (merged)", "Software List ROMs (merged)", "Software List CHDs (merged)" }),
					Globals.Magnets);

			if (Globals.Cores.Contains("hbmame"))
				ParseMagentLinks("https://data.spludlow.co.uk/api/magnets/hbmame",
					new AssetType[] { AssetType.HbMameMachineRom, AssetType.HbMameSoftwareRom, },
					new List<string>(new string[] { "ROMs (merged)", "Software List ROMs (merged)", }),
					Globals.Magnets);
		}

		public static void ParseMagentLinks(string url, AssetType[] assetTypes, List<string> names, Dictionary<AssetType, MagnetInfo> magnets)
		{
			Console.WriteLine(url);

			dynamic json = JsonConvert.DeserializeObject<dynamic>(Tools.FetchCached(url) ?? throw new ApplicationException("Can't fetch Magnets"));

			string html;
			using (var aes = Aes.Create())
			{
				aes.Key = Convert.FromBase64String(MagnetKey);
				aes.IV = Convert.FromBase64String((string)json.iv);
				aes.Mode = CipherMode.CBC;
				aes.Padding = PaddingMode.PKCS7;

				using (var decryptor = aes.CreateDecryptor())
					using (var stream = new MemoryStream(Convert.FromBase64String((string)json.body)))
						using (var cryStream = new CryptoStream(stream, decryptor, CryptoStreamMode.Read))
							using (var reader = new StreamReader(cryStream))
								html = reader.ReadToEnd();
			}

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
