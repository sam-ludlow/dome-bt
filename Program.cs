
using dome_bt;

using HtmlAgilityPack;



string html = Tools.FetchCached("https://pleasuredome.github.io/pleasuredome/mame/index.html") ?? throw new ApplicationException("Can't fetch HTML");

HtmlDocument doc = new HtmlDocument();
doc.LoadHtml(html);

Dictionary<string, string> magnetLinks = new Dictionary<string, string>();

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
	int index = version.IndexOf(" ");
	string name = version.Substring(index + 1);
	version = version.Substring(0, index);


	Console.WriteLine($"\t{version}\t{name}\t{node.InnerText}\t{href}");

	

}


