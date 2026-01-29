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

    public string[] GlobalAttributes =
    [
        // https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Global_attributes
        // FIXME: Update list to match 2024-04-01
        "accesskey",
        "anchor",
        "autocapitalize",
        "autocorrect",
        "autofocus",
        "class",
        "contenteditable",
        "data_attributes",
        "dir",
        "draggable",
        "enterkeyhint",
        "exportparts",
        "hidden",
        "id",
        "inert",
        "inputmode",
        "is",
        "lang",
        "nonce",
        "part",
        "popover",
        "slot",
        "spellcheck",
        "style",
        "tabindex",
        "title",
        "translate",
        "virtualkeyboardpolicy",
        "writingsuggestions",
    ];

    public MarkupDocument Root { get; private set; }

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
        OnStyleSheet(new(Root, new("tnab-resource:///TNAB.Parsers/Agent.css")));
        var attributeName = "";
        var stack = new Stack<MarkupNode>();
        stack.Push(Root);
        foreach (var token in Tokeniser.GetTokens())
        {
            switch (token.Type)
            {
                case HtmlTokeniser.TokenType.Data:
                    var text = new MarkupText(token.Value)
                    {
                        ParentNode = stack.Peek()
                    };
                    stack.Peek().Children.Add(text);
                    yield return text;
                    break;
                case HtmlTokeniser.TokenType.TagOpen:
                    if (!IsCustomElement(token.Value)) OnFeatureUsed(new($"html.elements.{token.Value}"));
                    var element = new MarkupElement(token.Value, [])
                    {
                        ParentNode = stack.Peek()
                    };
                    stack.Peek().Children.Add(element);
                    stack.Push(element);
                    break;
                case HtmlTokeniser.TokenType.TagOpenAttributeName:
                    if (!IsCustomElement(stack.Peek().Name)) OnFeatureUsed(new(token.Value.StartsWith("data-") ? "html.global_attributes.data_attributes" : token.Value == "role" ? "html.aria_attributes.role" : token.Value.StartsWith("aria-") ? $"html.aria_attributes.{token.Value[5..]}" : GlobalAttributes.Contains(token.Value) ? $"html.global_attributes.{token.Value}" : $"html.elements.{stack.Peek().Name}.{token.Value}"));
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
                    var comment = new MarkupComment(token.Value)
                    {
                        ParentNode = stack.Peek()
                    };
                    stack.Peek().Children.Add(comment);
                    yield return comment;
                    break;
                case HtmlTokeniser.TokenType.DocType:
                    var docTypeParts = token.Value.Split('\0').Select(s => s switch
                    {
                        "" => null,
                        _ => s,
                    }).ToArray();
                    Root.DocType = new(docTypeParts[0], docTypeParts[1], docTypeParts[2]);
                    break;
            }
        }
    }

    public void Parse()
    {
        foreach (var _ in GetNodes()) ;
    }

    static bool IsCustomElement(string name) => name.Equals(name, StringComparison.InvariantCultureIgnoreCase) && name.Contains('-');

    public record FeatureUsedEventArgs(string Feature);
    public event EventHandler<FeatureUsedEventArgs>? FeatureUsed;
    protected virtual void OnFeatureUsed(FeatureUsedEventArgs e) => FeatureUsed?.Invoke(this, e);

    public record StyleSheetEventArgs(MarkupNode Node, Uri? Uri);
    public event EventHandler<StyleSheetEventArgs>? StyleSheet;
    protected virtual void OnStyleSheet(StyleSheetEventArgs e) => StyleSheet?.Invoke(this, e);
}
