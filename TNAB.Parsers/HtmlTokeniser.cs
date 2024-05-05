using System.Text;
using TNAB.Streams;

namespace TNAB.Parsers;

public class HtmlTokeniser(StreamReaderWithPeekBuffer reader)
{
    public enum TokenType
    {
        DocType,
        TagOpen,
        TagOpenAttributeName,
        TagOpenAttributeValue,
        TagOpenEnd,
        TagClose,
        Comment,
        Character,
    }

    public record struct Token(TokenType Type, string Value);

    readonly StreamReaderWithPeekBuffer Reader = reader;

    public HtmlTokeniser(Stream stream) : this(new StreamReaderWithPeekBuffer(stream))
    {
    }

    enum State
    {
        DocType,
        TagStart,
        TagAttributeName,
        TagAttributeValue,
        TagEnd,
        Comment,
        Character,
        CharacterScript,
        CharacterStyle,
    }

    public IEnumerable<Token> GetTokens()
    {
        var state = State.Character;
        var lastTag = "";
        var buffer = new StringBuilder();
        while (!Reader.EndOfStream)
        {
            switch (state)
            {
                case State.DocType:
                    Reader.ReadUntil(buffer, '"', '\'', '>');
                    if (Reader.Read(">"))
                    {
                        yield return new(TokenType.DocType, buffer.ToString());
                        buffer.Clear();
                        state = State.Character;
                    }
                    else if (Reader.Peek("\"") || Reader.Peek("'"))
                    {
                        var ch = Reader.Read();
                        buffer.Append(ch);
                        Reader.ReadUntil(buffer, ch);
                        buffer.Append(Reader.Read());
                    }
                    break;
                case State.TagStart:
                    Reader.ReadWhile(null, ' ', '\t', '\v', '\r', '\n');
                    Reader.ReadUntil(buffer, ' ', '\t', '\v', '\r', '\n', '>');
                    Reader.ReadWhile(null, ' ', '\t', '\v', '\r', '\n');
                    yield return new(TokenType.TagOpen, lastTag = buffer.ToString().ToLowerInvariant());
                    buffer.Clear();
                    if (Reader.Read(">"))
                    {
                        yield return new(TokenType.TagOpenEnd, "");
                        state = State.Character;
                    }
                    else
                    {
                        state = State.TagAttributeName;
                    }
                    break;
                case State.TagAttributeName:
                    Reader.ReadWhile(null, ' ', '\t', '\v', '\r', '\n');
                    Reader.ReadUntil(buffer, ' ', '\t', '\v', '\r', '\n', '=', '>');
                    yield return new(TokenType.TagOpenAttributeName, buffer.ToString().ToLowerInvariant());
                    buffer.Clear();
                    Reader.ReadWhile(null, ' ', '\t', '\v', '\r', '\n');
                    if (Reader.Read(">"))
                    {
                        yield return new(TokenType.TagOpenAttributeValue, "");
                        yield return new(TokenType.TagOpenEnd, "");
                        state = State.Character;
                    }
                    else if (Reader.Read("="))
                    {
                        state = State.TagAttributeValue;
                    }
                    break;
                case State.TagAttributeValue:
                    Reader.ReadWhile(null, ' ', '\t', '\v', '\r', '\n');
                    if (Reader.Read(">"))
                    {
                        yield return new(TokenType.TagOpenAttributeValue, "");
                        yield return new(TokenType.TagOpenEnd, "");
                        state = State.Character;
                    }
                    else if (Reader.Peek("\"") || Reader.Peek("'"))
                    {
                        var ch = Reader.Read();
                        Reader.ReadUntil(buffer, ch);
                        Reader.Read();
                        yield return new(TokenType.TagOpenAttributeValue, buffer.ToString());
                        buffer.Clear();
                        state = State.TagAttributeName;
                    }
                    else
                    {
                        Reader.ReadUntil(buffer, ' ', '\t', '\v', '\r', '\n', '>');
                        yield return new(TokenType.TagOpenAttributeValue, buffer.ToString());
                        buffer.Clear();
                        state = State.TagAttributeName;
                    }
                    break;
                case State.TagEnd:
                    Reader.ReadWhile(null, ' ', '\t', '\v', '\r', '\n');
                    Reader.ReadUntil(buffer, ' ', '\t', '\v', '\r', '\n', '>');
                    Reader.ReadUntil(null, '>');
                    yield return new(TokenType.TagClose, buffer.ToString());
                    lastTag = "";
                    buffer.Clear();
                    if (Reader.Read(">"))
                    {
                        state = State.Character;
                    }
                    break;
                case State.Comment:
                    Reader.ReadUntil(buffer, '-');
                    if (Reader.Read("-->"))
                    {
                        yield return new(TokenType.Comment, buffer.ToString());
                        buffer.Clear();
                        state = State.Character;
                    }
                    else if (Reader.Peek("-"))
                    {
                        buffer.Append(Reader.Read());
                    }
                    break;
                case State.Character:
                    if (lastTag == "script")
                    {
                        state = State.CharacterScript;
                        break;
                    }
                    if (lastTag == "style")
                    {
                        state = State.CharacterStyle;
                        break;
                    }
                    Reader.ReadUntil(buffer, '<');
                    yield return new(TokenType.Character, buffer.ToString());
                    buffer.Clear();
                    if (Reader.Read("<!--"))
                    {
                        state = State.Comment;
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
                case State.CharacterScript:
                    if (Reader.Read("</script>", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return new(TokenType.Character, buffer.ToString());
                        buffer.Clear();
                        yield return new(TokenType.TagClose, "script");
                        lastTag = "";
                        state = State.Character;
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
                case State.CharacterStyle:
                    if (Reader.Read("</style>", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return new(TokenType.Character, buffer.ToString());
                        buffer.Clear();
                        yield return new(TokenType.TagClose, "style");
                        lastTag = "";
                        state = State.Character;
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
            }
        }
    }
}
