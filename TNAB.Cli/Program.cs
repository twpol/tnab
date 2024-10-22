using TNAB.Browser;
using TNAB.Parsers;
using TNAB.Network;
using TNAB.Layout;
using TNAB.Renderer.Skia;
using SkiaSharp;
using System.Diagnostics;

const string LOGGING_TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";
var lastLog = DateTime.UtcNow;
void Log(string message, string argument = "", long durationMS = -1)
{
    var now = DateTime.UtcNow;
    Console.WriteLine($"{now.ToString(LOGGING_TIMESTAMP_FORMAT)}  {(now - lastLog).TotalMilliseconds,7:+#,0}  {message,-20}  {durationMS,6:#,0; ;}  {argument}");
    lastLog = now;
}

var action = "";
var options = new Dictionary<string, string>();
foreach (var arg in args)
{
    if (arg.StartsWith("--") || arg.StartsWith('/'))
    {
        action = arg[0] == '-' ? arg[2..] : arg[1..];
        switch (action)
        {
            // Options...
            case "verbose":
            case "verbose-cpu":
                options[action] = "";
                action = "";
                break;
        }
    }
    else
    {
        switch (action)
        {
            // Options...
            case "device-pixel-ratio":
            case "screenshot":
            case "viewport":
                options[action] = arg;
                break;
            // Actions...
            case "benchmark":
                await Benchmark(arg);
                break;
            case "crash-test":
            case "crashtest":
                await CrashTest(arg);
                break;
            case "load-document":
            case "reftest":
                await LoadDocument(arg, options);
                break;
            case "print-boxes":
                await PrintBoxes(arg);
                break;
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
}
if (action == "")
{
    Console.WriteLine("TNAB (The Not As Bad web browser) CLI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  TNAB.Cli [options] [action <URL> [...]] [...]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  /device-pixel-ratio <RATIO>  Set the device pixel ratio [default: 1.0]");
    Console.WriteLine("  /screenshot <PATH>    Save a screenshot to the specified path");
    Console.WriteLine("  /verbose              Enable verbose logging");
    Console.WriteLine("  /verbose-cpu          Enable verbose logging of CPU usage");
    Console.WriteLine("  /viewport <WxH>       Set the viewport size [default: 800x600]");
    Console.WriteLine();
    Console.WriteLine("Actions:");
    Console.WriteLine("  /benchmark            Benchmark the HTML/CSS parser with the specified URLs");
    Console.WriteLine("  /crash-test           Crash test the HTML/CSS parser with the specified URLs");
    Console.WriteLine("  /load-document        Load navigable document from the specified URLs");
    Console.WriteLine("  /print-boxes          Print the box tree from the specified URLs");
    Console.WriteLine("  /print-dom            Print the HTML/CSS tree from the specified URLs");
    Console.WriteLine("  /print-nodes          Print the HTML/CSS nodes from the specified URLs");
    Console.WriteLine("  /print-tokens         Print the HTML/CSS tokens from the specified URLs");
    Console.WriteLine();
    Console.WriteLine("Aliases for Web Platform Tests:");
    Console.WriteLine("  /crashtest            --> /crash-test");
    Console.WriteLine("  /reftest              --> /load-document");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <URL>                 URL to load");
}
else
{
    if (options.ContainsKey("verbose-cpu"))
    {
        var process = Process.GetCurrentProcess();
        Console.WriteLine("Total CPU time: {0:N0} ms", process.TotalProcessorTime.TotalMilliseconds);
    }
}

async Task Benchmark(string url)
{
    var network = new NetworkManager();
    var stream = new MemoryStream();
    var uri = new Uri(url);
    var response = await network.Get(uri);
    response.Content.ReadAsStream().CopyTo(stream);
    var mediaType = response.Content.Headers.ContentType?.MediaType;
    var benchmarkCount = 100;
    var timeTokensStart = DateTime.Now;
    for (var i = 0; i < benchmarkCount; i++)
    {
        stream.Seek(0, SeekOrigin.Begin);
        switch (mediaType)
        {
            case "text/css":
                var cssTokens = new CssTokeniser(stream);
                foreach (var token in cssTokens.GetTokens()) ;
                break;
            default:
                var htmlTokens = new HtmlTokeniser(stream);
                foreach (var token in htmlTokens.GetTokens()) ;
                break;
        }
    }
    var timeNodesStart = DateTime.Now;
    for (var i = 0; i < benchmarkCount; i++)
    {
        stream.Seek(0, SeekOrigin.Begin);
        switch (mediaType)
        {
            case "text/css":
                var cssNodes = new CssParser(uri, stream);
                foreach (var node in cssNodes.GetNodes()) ;
                break;
            default:
                var htmlNodes = new HtmlParser(uri, stream);
                foreach (var node in htmlNodes.GetNodes()) ;
                break;
        }
    }
    var timeParseStart = DateTime.Now;
    for (var i = 0; i < benchmarkCount; i++)
    {
        stream.Seek(0, SeekOrigin.Begin);
        switch (mediaType)
        {
            case "text/css":
                var cssParser = new CssParser(uri, stream);
                cssParser.Parse();
                break;
            default:
                var htmlParser = new HtmlParser(uri, stream);
                htmlParser.Parse();
                break;
        }
    }
    var timeDone = DateTime.Now;
    Console.WriteLine("Tokens:  {0:F3} ms", (timeNodesStart - timeTokensStart).TotalMilliseconds / benchmarkCount);
    Console.WriteLine("Nodes:   {0:F3} ms", (timeParseStart - timeNodesStart).TotalMilliseconds / benchmarkCount);
    Console.WriteLine("Parse:   {0:F3} ms", (timeDone - timeParseStart).TotalMilliseconds / benchmarkCount);
}

async Task CrashTest(string url)
{
    var network = new NetworkManager();
    var uri = new Uri(url);
    var response = await network.Get(uri);
    var stream = response.Content.ReadAsStream();
    switch (response.Content.Headers.ContentType?.MediaType)
    {
        case "text/css":
            var cssParser = new CssParser(uri, stream);
            cssParser.Parse();
            break;
        default:
            var htmlParser = new HtmlParser(uri, stream);
            htmlParser.Parse();
            break;
    }
}

async Task LoadDocument(string url, Dictionary<string, string> options)
{
    var verbose = options.ContainsKey("verbose");
    var network = new NetworkManager();
    var navigable = new Navigable(network);
    if (verbose)
    {
        Console.WriteLine($"Date and time         Delta (ms)  Operation      Duration (ms)  Arguments");
        network.RequestLoading += (sender, e) => Log("Request begin", e.Uri.ToString());
        network.RequestLoaded += (sender, e) => Log("Request end", e.Uri.ToString(), e.DurationMS);
        navigable.DocumentLoading += (sender, e) => Log("Document begin", e.Uri.ToString());
        navigable.DocumentLoaded += (sender, e) => Log("Document end", e.Uri.ToString(), e.DurationMS);
        navigable.ResourceLoading += (sender, e) => Log("Resource begin", e.Uri?.ToString() ?? "(inline)");
        navigable.ResourceLoaded += (sender, e) => Log("Resource end", e.Uri?.ToString() ?? "(inline)", e.DurationMS);
    }

    await navigable.Navigate(new Uri(url));

    var layout = new BoxParser(navigable.ActiveDocument);
    if (options.TryGetValue("viewport", out string? viewport))
    {
        var parts = viewport.Split('x');
        layout.Viewport = new SKSizeI(int.Parse(parts[0]), int.Parse(parts[1]));
    }
    layout.Parse();
    if (verbose) Log("Layout complete");

    var renderer = new SkiaRenderer(layout.Root);
    var image = renderer.Render();
    if (verbose) Log("Render complete");

    if (options.TryGetValue("screenshot", out string? screenshot))
    {
        using var stream = new FileStream(screenshot, FileMode.Create);
        image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
        if (verbose) Log("Screenshot saved", screenshot);
    }
}

async Task PrintBoxes(string url)
{
    var network = new NetworkManager();
    var navigable = new Navigable(network);
    await navigable.Navigate(new Uri(url));
    var box = new BoxParser(navigable.ActiveDocument);
    box.Parse();
    PrintBoxNode(0, box.Root);
}

async Task PrintDom(string url)
{
    var network = new NetworkManager();
    var uri = new Uri(url);
    var response = await network.Get(uri);
    if (response.Content.Headers.ContentType?.MediaType == "text/css")
    {
        var cssParser = new CssParser(uri, response.Content.ReadAsStream());
        cssParser.Parse();
        PrintStyleNode(0, cssParser.Root);
    }
    else
    {
        var htmlParser = new HtmlParser(uri, response.Content.ReadAsStream());
        htmlParser.Parse();
        PrintMarkupNode(0, htmlParser.Root);
    }
}

async Task PrintNodes(string url)
{
    var network = new NetworkManager();
    var uri = new Uri(url);
    var response = await network.Get(uri);
    if (response.Content.Headers.ContentType?.MediaType == "text/css")
    {
        var cssNodes = new CssParser(uri, response.Content.ReadAsStream());
        foreach (var node in cssNodes.GetNodes()) Console.WriteLine(node);
    }
    else
    {
        var htmlNodes = new HtmlParser(uri, response.Content.ReadAsStream());
        foreach (var node in htmlNodes.GetNodes()) Console.WriteLine(node);
    }
}

async Task PrintTokens(string url)
{
    var network = new NetworkManager();
    var response = await network.Get(new Uri(url));
    if (response.Content.Headers.ContentType?.MediaType == "text/css")
    {
        var cssTokens = new CssTokeniser(response.Content.ReadAsStream());
        foreach (var token in cssTokens.GetTokens()) Console.WriteLine(token);
    }
    else
    {
        var htmlTokens = new HtmlTokeniser(response.Content.ReadAsStream());
        foreach (var token in htmlTokens.GetTokens()) Console.WriteLine(token);
    }
}

string GetMarkupIndent(int level) => level == 0 ? string.Empty : '|' + new string(' ', level * 2 - 1);

void PrintMarkupNode(int level, MarkupNode node)
{
    switch (node)
    {
        case MarkupDocument:
            Console.WriteLine("{0}{1}", GetMarkupIndent(level), node.Name);
            foreach (var child in node.Children) PrintMarkupNode(level + 1, child);
            break;
        case MarkupElement element:
            Console.WriteLine("{0}<{1}>", GetMarkupIndent(level), element.Name);
            foreach (var child in element.Attributes) Console.WriteLine("{0}{1}=\"{2}\"", GetMarkupIndent(level + 1), child.Key, child.Value);
            foreach (var child in node.Children) PrintMarkupNode(level + 1, child);
            break;
        case MarkupText:
            Console.WriteLine("{0}\"{1}\"", GetMarkupIndent(level), node.Value);
            foreach (var child in node.Children) PrintMarkupNode(level + 1, child);
            break;
        case MarkupComment:
            Console.WriteLine("{0}<!-- {1} -->", GetMarkupIndent(level), node.Value);
            foreach (var child in node.Children) PrintMarkupNode(level + 1, child);
            break;
    }
}

string GetStyleIndent(int level) => level == 0 ? string.Empty : new string(' ', level * 2);

void PrintStyleNode(int level, StyleNode node)
{
    switch (node)
    {
        case CssStyleSheet cssStyleSheet:
            Console.WriteLine("{0}{1}", GetStyleIndent(level), "#stylesheet");
            foreach (var child in cssStyleSheet.Statements) PrintStyleNode(level + 1, child);
            break;
        case CssAtRule cssAtRule:
            Console.Write("{0}@{1}", GetStyleIndent(level), cssAtRule.Name);
            foreach (var child in cssAtRule.Values) PrintStyleNode(level, child);
            if (cssAtRule.Statements == null)
            {
                Console.WriteLine(";");
            }
            else
            {
                Console.WriteLine(" {");
                foreach (var child in cssAtRule.Statements) PrintStyleNode(level + 1, child);
                Console.WriteLine("{0}{1}", GetStyleIndent(level), "}");
            }
            break;
        case CssRuleSet cssRuleSet:
            foreach (var child in cssRuleSet.Selectors) PrintStyleNode(level, child);
            Console.WriteLine("{0}{1}", GetStyleIndent(level), "{");
            foreach (var child in cssRuleSet.Statements ?? []) PrintStyleNode(level + 1, child);
            Console.WriteLine("{0}{1}", GetStyleIndent(level), "}");
            break;
        case CssDeclaration cssStyleProperty:
            Console.Write("{0}{1}:", GetStyleIndent(level), cssStyleProperty.Name);
            foreach (var child in cssStyleProperty.Values) PrintStyleNode(level + 1, child);
            if (cssStyleProperty.Important) Console.Write(" !important");
            Console.WriteLine(";");
            break;
        case CssSelector cssSelector:
            Console.Write("{0}", GetStyleIndent(level));
            foreach (var component in cssSelector.Components) PrintStyleNode(level, component);
            Console.WriteLine(",");
            break;
        case CssSelectorComponent cssSelectorAndCombinator:
            Console.Write("{0}", cssSelectorAndCombinator.Combinator switch
            {
                CssCombinator.Unset => "",
                CssCombinator.Descendant => " ",
                CssCombinator.Child => " > ",
                CssCombinator.NextSibling => " + ",
                CssCombinator.SubsequentSibling => " ~ ",
                _ => throw new NotImplementedException(),
            });
            foreach (var selector in cssSelectorAndCombinator.Selectors) PrintStyleNode(level, selector);
            break;
        case CssUniversalSelector cssUniversalSelector:
            Console.Write("*");
            break;
        case CssTypeSelector cssTypeSelector:
            Console.Write(cssTypeSelector.Type);
            break;
        case CssClassSelector cssClassSelector:
            Console.Write(".{0}", cssClassSelector.Class);
            break;
        case CssIDSelector cssIDSelector:
            Console.Write("#{0}", cssIDSelector.ID);
            break;
        case CssAttributeSelector cssAttributeSelector:
            Console.Write("[");
            foreach (var child in cssAttributeSelector.Values) PrintStyleNode(level + 1, child);
            Console.Write("]");
            break;
        case CssPseudoClassSelector cssPseudoClassSelector:
            Console.Write(":{0}", cssPseudoClassSelector.PseudoClass);
            if (cssPseudoClassSelector.Values.Count > 0)
            {
                Console.Write("(");
                foreach (var child in cssPseudoClassSelector.Values) PrintStyleNode(level + 1, child);
                Console.Write(")");
            }
            break;
        case CssPseudoElementSelector cssPseudoElementSelector:
            Console.Write("::{0}", cssPseudoElementSelector.PseudoElement);
            if (cssPseudoElementSelector.Values.Count > 0)
            {
                Console.Write("(");
                foreach (var child in cssPseudoElementSelector.Values) PrintStyleNode(level + 1, child);
                Console.Write(")");
            }
            break;
        case CssOperatorValue cssOperatorValue:
            Console.Write(" {0}", cssOperatorValue.Value);
            break;
        case CssKeywordValue cssKeywordValue:
            Console.Write(" {0}", cssKeywordValue.Value);
            break;
        case CssStringValue cssStringValue:
            Console.Write(" '{0}'", cssStringValue.Value);
            break;
        case CssUnitValue cssUnitValue:
            Console.Write(" {0}{1}", cssUnitValue.Value, cssUnitValue.Unit.ToString().Replace("Number", "").ToLowerInvariant());
            break;
        case CssColorValue cssColorValue:
            Console.Write(" rgba({0}, {1}, {2}, {3})", cssColorValue.R, cssColorValue.G, cssColorValue.B, cssColorValue.A);
            break;
        case CssFunctionValue cssFunctionValue:
            Console.Write(" {0}(", cssFunctionValue.Name);
            foreach (var child in cssFunctionValue.Values) PrintStyleNode(level + 1, child);
            Console.Write(")");
            break;
        default:
            throw new NotImplementedException($"Print not implemented for {node.GetType().Name}");
    }
}

string GetBoxIndent(int level) => level == 0 ? string.Empty : new string(' ', level * 2);

void PrintBoxNode(int level, BoxNode node)
{
    Console.WriteLine("{0}{1} {2}", GetBoxIndent(level), node.Rectangle, node.Node.Name);
    foreach (var child in node.Children) PrintBoxNode(level + 1, child);
}
