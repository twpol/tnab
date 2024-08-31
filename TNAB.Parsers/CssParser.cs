using System.Globalization;
using System.Text.RegularExpressions;
using TNAB.Streams;

namespace TNAB.Parsers;

public partial class CssParser
{
    public CssStyleSheet Root;

    readonly Uri BaseUri;
    readonly StreamReaderWithPeekBuffer Reader;
    readonly CssTokeniser Tokeniser;

    public CssParser(Uri baseUri, Stream stream)
    {
        BaseUri = baseUri;
        Reader = new StreamReaderWithPeekBuffer(stream);
        Tokeniser = new CssTokeniser(Reader);
        Root = new CssStyleSheet(BaseUri, []);
    }

    public IEnumerable<StyleNode> GetNodes()
    {
        var stack = new Stack<StyleNode>();
        stack.Push(Root);
        foreach (var token in Tokeniser.GetTokens())
        {
            var cssStyleSheet = stack.Peek() as CssStyleSheet;
            var cssGroupingStatement = stack.Peek() as CssGroupingStatement;
            var propStatements = cssGroupingStatement?.Statements ?? cssStyleSheet?.Statements;
            var cssRuleSet = stack.Peek() as CssRuleSet;
            var cssDeclaration = stack.Peek() as CssDeclaration;
            var cssAtRule = stack.Peek() as CssAtRule;
            var cssSimpleSelector = stack.Peek() as CssSimpleSelector;
            var cssFunctionValue = stack.Peek() as CssFunctionValue;
            var propValues = cssFunctionValue?.Values ?? cssSimpleSelector?.Values ?? cssAtRule?.Values ?? cssDeclaration?.Values;
            switch (token.Type)
            {
                case CssTokeniser.TokenType.StatementAtRule:
                    if (propStatements == null) throw new InvalidDataException($"Top of stack expected to have statements; got {stack.Peek().GetType().Name}");
                    var atRule = new CssAtRule(token.Value, []);
                    propStatements.Add(atRule);
                    stack.Push(atRule);
                    break;
                case CssTokeniser.TokenType.StatementRuleSet:
                    if (propStatements == null) throw new InvalidDataException($"Top of stack expected to have statements; got {stack.Peek().GetType().Name}");
                    var rule = new CssRuleSet([]);
                    rule.Selectors.Add(new([new(CssCombinator.Unset, [])]));
                    propStatements.Add(rule);
                    stack.Push(rule);
                    break;
                case CssTokeniser.TokenType.StatementDeclaration:
                    if (propStatements == null) throw new InvalidDataException($"Top of stack expected to have statements; got {stack.Peek().GetType().Name}");
                    var property = new CssDeclaration(token.Value, [], false);
                    propStatements.Add(property);
                    stack.Push(property);
                    yield return property;
                    break;
                case CssTokeniser.TokenType.StatementEnd:
                    stack.Pop();
                    break;
                case CssTokeniser.TokenType.SelectorUniversal:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssUniversalSelector());
                    break;
                case CssTokeniser.TokenType.SelectorType:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssTypeSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorClass:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssClassSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorID:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssIDSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorAttribute:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssAttributeSelector());
                    break;
                case CssTokeniser.TokenType.SelectorPseudoClass:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssPseudoClassSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorPseudoElement:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssPseudoElementSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorFunctionStart:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    stack.Push(cssRuleSet.Selectors[^1].Components[^1].Selectors[^1]);
                    break;
                case CssTokeniser.TokenType.SelectorFunctionEnd:
                    if (cssSimpleSelector == null) throw new InvalidDataException($"Top of stack expected CssSimpleSelector; got {stack.Peek().GetType().Name}");
                    stack.Pop();
                    break;
                case CssTokeniser.TokenType.SelectorCombinator:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    var combinator = token.Value switch
                    {
                        " " => CssCombinator.Descendant,
                        ">" => CssCombinator.Child,
                        "+" => CssCombinator.NextSibling,
                        "~" => CssCombinator.SubsequentSibling,
                        _ => throw new InvalidDataException($"Unknown combinator {token.Value}")
                    };
                    cssRuleSet.Selectors[^1].Components.Add(new(combinator, []));
                    break;
                case CssTokeniser.TokenType.SelectorList:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    cssRuleSet.Selectors.Add(new([new(CssCombinator.Unset, [])]));
                    break;
                case CssTokeniser.TokenType.BlockStart:
                    if (cssGroupingStatement == null) throw new InvalidDataException($"Top of stack expected CssGroupingStatement; got {stack.Peek().GetType().Name}");
                    yield return cssGroupingStatement;
                    break;
                case CssTokeniser.TokenType.BlockEnd:
                    if (cssGroupingStatement == null) throw new InvalidDataException($"Top of stack expected CssGroupingStatement; got {stack.Peek().GetType().Name}");
                    break;
                case CssTokeniser.TokenType.Value:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    propValues.Add(new CssKeywordValue(token.Value));
                    yield return propValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueOperator:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    propValues.Add(new CssOperatorValue(token.Value));
                    yield return propValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueString:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    propValues.Add(new CssStringValue(token.Value));
                    yield return propValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueNumber:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    propValues.Add(new CssUnitValue(double.Parse(token.Value), CssUnit.Number));
                    yield return propValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueDimension:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    var number = NumberRegex().Match(token.Value).Value;
                    if (Enum.TryParse(typeof(CssUnit), token.Value[number.Length..], true, out var unit))
                    {
                        propValues.Add(new CssUnitValue(double.Parse(number), (CssUnit)unit));
                        yield return propValues[^1];
                    }
                    break;
                case CssTokeniser.TokenType.ValueColor:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    if (token.Value.Length == 3 || token.Value.Length == 4 || token.Value.Length == 6 || token.Value.Length == 8)
                    {
                        var r = token.Value.Length <= 4 ? token.Value[0..1] + token.Value[0..1] : token.Value[0..2];
                        var g = token.Value.Length <= 4 ? token.Value[1..2] + token.Value[1..2] : token.Value[2..4];
                        var b = token.Value.Length <= 4 ? token.Value[2..3] + token.Value[2..3] : token.Value[4..6];
                        var a = token.Value.Length == 4 ? token.Value[3..4] + token.Value[3..4] : token.Value.Length == 8 ? token.Value[6..8] : "FF";
                        propValues.Add(new CssColorValue(byte.Parse(r, NumberStyles.HexNumber), byte.Parse(g, NumberStyles.HexNumber), byte.Parse(b, NumberStyles.HexNumber), byte.Parse(a, NumberStyles.HexNumber)));
                        yield return propValues[^1];
                    }
                    break;
                case CssTokeniser.TokenType.ValueFunctionStart:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    var function = new CssFunctionValue(token.Value, []);
                    propValues.Add(function);
                    stack.Push(function);
                    yield return function;
                    break;
                case CssTokeniser.TokenType.ValueFunctionEnd:
                    if (cssFunctionValue == null) throw new InvalidDataException($"Top of stack expected CssFunctionValue; got {stack.Peek().GetType().Name}");
                    stack.Pop();
                    break;
                case CssTokeniser.TokenType.None:
                    break;
            }
        }
    }

    public void Parse()
    {
        foreach (var _ in GetNodes()) ;
    }

    [GeneratedRegex("[0-9.]+")]
    private static partial Regex NumberRegex();
}
