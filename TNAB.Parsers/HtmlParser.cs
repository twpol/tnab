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

    public MarkupDocument Root;

    readonly Uri BaseUri;
    readonly StreamReaderWithPeekBuffer Reader;
    readonly HtmlTokeniser Tokeniser;

    public HtmlParser(Uri baseUri, Stream stream)
    {
        BaseUri = baseUri;
        Reader = new StreamReaderWithPeekBuffer(stream);
        Tokeniser = new HtmlTokeniser(Reader);
        Root = new MarkupDocument(BaseUri);
    }

    public IEnumerable<MarkupNode> GetNodes()
    {
        var attributeName = "";
        var stack = new Stack<MarkupNode>();
        stack.Push(Root);
        foreach (var token in Tokeniser.GetTokens())
        {
            switch (token.Type)
            {
                case HtmlTokeniser.TokenType.DocType:
                    break;
                case HtmlTokeniser.TokenType.TagOpen:
                    var element = new MarkupElement(token.Value, []);
                    stack.Peek().Children.Add(element);
                    stack.Push(element);
                    break;
                case HtmlTokeniser.TokenType.TagOpenAttributeName:
                    attributeName = token.Value;
                    break;
                case HtmlTokeniser.TokenType.TagOpenAttributeValue:
                    if (stack.Peek() is MarkupElement el && attributeName.Length > 0) el.Attributes[attributeName] = token.Value;
                    attributeName = "";
                    break;
                case HtmlTokeniser.TokenType.TagOpenEnd:
                    var openEndElement = stack.Peek() as MarkupElement;
                    if (openEndElement != null && openEndElement.Name == "link" && openEndElement.Attributes.TryGetValue("rel", out string? rel) && rel == "stylesheet" && openEndElement.Attributes.TryGetValue("href", out string? href)) OnStyleSheet(new(openEndElement, new(BaseUri, href)));
                    yield return stack.Peek();
                    if (VoidElements.Contains(stack.Peek().Name)) stack.Pop();
                    break;
                case HtmlTokeniser.TokenType.TagClose:
                    var ending = stack.FirstOrDefault(s => s.Name == token.Value);
                    if (ending != null)
                    {
                        if (ending is MarkupElement endingElement && ending.Name == "style") OnStyleSheet(new(endingElement, null));
                        while (stack.Peek() != ending) stack.Pop();
                        stack.Pop();
                    }
                    break;
                case HtmlTokeniser.TokenType.Comment:
                    var comment = new MarkupComment(token.Value);
                    stack.Peek().Children.Add(comment);
                    yield return comment;
                    break;
                case HtmlTokeniser.TokenType.Character:
                    var text = new MarkupText(token.Value);
                    stack.Peek().Children.Add(text);
                    yield return text;
                    break;
            }
        }
    }

    public void Parse()
    {
        foreach (var _ in GetNodes()) ;
    }

    public record StyleSheetEventArgs(MarkupNode Node, Uri? Uri);
    public event EventHandler<StyleSheetEventArgs>? StyleSheet;
    protected virtual void OnStyleSheet(StyleSheetEventArgs e) => StyleSheet?.Invoke(this, e);
}
