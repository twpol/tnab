using System.Globalization;
using System.Text.RegularExpressions;
using TNAB.Streams;

namespace TNAB.Parsers;

public partial class CssParser
{
    public CssStyleSheet Root;

    readonly StreamReaderWithPeekBuffer Reader;
    readonly CssTokeniser Tokeniser;

    public CssParser(Stream stream)
    {
        Reader = new StreamReaderWithPeekBuffer(stream);
        Tokeniser = new CssTokeniser(Reader);
        Root = new CssStyleSheet([]);
    }

    public IEnumerable<StyleNode> GetNodes()
    {
        var stack = new Stack<StyleNode>();
        stack.Push(Root);
        foreach (var token in Tokeniser.GetTokens())
        {
            // Console.WriteLine($"P {token.Type,-24}{token.Value,-24}>>{string.Join(" ", stack.Select(s => s.GetType().Name))}");
            var cssStyleSheet = stack.Peek() as CssStyleSheet;
            var cssGroupingRule = stack.Peek() as CssGroupingRule;
            var cssConditionRule = stack.Peek() as CssConditionRule;
            var cssRules = cssGroupingRule?.Rules ?? cssStyleSheet?.Rules;
            var cssStyleRule = stack.Peek() as CssStyleRule;
            var cssAtRule = stack.Peek() as CssAtRule;
            var cssStyleProperty = stack.Peek() as CssStyleProperty;
            var cssFunctionValue = stack.Peek() as CssFunctionValue;
            var cssSimpleSelector = stack.Peek() as CssSimpleSelector;
            var cssValues = cssFunctionValue?.Values ?? cssStyleProperty?.Values ?? cssConditionRule?.Condition ?? cssSimpleSelector?.Values;
            var cssStyle = cssStyleRule?.Style ?? cssAtRule?.Style;
            switch (token.Type)
            {
                case CssTokeniser.TokenType.StatementAtRule:
                    if (cssRules == null) throw new InvalidDataException($"Top of stack expected to have CssRules; got {stack.Peek().GetType().Name}");
                    var atRule = new CssAtRule(token.Value, new([]));
                    cssRules.Add(atRule);
                    stack.Push(atRule);
                    break;
                case CssTokeniser.TokenType.StatementRuleSet:
                    if (cssRules == null) throw new InvalidDataException($"Top of stack expected to have CssRules; got {stack.Peek().GetType().Name}");
                    var rule = new CssStyleRule([], new([]));
                    rule.Selectors.Add(new([new(CssCombinator.Unset, [])]));
                    cssRules.Add(rule);
                    stack.Push(rule);
                    break;
                case CssTokeniser.TokenType.StatementDeclaration:
                    if (cssStyle == null) throw new InvalidDataException($"Top of stack expected CssStyle; got {stack.Peek().GetType().Name}");
                    var property = new CssStyleProperty(token.Value, [], false);
                    cssStyle.Properties.Add(property);
                    stack.Push(property);
                    yield return property;
                    break;
                case CssTokeniser.TokenType.StatementEnd:
                    stack.Pop();
                    break;
                case CssTokeniser.TokenType.SelectorType:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    cssStyleRule.Selectors[^1].Components[^1].Selectors.Add(new CssTypeSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorUniversal:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    cssStyleRule.Selectors[^1].Components[^1].Selectors.Add(new CssUniversalSelector());
                    break;
                case CssTokeniser.TokenType.SelectorAttribute:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    cssStyleRule.Selectors[^1].Components[^1].Selectors.Add(new CssAttributeSelector());
                    break;
                case CssTokeniser.TokenType.SelectorClass:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    cssStyleRule.Selectors[^1].Components[^1].Selectors.Add(new CssClassSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorID:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    cssStyleRule.Selectors[^1].Components[^1].Selectors.Add(new CssIDSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorPseudoClass:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    cssStyleRule.Selectors[^1].Components[^1].Selectors.Add(new CssPseudoClassSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorPseudoElement:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    cssStyleRule.Selectors[^1].Components[^1].Selectors.Add(new CssPseudoElementSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorFunctionStart:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    stack.Push(cssStyleRule.Selectors[^1].Components[^1].Selectors[^1]);
                    break;
                case CssTokeniser.TokenType.SelectorFunctionEnd:
                    if (cssSimpleSelector == null) throw new InvalidDataException($"Top of stack expected CssSimpleSelector; got {stack.Peek().GetType().Name}");
                    stack.Pop();
                    break;
                case CssTokeniser.TokenType.SelectorCombinator:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    var combinator = token.Value switch
                    {
                        " " => CssCombinator.Descendant,
                        ">" => CssCombinator.Child,
                        "+" => CssCombinator.NextSibling,
                        "~" => CssCombinator.SubsequentSibling,
                        _ => throw new InvalidDataException($"Unknown combinator {token.Value}")
                    };
                    cssStyleRule.Selectors[^1].Components.Add(new(combinator, []));
                    break;
                case CssTokeniser.TokenType.SelectorList:
                    if (cssStyleRule == null) throw new InvalidDataException($"Top of stack expected CssStyleRule; got {stack.Peek().GetType().Name}");
                    cssStyleRule.Selectors.Add(new([new(CssCombinator.Unset, [])]));
                    break;
                case CssTokeniser.TokenType.BlockStart:
                    if (cssGroupingRule == null) throw new InvalidDataException($"Top of stack expected CssGroupingRule; got {stack.Peek().GetType().Name}");
                    yield return cssGroupingRule;
                    break;
                case CssTokeniser.TokenType.BlockEnd:
                    if (cssGroupingRule == null) throw new InvalidDataException($"Top of stack expected CssGroupingRule; got {stack.Peek().GetType().Name}");
                    break;
                case CssTokeniser.TokenType.Value:
                    if (cssValues == null) throw new InvalidDataException($"Top of stack expected to have CssValues; got {stack.Peek().GetType().Name}");
                    cssValues.Add(new CssKeywordValue(token.Value));
                    yield return cssValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueOperator:
                    if (cssValues == null) throw new InvalidDataException($"Top of stack expected to have CssValues; got {stack.Peek().GetType().Name}");
                    cssValues.Add(new CssOperatorValue(token.Value));
                    yield return cssValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueString:
                    if (cssValues == null) throw new InvalidDataException($"Top of stack expected to have CssValues; got {stack.Peek().GetType().Name}");
                    cssValues.Add(new CssStringValue(token.Value));
                    yield return cssValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueNumber:
                    if (cssValues == null) throw new InvalidDataException($"Top of stack expected to have CssValues; got {stack.Peek().GetType().Name}");
                    cssValues.Add(new CssUnitValue(double.Parse(token.Value), CssUnit.Number));
                    yield return cssValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueDimension:
                    if (cssValues == null) throw new InvalidDataException($"Top of stack expected to have CssValues; got {stack.Peek().GetType().Name}");
                    var number = NumberRegex().Match(token.Value).Value;
                    if (Enum.TryParse(typeof(CssUnit), token.Value[number.Length..], true, out var unit))
                    {
                        cssValues.Add(new CssUnitValue(double.Parse(number), (CssUnit)unit));
                        yield return cssValues[^1];
                    }
                    break;
                case CssTokeniser.TokenType.ValueColor:
                    if (cssValues == null) throw new InvalidDataException($"Top of stack expected to have CssValues; got {stack.Peek().GetType().Name}");
                    if (token.Value.Length == 3 || token.Value.Length == 4 || token.Value.Length == 6 || token.Value.Length == 8)
                    {
                        var r = token.Value.Length <= 4 ? token.Value[0..1] + token.Value[0..1] : token.Value[0..2];
                        var g = token.Value.Length <= 4 ? token.Value[1..2] + token.Value[1..2] : token.Value[2..4];
                        var b = token.Value.Length <= 4 ? token.Value[2..3] + token.Value[2..3] : token.Value[4..6];
                        var a = token.Value.Length == 4 ? token.Value[3..4] + token.Value[3..4] : token.Value.Length == 8 ? token.Value[6..8] : "FF";
                        cssValues.Add(new CssColorValue(byte.Parse(r, NumberStyles.HexNumber), byte.Parse(g, NumberStyles.HexNumber), byte.Parse(b, NumberStyles.HexNumber), byte.Parse(a, NumberStyles.HexNumber)));
                        yield return cssValues[^1];
                    }
                    break;
                case CssTokeniser.TokenType.ValueFunctionStart:
                    if (cssValues == null) throw new InvalidDataException($"Top of stack expected to have CssValues; got {stack.Peek().GetType().Name}");
                    var function = new CssFunctionValue(token.Value, []);
                    cssValues.Add(function);
                    stack.Push(function);
                    yield return function;
                    break;
                case CssTokeniser.TokenType.ValueFunctionEnd:
                    if (cssFunctionValue == null) throw new InvalidDataException($"Top of stack expected to have CssFunctionValue; got {stack.Peek().GetType().Name}");
                    stack.Pop();
                    break;
                case CssTokeniser.TokenType.None:
                    break;
                default:
                    Console.Error.WriteLine(token);
                    break;
            }
        }
        yield break;
    }

    public void Parse()
    {
        foreach (var _ in GetNodes()) ;
    }

    [GeneratedRegex("[0-9.]+")]
    private static partial Regex NumberRegex();
}
