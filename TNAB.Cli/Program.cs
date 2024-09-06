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
        case "benchmark":
            await Benchmark(arg);
            break;
        case "crashtest":
            await CrashTest(arg);
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
if (action == "")
{
    Console.WriteLine("TNAB (The Not As Bad web browser) CLI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  TNAB.Cli [action <URL> [...]] [...]");
    Console.WriteLine();
    Console.WriteLine("Actions:");
    Console.WriteLine("  /benchmark            Benchmark the HTML/CSS parser with the specified URLs");
    Console.WriteLine("  /crashtest            Crash test the HTML/CSS parser with the specified URLs");
    Console.WriteLine("  /print-dom            Print the HTML/CSS tree from the specified URLs");
    Console.WriteLine("  /print-nodes          Print the HTML/CSS nodes from the specified URLs");
    Console.WriteLine("  /print-tokens         Print the HTML/CSS tokens from the specified URLs");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <URL>                 URL to load");
}

async Task Benchmark(string url)
{
    var stream = new MemoryStream();
    var response = await NetworkManager.Get(new Uri(url));
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
                var cssNodes = new CssParser(stream);
                foreach (var node in cssNodes.GetNodes()) ;
                break;
            default:
                var htmlNodes = new HtmlParser(stream);
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
                var cssParser = new CssParser(stream);
                cssParser.Parse();
                break;
            default:
                var htmlParser = new HtmlParser(stream);
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
    var response = await NetworkManager.Get(new Uri(url));
    var stream = response.Content.ReadAsStream();
    switch (response.Content.Headers.ContentType?.MediaType)
    {
        case "text/css":
            var cssParser = new CssParser(stream);
            cssParser.Parse();
            break;
        default:
            var htmlParser = new HtmlParser(stream);
            htmlParser.Parse();
            break;
    }
}

async Task PrintDom(string url)
{
    var response = await NetworkManager.Get(new Uri(url));
    if (response.Content.Headers.ContentType?.MediaType == "text/css")
    {
        var cssParser = new CssParser(response.Content.ReadAsStream());
        cssParser.Parse();
        PrintStyleNode(0, cssParser.Root);
    }
    else
    {
        var htmlParser = new HtmlParser(response.Content.ReadAsStream());
        htmlParser.Parse();
        PrintMarkupNode(0, htmlParser.Root);
    }
}

async Task PrintNodes(string url)
{
    var response = await NetworkManager.Get(new Uri(url));
    if (response.Content.Headers.ContentType?.MediaType == "text/css")
    {
        var cssNodes = new CssParser(response.Content.ReadAsStream());
        foreach (var node in cssNodes.GetNodes()) Console.WriteLine(node);
    }
    else
    {
        var htmlNodes = new HtmlParser(response.Content.ReadAsStream());
        foreach (var node in htmlNodes.GetNodes()) Console.WriteLine(node);
    }
}

async Task PrintTokens(string url)
{
    var response = await NetworkManager.Get(new Uri(url));
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

void PrintMarkupNode(int level, Node node)
{
    switch (node)
    {
        case Document:
            Console.WriteLine("{0}{1}", GetMarkupIndent(level), node.NodeName);
            foreach (var child in node.Children) PrintMarkupNode(level + 1, child);
            break;
        case Element element:
            Console.WriteLine("{0}<{1}>", GetMarkupIndent(level), element.NodeName);
            foreach (var child in element.Attributes) Console.WriteLine("{0}{1}=\"{2}\"", GetMarkupIndent(level + 1), child.Key, child.Value);
            foreach (var child in node.Children) PrintMarkupNode(level + 1, child);
            break;
        case Text:
            Console.WriteLine("{0}\"{1}\"", GetMarkupIndent(level), node.NodeValue);
            foreach (var child in node.Children) PrintMarkupNode(level + 1, child);
            break;
        case Comment:
            Console.WriteLine("{0}<!-- {1} -->", GetMarkupIndent(level), node.NodeValue);
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
            Console.WriteLine("{0}{1}", GetStyleIndent(level), "<stylesheet>");
            foreach (var child in cssStyleSheet.Rules) PrintStyleNode(level + 1, child);
            break;
        case CssStyleRule cssStyleRule:
            foreach (var child in cssStyleRule.Selectors) PrintStyleNode(level, child);
            Console.WriteLine("{0}{1}", GetStyleIndent(level), "{");
            foreach (var child in cssStyleRule.Rules) PrintStyleNode(level + 1, child);
            PrintStyleNode(level, cssStyleRule.Style);
            Console.WriteLine("{0}{1}", GetStyleIndent(level), "}");
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
            Console.Write(cssTypeSelector.Name);
            break;
        case CssAttributeSelector cssAttributeSelector:
            Console.Write("[");
            foreach (var child in cssAttributeSelector.Values) PrintStyleNode(level + 1, child);
            Console.Write("]");
            break;
        case CssClassSelector cssClassSelector:
            Console.Write(".{0}", cssClassSelector.Class);
            break;
        case CssIDSelector cssIDSelector:
            Console.Write("#{0}", cssIDSelector.ID);
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
        case CssStyleDeclaration cssStyleDeclaration:
            foreach (var child in cssStyleDeclaration.Properties) PrintStyleNode(level + 1, child);
            break;
        case CssStyleProperty cssStyleProperty:
            Console.Write("{0}{1}:", GetStyleIndent(level), cssStyleProperty.Name);
            foreach (var child in cssStyleProperty.Values) PrintStyleNode(level + 1, child);
            if (cssStyleProperty.Important) Console.Write(" !important");
            Console.WriteLine(";");
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
        case CssAtRule cssAtRule:
            Console.Write("{0}@{1}", GetStyleIndent(level), cssAtRule.Name);
            foreach (var child in cssAtRule.Condition) PrintStyleNode(level + 1, child);
            if (cssAtRule.Style.Properties.Count == 0 && cssAtRule.Rules.Count == 0)
            {
                Console.WriteLine(";");
            }
            else
            {
                Console.WriteLine(" {");
                foreach (var child in cssAtRule.Style.Properties) PrintStyleNode(level + 1, child);
                foreach (var child in cssAtRule.Rules) PrintStyleNode(level + 1, child);
                Console.WriteLine("{0}}}", GetStyleIndent(level));
            }
            break;
        default:
            throw new NotImplementedException($"Print not implemented for {node.GetType().Name}");
    }
}
