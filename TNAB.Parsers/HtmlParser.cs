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

    readonly StreamReaderWithPeekBuffer Reader;
    readonly HtmlTokeniser Tokeniser;

    public HtmlParser(Stream stream)
    {
        Reader = new StreamReaderWithPeekBuffer(stream);
        Tokeniser = new HtmlTokeniser(Reader);
        Root = new Document();
    }

    public IEnumerable<Node> GetNodes()
    {
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
                    yield return stack.Peek();
                    if (VoidElements.Contains(stack.Peek().NodeName)) stack.Pop();
                    break;
                case HtmlTokeniser.TokenType.TagClose:
                    var ending = stack.FirstOrDefault(s => s.NodeName == token.Value);
                    if (ending != null)
                    {
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
}
