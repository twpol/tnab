using System.Diagnostics;
using System.Text;
using TNAB.Streams;

namespace TNAB.Parsers;

public class CssTokeniser(StreamReaderWithPeekBuffer reader)
{
    public enum TokenType
    {
        None,
        StatementAtRule,
        StatementRuleSet,
        StatementDeclaration,
        StatementEnd,
        SelectorUniversal,
        SelectorType,
        SelectorClass,
        SelectorID,
        SelectorAttribute,
        SelectorPseudoClass,
        SelectorPseudoElement,
        SelectorFunctionStart,
        SelectorFunctionEnd,
        SelectorCombinator,
        SelectorList,
        BlockStart,
        BlockEnd,
        Value,
        ValueOperator,
        ValueString,
        ValueNumber,
        ValueDimension,
        ValueColor,
        ValueFunctionStart,
        ValueFunctionEnd,
    }

    public record struct Token(TokenType Type, string Value);

    readonly StreamReaderWithPeekBuffer Reader = reader;

    public CssTokeniser(Stream stream) : this(new StreamReaderWithPeekBuffer(stream))
    {
    }

    enum State
    {
        Scope,
        RuleSetOrDeclaration,
        RuleSet,
        Value,
    }

    static readonly Dictionary<char, char> BracketOpenToClose = new()
    {
        { '(', ')' },
        { '[', ']' },
        { '{', '}' },
    };

