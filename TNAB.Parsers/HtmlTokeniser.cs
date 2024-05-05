using System.Text;
using TNAB.Streams;

namespace TNAB.Parsers;

public class HtmlTokeniser(StreamReaderWithPeekBuffer reader)
{
    public enum TokenType
    {
        Data,
        TagOpen,
        TagOpenAttributeName,
        TagOpenAttributeValue,
        TagOpenEnd,
        TagClose,
        Comment,
        DocType,
    }

    public record struct Token(TokenType Type, string Value);

    readonly StreamReaderWithPeekBuffer Reader = reader;

    public HtmlTokeniser(Stream stream) : this(new StreamReaderWithPeekBuffer(stream))
    {
    }

    enum State
    {
        Data,
        RCData,
        RawText,
        ScriptData,
        PlaintText,
        TagStart,
        TagAttributeName,
        TagAttributeValue,
        TagEnd,
        Comment,
        DocType,
        CData,
    }

    public IEnumerable<Token> GetTokens()
    {
        var state = State.Data;
        var lastTag = "";
        var buffer = new StringBuilder();
        while (!Reader.EndOfStream)
        {
            switch (state)
            {
                case State.Data:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#parsing-html-fragments
                    state = lastTag switch
                    {
                        "title" => State.RCData,
                        "textarea" => State.RCData,
                        "style" => State.RawText,
                        "xmp" => State.RawText,
                        "iframe" => State.RawText,
                        "noembed" => State.RawText,
                        "noframes" => State.RawText,
                        "script" => State.ScriptData,
                        "noscript" => State.Data,
                        "plaintext" => State.PlaintText,
                        _ => State.Data,
                    };
                    if (state != State.Data) break;
                    Reader.ReadUntil(buffer, '<');
                    if (buffer.Length > 0) yield return new(TokenType.Data, buffer.ToString());
                    buffer.Clear();
                    if (Reader.Read("<!--"))
                    {
                        state = State.Comment;
                    }
                    else if (Reader.Read("<![CDATA["))
                    {
                        state = State.CData;
                    }
                    else if (Reader.Read("<!"))
                    {
                        state = State.DocType;
                    }
                    else if (Reader.Read("</"))
                    {
                        state = State.TagEnd;
                    }
                    else if (Reader.Read("<"))
                    {
                        state = State.TagStart;
                    }
                    break;
                case State.RCData:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#rawtext-end-tag-name-state
                    if (Reader.Read($"</{lastTag}>", StringComparison.OrdinalIgnoreCase))
                    {
                        if (buffer.Length > 0) yield return new(TokenType.Data, buffer.ToString());
                        buffer.Clear();
                        yield return new(TokenType.TagClose, lastTag);
                        lastTag = "";
                        state = State.Data;
                    }
                    else if (Reader.Peek("<"))
                    {
                        buffer.Append(Reader.Read());
                    }
                    else
                    {
                        Reader.ReadUntil(buffer, '<');
                    }
                    break;
                case State.RawText:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#rawtext-end-tag-name-state
                    if (Reader.Read($"</{lastTag}>", StringComparison.OrdinalIgnoreCase))
                    {
                        if (buffer.Length > 0) yield return new(TokenType.Data, buffer.ToString());
                        buffer.Clear();
                        yield return new(TokenType.TagClose, lastTag);
                        lastTag = "";
                        state = State.Data;
                    }
                    else if (Reader.Peek("<"))
                    {
                        buffer.Append(Reader.Read());
                    }
                    else
                    {
                        Reader.ReadUntil(buffer, '<');
                    }
                    break;
                case State.ScriptData:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#script-data-end-tag-name-state
                    Reader.ReadUntil(buffer, '<');
                    if (Reader.Read($"</{lastTag}>", StringComparison.OrdinalIgnoreCase))
                    {
                        if (buffer.Length > 0) yield return new(TokenType.Data, buffer.ToString());
                        buffer.Clear();
                        yield return new(TokenType.TagClose, lastTag);
                        lastTag = "";
                        state = State.Data;
                    }
                    else if (Reader.Peek("<"))
                    {
                        buffer.Append(Reader.Read());
                    }
                    break;
                case State.PlaintText:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#plaintext-state
                    Reader.ReadUntil(buffer);
                    if (buffer.Length > 0) yield return new(TokenType.Data, buffer.ToString());
                    buffer.Clear();
                    break;
                case State.TagStart:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#tag-open-state
                    Reader.ReadWhile(buffer, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray());
                    if (buffer.Length == 0)
                    {
                        buffer.Append('<');
                        state = State.Data;
                        break;
                    }
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#tag-name-state
                    Reader.ReadUntil(buffer, '\t', '\r', '\n', '\f', ' ', '/', '>');
                    yield return new(TokenType.TagOpen, lastTag = buffer.ToString().ToLowerInvariant());
                    buffer.Clear();
                    if (Reader.Read(">"))
                    {
                        yield return new(TokenType.TagOpenEnd, "");
                        state = State.Data;
                    }
                    else if (Reader.Read("/>"))
                    {
                        yield return new(TokenType.TagOpenEnd, "");
                        yield return new(TokenType.TagClose, "");
                        state = State.Data;
                    }
                    else
                    {
                        Reader.Read("/");
                        state = State.TagAttributeName;
                    }
                    break;
                case State.TagAttributeName:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#before-attribute-name-state
                    Reader.ReadWhile(null, '\t', '\r', '\n', '\f', ' ');
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#attribute-name-state
                    Reader.ReadUntil(buffer, '\t', '\r', '\n', '\f', ' ', '/', '>', '=');
                    if (buffer.Length > 0) yield return new(TokenType.TagOpenAttributeName, buffer.ToString().ToLowerInvariant());
                    Reader.ReadWhile(null, '\t', '\r', '\n', '\f', ' ');
                    if (Reader.Peek("/>") || Reader.Peek(">"))
                    {
                        if (buffer.Length > 0) yield return new(TokenType.TagOpenAttributeValue, "");
                        yield return new(TokenType.TagOpenEnd, "");
                        if (Reader.Read("/>")) yield return new(TokenType.TagClose, "");
                        else Reader.Read(">");
                        state = State.Data;
                    }
                    else if (Reader.Read("="))
                    {
                        state = State.TagAttributeValue;
                    }
                    else
                    {
                        yield return new(TokenType.TagOpenAttributeValue, "");
                    }
                    buffer.Clear();
                    break;
                case State.TagAttributeValue:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#before-attribute-value-state
                    Reader.ReadWhile(null, '\t', '\r', '\n', '\f', ' ');
                    if (Reader.Peek("\"") || Reader.Peek("'"))
                    {
                        // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#attribute-value-(double-quoted)-state
                        // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#attribute-value-(single-quoted)-state
                        var ch = Reader.Read();
                        Reader.ReadUntil(buffer, ch);
                        Reader.Read();
                        yield return new(TokenType.TagOpenAttributeValue, buffer.ToString());
                        buffer.Clear();
                        state = State.TagAttributeName;
                    }
                    else if (Reader.Read(">"))
                    {
                        yield return new(TokenType.TagOpenAttributeValue, "");
                        yield return new(TokenType.TagOpenEnd, "");
                        state = State.Data;
                    }
                    else
                    {
                        // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#attribute-value-(unquoted)-state
                        Reader.ReadUntil(buffer, '\t', '\r', '\n', '\f', ' ', '>');
                        yield return new(TokenType.TagOpenAttributeValue, buffer.ToString());
                        buffer.Clear();
                        state = State.TagAttributeName;
                    }
                    break;
                case State.TagEnd:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#end-tag-open-state
                    Reader.ReadWhile(null, '\t', '\r', '\n', '\f', ' ');
                    Reader.ReadUntil(buffer, '\t', '\r', '\n', '\f', ' ', '/', '>');
                    Reader.ReadUntil(null, '/', '>');
                    yield return new(TokenType.TagClose, buffer.ToString().ToLowerInvariant());
                    lastTag = "";
                    buffer.Clear();
                    if (Reader.Read("/>") || Reader.Read(">"))
                    {
                        state = State.Data;
                    }
                    break;
                case State.Comment:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#comment-start-state
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#comment-state
                    Reader.ReadUntil(buffer, '-', '>');
                    if (Reader.Read("-->") || Reader.Read(">"))
                    {
                        yield return new(TokenType.Comment, buffer.ToString());
                        buffer.Clear();
                        state = State.Data;
                    }
                    else if (Reader.Peek("-"))
                    {
                        buffer.Append(Reader.Read());
                    }
                    break;
                case State.DocType:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#doctype-state
                    Reader.ReadUntil(buffer, '"', '\'', '>');
                    if (Reader.Read(">"))
                    {
                        yield return new(TokenType.DocType, buffer.ToString());
                        buffer.Clear();
                        state = State.Data;
                    }
                    else if (Reader.Peek("\"") || Reader.Peek("'"))
                    {
                        var ch = Reader.Read();
                        buffer.Append(ch);
                        Reader.ReadUntil(buffer, ch);
                        buffer.Append(Reader.Read());
                    }
                    break;
                case State.CData:
                    // Spec: https://html.spec.whatwg.org/commit-snapshots/aa0f4e89a9c34273ba2a4b70169cc50b247cb8da/#cdata-section-state
                    Reader.ReadUntil(buffer, ']');
                    if (Reader.Read("]]>"))
                    {
                        yield return new(TokenType.Data, buffer.ToString());
                        buffer.Clear();
                        state = State.Data;
                    }
                    else
                    {
                        buffer.Append(Reader.Read());
                    }
                    break;
            }
        }
        if (buffer.Length > 0) yield return new(TokenType.Data, buffer.ToString());
    }
}
