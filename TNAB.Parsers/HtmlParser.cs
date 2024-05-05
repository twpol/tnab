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

    public MarkupDocument Root { get; private set; }

    readonly StreamReaderWithPeekBuffer Reader;
    readonly HtmlTokeniser Tokeniser;

    public HtmlParser(Stream stream)
    {
        Reader = new StreamReaderWithPeekBuffer(stream);
        Tokeniser = new HtmlTokeniser(Reader);
        Root = new MarkupDocument(null);
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
                case HtmlTokeniser.TokenType.Data:
                    var text = new MarkupText(token.Value);
                    stack.Peek().Children.Add(text);
                    yield return text;
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
                    yield return stack.Peek();
                    if (VoidElements.Contains(stack.Peek().Name)) stack.Pop();
                    break;
                case HtmlTokeniser.TokenType.TagClose:
                    var ending = stack.FirstOrDefault(s => s.Name == token.Value);
                    if (ending != null)
                    {
                        while (stack.Peek() != ending) stack.Pop();
                        stack.Pop();
                    }
                    break;
                case HtmlTokeniser.TokenType.Comment:
                    var comment = new MarkupComment(token.Value);
                    stack.Peek().Children.Add(comment);
                    yield return comment;
                    break;
                case HtmlTokeniser.TokenType.DocType:
                    Root = Root with { DocType = token.Value };
                    break;
            }
        }
    }

    public void Parse()
    {
        foreach (var _ in GetNodes()) ;
    }
}