    public IEnumerable<Token> GetTokens()
    {
        var stack = new Stack<StateStack>();
        stack.Push(new(State.Scope, "", new(TokenType.None, "")));
        var buffer = new StringBuilder();
        var index = 0;
        while (!Reader.EndOfStream)
        {
            if (++index > 1000000) throw new InvalidOperationException("Exceeded maximum token steps in CssTokeniser");
            // Console.WriteLine();
            // Console.WriteLine($"T {string.Join("", Reader.Peek(22).Replace("\r", " ").Replace("\n", " ").ToCharArray().Take(22)),-48}>>{string.Join(" ", stack.Select(s => $"{s.State}{s.End}"))}");
            if (stack.Count == 0) yield break;
            switch (stack.Peek().State)
            {
                case State.Scope:
                    ConsumeWhitespaceAndComments();
                    if (Reader.Read("<!--"))
                    {
                        yield return new(TokenType.Value, "<!--");
                    }
                    else if (Reader.Read("-->"))
                    {
                        yield return new(TokenType.Value, "-->");
                    }
                    else if (Reader.Read("@"))
                    {
                        ReadIdent(buffer);
                        yield return new(TokenType.StatementAtRule, buffer.ToString());
                        buffer.Clear();
                        stack.Push(new(State.Value, "", new(TokenType.StatementEnd, "")));
                    }
                    else if (Reader.Read(";"))
                    {
                        foreach (var token in EndScope(stack)) yield return token;
                    }
                    else if (Reader.Peek(")") || Reader.Peek("]") || Reader.Peek("}"))
                    {
                        foreach (var token in EndBlock(stack, Reader.Read())) yield return token;
                    }
                    else
                    {
                        stack.Push(new(State.RuleSetOrDeclaration, "", new(TokenType.None, "")));
                    }
                    break;
                case State.RuleSetOrDeclaration:
                    stack.Pop();
                    if (ReadIdent(buffer))
                    {
                        ConsumeWhitespaceAndComments();
                        if (Reader.Peek(": ") || Reader.Peek(":\t") || Reader.Peek(":\r") || Reader.Peek(":\n") || Reader.Peek(":\f"))
                        {
                            yield return new(TokenType.StatementDeclaration, buffer.ToString());
                            buffer.Clear();
                            Reader.Read(":");
                            stack.Push(new(State.Value, "", new(TokenType.StatementEnd, "")));
                        }
                        else
                        {
                            yield return new(TokenType.StatementRuleSet, "");
                            yield return new(TokenType.SelectorType, buffer.ToString().ToLowerInvariant());
                            buffer.Clear();
                            stack.Push(new(State.RuleSet, "", new(TokenType.StatementEnd, "")));
                        }
                    }
                    else
                    {
                        yield return new(TokenType.StatementRuleSet, "");
                        stack.Push(new(State.RuleSet, "", new(TokenType.StatementEnd, "")));
                    }
                    break;
                case State.RuleSet:
                    if (Reader.Read("*"))
                    {
                        yield return new(TokenType.SelectorUniversal, "");
                        break;
                    }
                    else if (Reader.Read("["))
                    {
                        yield return new(TokenType.SelectorAttribute, "");
                        yield return new(TokenType.SelectorFunctionStart, "");
                        stack.Push(new(State.Value, "]", new(TokenType.SelectorFunctionEnd, "")));
                        break;
                    }
                    else if (Reader.Read("."))
                    {
                        ReadIdent(buffer);
                        yield return new(TokenType.SelectorClass, buffer.ToString());
                        buffer.Clear();
                        break;
                    }
                    else if (Reader.Read("#"))
                    {
                        ReadIdent(buffer);
                        yield return new(TokenType.SelectorID, buffer.ToString());
                        buffer.Clear();
                        break;
                    }
                    else if (Reader.Read("::"))
                    {
                        ReadIdent(buffer);
                        yield return new(TokenType.SelectorPseudoElement, buffer.ToString());
                        buffer.Clear();
                        if (Reader.Read("("))
                        {
                            yield return new(TokenType.SelectorFunctionStart, "");
                            stack.Push(new(State.Value, ")", new(TokenType.SelectorFunctionEnd, "")));
                        }
                        break;
                    }
                    else if (Reader.Read(":"))
                    {
                        ReadIdent(buffer);
                        yield return new(TokenType.SelectorPseudoClass, buffer.ToString());
                        buffer.Clear();
                        if (Reader.Read("("))
                        {
                            yield return new(TokenType.SelectorFunctionStart, "");
                            stack.Push(new(State.Value, ")", new(TokenType.SelectorFunctionEnd, "")));
                        }
                        break;
                    }
                    var pos = Reader.Position;
                    ConsumeWhitespaceAndComments();
                    var foundWhitespace = Reader.Position > pos;
                    if (Reader.Read(";"))
                    {
                        foreach (var token in EndScope(stack)) yield return token;
                    }
                    else if (Reader.Peek("(") || Reader.Peek("{"))
                    {
                        foreach (var token in StartBlock(stack, Reader.Read())) yield return token;
                    }
                    else if (Reader.Peek(")") || Reader.Peek("]") || Reader.Peek("}"))
                    {
                        foreach (var token in EndBlock(stack, Reader.Read())) yield return token;
                    }
                    else if (Reader.Peek("\"") || Reader.Peek("'"))
                    {
                        yield return ReadStringToken(buffer) with { Type = TokenType.None };
                    }
                    else if (Reader.Read(">"))
                    {
                        yield return new(TokenType.SelectorCombinator, ">");
                        ConsumeWhitespaceAndComments();
                    }
                    else if (Reader.Read("+"))
                    {
                        yield return new(TokenType.SelectorCombinator, "+");
                        ConsumeWhitespaceAndComments();
                    }
                    else if (Reader.Read("~"))
                    {
                        yield return new(TokenType.SelectorCombinator, "~");
                        ConsumeWhitespaceAndComments();
                    }
                    else if (Reader.Read(","))
                    {
                        yield return new(TokenType.SelectorList, "");
                        ConsumeWhitespaceAndComments();
                    }
                    else
                    {
                        if (ReadIdent(buffer))
                        {
                            if (foundWhitespace) yield return new(TokenType.SelectorCombinator, " ");
                            yield return new(TokenType.SelectorType, buffer.ToString().ToLowerInvariant());
                            buffer.Clear();
                        }
                        else
                        {
                            Reader.ReadUntil(buffer, ' ', '\t', '\r', '\n', '\f', ';', '(', ')', '[', ']', '{', '}', '"', '\'', '*', '[', '.', '#', ':', '>', '+', '~', ',');
                            if (buffer.Length > 0) yield return new(TokenType.None, buffer.ToString());
                            buffer.Clear();
                        }
                    }
                    break;
                case State.Value:
                    ConsumeWhitespaceAndComments();
                    if (Reader.Read(";"))
                    {
                        foreach (var token in EndScope(stack)) yield return token;
                    }
                    else if (Reader.Peek("(") || Reader.Peek("[") || Reader.Peek("{"))
                    {
                        foreach (var token in StartBlock(stack, Reader.Read())) yield return token;
                    }
                    else if (Reader.Peek(")") || Reader.Peek("]") || Reader.Peek("}"))
                    {
                        foreach (var token in EndBlock(stack, Reader.Read())) yield return token;
                    }
                    else if (Reader.Peek("\"") || Reader.Peek("'"))
                    {
                        yield return ReadStringToken(buffer);
                    }
                    else if (Reader.Read("!important"))
                    {
                        yield return new(TokenType.ValueOperator, "!important");
                    }
                    else if (Reader.Read("!"))
                    {
                        yield return new(TokenType.ValueOperator, "!");
                    }
                    else if (ReadNumber(buffer))
                    {
                        var numberLen = buffer.Length;
                        Reader.ReadWhile(buffer, 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z');
                        yield return new(buffer.Length == numberLen ? TokenType.ValueNumber : TokenType.ValueDimension, buffer.ToString());
                        buffer.Clear();
                    }
                    else if (Reader.Read("#"))
                    {
                        Reader.ReadWhile(buffer, '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', 'A', 'B', 'C', 'D', 'E', 'F');
                        yield return new(TokenType.ValueColor, buffer.ToString());
                        buffer.Clear();
                    }
                    else if (Reader.Read("/"))
                    {
                        yield return new(TokenType.ValueOperator, "/");
                    }
                    else if (Reader.Read(","))
                    {
                        yield return new(TokenType.ValueOperator, ",");
                    }
                    else
                    {
                        if (ReadIdent(buffer))
                        {
                            if (Reader.Read("("))
                            {
                                yield return new(TokenType.ValueFunctionStart, buffer.ToString());
                                stack.Push(new(State.Value, ")", new(TokenType.ValueFunctionEnd, "")));
                            }
                            else
                            {
                                yield return new(TokenType.Value, buffer.ToString());
                            }
                        }
                        else
                        {
                            Reader.ReadUntil(buffer, ' ', '\t', '\r', '\n', '\f', ';', '(', ')', '[', ']', '{', '}', '"', '\'', '!', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '#', '/', ',');
                            if (buffer.Length > 0) yield return new(TokenType.Value, buffer.ToString());
                        }
                        buffer.Clear();
                    }
                    break;
            }
        }
    }

    record struct StateStack(State State, string End, Token Token);

    void ConsumeWhitespaceAndComments()
    {
        while (!Reader.EndOfStream)
        {
            Reader.ReadWhile(null, ' ', '\t', '\r', '\n', '\f');
            if (!Reader.Read("/*")) break;
            while (!Reader.EndOfStream)
            {
                Reader.ReadUntil(null, '*');
                if (Reader.Read("*/")) break;
                Reader.Read();
            }
        }
    }

    static IEnumerable<Token> EndScope(Stack<StateStack> stack)
    {
        while (stack.Peek().State != State.Scope)
        {
            var item = stack.Pop();
            if (item.Token.Type != TokenType.None) yield return item.Token;
        }
    }

    static IEnumerable<Token> StartBlock(Stack<StateStack> stack, char ch)
    {
        if (stack.Peek().End.Length == 0 && ch == '{')
        {
            yield return new(TokenType.BlockStart, ch.ToString());
            stack.Push(new(State.Scope, BracketOpenToClose[ch].ToString(), new(TokenType.BlockEnd, BracketOpenToClose[ch].ToString())));
        }
        else
        {
            var type = stack.Peek().State == State.Value ? TokenType.ValueOperator : TokenType.None;
            yield return new(type, ch.ToString());
            stack.Push(new(stack.Peek().State, BracketOpenToClose[ch].ToString(), new(type, BracketOpenToClose[ch].ToString())));
        }
    }

    static IEnumerable<Token> EndBlock(Stack<StateStack> stack, char ch)
    {
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            if (item.Token.Type != TokenType.None) yield return item.Token;
            if (stack.Count == 0) yield break;
            if (item.End == ch.ToString()) break;
        }
        if (ch == '}')
        {
            foreach (var token in EndScope(stack)) yield return token;
        }
    }

