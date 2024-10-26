using System.Diagnostics;
using TNAB.Streams;

namespace TNAB.Parsers;

public class HtmlParser
{
    public string[] VoidElements =
    [
        // HTML4-only
        "basefont",
        // HTML5
        "area",
        "base",
        "br",
        "col",
        "embed",
        "hr",
        "img",
        "input",
        "link",
        "meta",
        "param",
        "source",
        "track",
        "wbr",
    ];

    public Document Root;

    readonly Uri BaseUri;
    readonly StreamReaderWithPeekBuffer Reader;
    readonly HtmlTokeniser Tokeniser;

    public HtmlParser(Uri baseUri, Stream stream)
    {
        BaseUri = baseUri;
        Reader = new StreamReaderWithPeekBuffer(stream);
        Tokeniser = new HtmlTokeniser(Reader);
        Root = new Document(BaseUri);
    }

    public IEnumerable<Node> GetNodes()
    {
        OnStyleSheet(new(Root, new("tnab-resource:///TNAB.Parsers/Agent.css")));
        var attributeName = "";
        var stack = new Stack<Node>();
        stack.Push(Root);
        foreach (var token in Tokeniser.GetTokens())
        {
            switch (token.Type)
            {
                case HtmlTokeniser.TokenType.DocType:
                    break;
                case HtmlTokeniser.TokenType.TagOpen:
                    var element = new Element(token.Value, []);
                    stack.Peek().Children.Add(element);
                    stack.Push(element);
                    break;
                case HtmlTokeniser.TokenType.TagOpenAttributeName:
                    attributeName = token.Value;
                    break;
                case HtmlTokeniser.TokenType.TagOpenAttributeValue:
                    if (stack.Peek() is Element el && attributeName.Length > 0) el.Attributes[attributeName] = token.Value;
                    attributeName = "";
                    break;
                case HtmlTokeniser.TokenType.TagOpenEnd:
                    var openEndElement = stack.Peek() as Element;
                    if (openEndElement != null && openEndElement.NodeName == "link" && openEndElement.Attributes.TryGetValue("rel", out string? rel) && rel == "stylesheet" && openEndElement.Attributes.TryGetValue("href", out string? href)) OnStyleSheet(new(openEndElement, new(BaseUri, href)));
                    yield return stack.Peek();
                    if (VoidElements.Contains(stack.Peek().NodeName)) stack.Pop();
                    break;
                case HtmlTokeniser.TokenType.TagClose:
                    var ending = stack.FirstOrDefault(s => s.NodeName == token.Value);
                    if (ending != null)
                    {
                        if (ending is Element endingElement && ending.NodeName == "style") OnStyleSheet(new(endingElement, null));
                        while (stack.Peek() != ending) stack.Pop();
                        stack.Pop();
                    }
                    break;
                case HtmlTokeniser.TokenType.Comment:
                    var comment = new Comment(token.Value);
                    stack.Peek().Children.Add(comment);
                    yield return comment;
                    break;
                case HtmlTokeniser.TokenType.Character:
                    var text = new Text(token.Value);
                    stack.Peek().Children.Add(text);
                    yield return text;
                    break;
            }
        }
        Debug.Assert(stack.Count == 1, $"Expected stack depth of 1; got {stack.Count}:\n{string.Join("\n", stack.Select(s => s.ToString()))}");
    }

    public void Parse()
    {
        foreach (var _ in GetNodes()) ;
    }

    public record StyleSheetEventArgs(Node Node, Uri? Uri);
    public event EventHandler<StyleSheetEventArgs>? StyleSheet;
    protected virtual void OnStyleSheet(StyleSheetEventArgs e) => StyleSheet?.Invoke(this, e);
}
