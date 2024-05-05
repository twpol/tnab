using TNAB.Parsers;
using TNAB.Network;

var action = "";
foreach (var arg in args)
{
    if (arg.StartsWith("--"))
    {
        action = arg[2..];
        continue;
    }
    if (arg.StartsWith('/'))
    {
        action = arg[1..];
        continue;
    }
    switch (action)
    {
        // Actions...
        case "print-dom":
            await PrintDom(arg);
            break;
        case "print-nodes":
            await PrintNodes(arg);
            break;
        case "print-tokens":
            await PrintTokens(arg);
            break;
        // Errors...
        case "":
            Console.Error.WriteLine("No action specified for argument: {0}", arg);
            Environment.Exit(0x10001);
            break;
        default:
            Console.Error.WriteLine("Unknown action {1} specified for argument: {0}", arg, action);
            Environment.Exit(0x10002);
            break;
    }
}
if (action == "")
{
    Console.WriteLine("TNAB (The Not As Bad web browser) CLI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  TNAB.Cli [action <URL> [...]] [...]");
    Console.WriteLine();
    Console.WriteLine("Actions:");
    Console.WriteLine("  /print-dom            Print the HTML tree from the specified URLs");
    Console.WriteLine("  /print-nodes          Print the HTML nodes from the specified URLs");
    Console.WriteLine("  /print-tokens         Print the HTML tokens from the specified URLs");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <URL>                 URL to load");
}

async Task PrintDom(string url)
{
    var htmlParser = new HtmlParser(await NetworkManager.Get(new Uri(url)));
    htmlParser.Parse();
    PrintNode(0, htmlParser.Root);
}

async Task PrintNodes(string url)
{
    var htmlNodes = new HtmlParser(await NetworkManager.Get(new Uri(url)));
    foreach (var node in htmlNodes.GetNodes()) Console.WriteLine(node);
}

async Task PrintTokens(string url)
{
    var htmlTokens = new HtmlTokeniser(await NetworkManager.Get(new Uri(url)));
    foreach (var token in htmlTokens.GetTokens()) Console.WriteLine(token);
}

string GetIndent(int level) => level == 0 ? string.Empty : '|' + new string(' ', level * 2 - 1);

void PrintNode(int level, MarkupNode node)
{
    switch (node)
    {
        case MarkupDocument document:
            Console.WriteLine("{0}{1}", GetIndent(level), node.Name);
            if (document.DocType != null) Console.WriteLine("{0}<!{1}>", GetIndent(level + 1), document.DocType);
            foreach (var child in node.Children) PrintNode(level + 1, child);
            break;
        case MarkupText:
            Console.WriteLine("{0}\"{1}\"", GetIndent(level), node.Value);
            foreach (var child in node.Children) PrintNode(level + 1, child);
            break;
        case MarkupElement element:
            Console.WriteLine("{0}<{1}>", GetIndent(level), element.Name);
            foreach (var child in element.Attributes) Console.WriteLine("{0}{1}=\"{2}\"", GetIndent(level + 1), child.Key, child.Value);
            foreach (var child in node.Children) PrintNode(level + 1, child);
            break;
        case MarkupComment:
            Console.WriteLine("{0}<!-- {1} -->", GetIndent(level), node.Value);
            foreach (var child in node.Children) PrintNode(level + 1, child);
            break;
    }
}