    Token ReadStringToken(StringBuilder buffer)
    {
        var ch = Reader.Read();
        Reader.ReadUntil(buffer, ch);
        if (!Reader.EndOfStream) Reader.Read();
        var str = buffer.ToString();
        buffer.Clear();
        return new(TokenType.ValueString, str);
    }

    // https://developer.mozilla.org/en-US/docs/Web/CSS/ident#syntax
    static readonly char[] IdentifierChar = [
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        '-',
        '_',
    ];

    bool ReadIdent(StringBuilder buffer)
    {
        Debug.Assert(buffer.Length == 0);
        while (Reader.Peek() >= 0xA0 || IdentifierChar.Contains(Reader.Peek())) buffer.Append(Reader.Read());
        return buffer.Length > 0;
    }

    // https://developer.mozilla.org/en-US/docs/Web/CSS/integer#syntax
    // https://developer.mozilla.org/en-US/docs/Web/CSS/number#syntax
    static readonly char[] NumberChar = [
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
    ];

    bool ReadNumber(StringBuilder buffer)
    {
        Debug.Assert(buffer.Length == 0);
        Reader.Read("-");
        while (NumberChar.Contains(Reader.Peek())) buffer.Append(Reader.Read());
        Reader.Read(".");
        while (NumberChar.Contains(Reader.Peek())) buffer.Append(Reader.Read());
        return buffer.Length > 0;
    }
}
