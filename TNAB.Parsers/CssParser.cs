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

    static readonly HashSet<string> PropertyFeatures = [
        "repeat",
    ];

    static readonly List<string> CustomAtRuleProperties = [
        "page",
    ];

    static readonly Dictionary<string, string> FeatureGroups = new()
    {
        { "css.types.color-mix", "css.types.color.color-mix" },
        { "css.types.conic-gradient", "css.types.image.gradient.conic-gradient" },
        { "css.types.cubic-bezier", "css.types.easing-function.cubic-bezier" },
        { "css.types.grayscale", "css.types.filter-function.grayscale" },
        { "css.types.hsl", "css.types.color.hsl" },
        { "css.types.hsla", "css.types.color.hsl" },
        { "css.types.inset", "css.types.basic-shape.inset" },
        { "css.types.invert", "css.types.filter-function.invert" },
        { "css.types.light-dark", "css.types.color.light-dark" },
        { "css.types.linear-gradient", "css.types.image.gradient.linear-gradient" },
        { "css.types.oklch", "css.types.color.oklch" },
        { "css.types.radial-gradient", "css.types.image.gradient.radial-gradient" },
        { "css.types.repeating-radial-gradient", "css.types.image.gradient.repeating-radial-gradient" },
        { "css.types.rgb", "css.types.color.rgb" },
        { "css.types.rgba", "css.types.color.rgb" },
        { "css.types.rotate", "css.types.transform-function.rotate" },
        { "css.types.rotateX", "css.types.transform-function.rotateX" },
        { "css.types.rotateY", "css.types.transform-function.rotateY" },
        { "css.types.rotateZ", "css.types.transform-function.rotateZ" },
        { "css.types.scale", "css.types.transform-function.scale" },
        { "css.types.scaleX", "css.types.transform-function.scaleX" },
        { "css.types.scaleY", "css.types.transform-function.scaleY" },
        { "css.types.scaleZ", "css.types.transform-function.scaleZ" },
        { "css.types.skew", "css.types.transform-function.skew" },
        { "css.types.steps", "css.types.easing-function.steps" },
        { "css.types.translate", "css.types.transform-function.translate" },
        { "css.types.translateX", "css.types.transform-function.translateX" },
        { "css.types.translateY", "css.types.transform-function.translateY" },
        { "css.types.translateZ", "css.types.transform-function.translateZ" },
        { "css.types.var", "css.properties.custom-property.var" },
    };

    public IEnumerable<StyleNode> GetNodes()
    {
        var stack = new Stack<StyleNode>();
        stack.Push(Root);
        var inGroupingStatements = false;
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
                    OnFeatureUsed(new($"css.at-rules.{token.Value}"));
                    var atRule = new CssAtRule(token.Value, []);
                    propStatements.Add(atRule);
                    stack.Push(atRule);
                    inGroupingStatements = false;
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
                    if (!token.Value.StartsWith("--"))
                    {
                        if (cssAtRule != null && CustomAtRuleProperties.Contains(cssAtRule.Name))
                        {
                            OnFeatureUsed(new($"css.at-rules.{cssAtRule.Name}.{token.Value}"));
                        }
                        else
                        {
                            OnFeatureUsed(new($"css.properties.{token.Value}"));
                        }
                    }
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
                    OnFeatureUsed(new("css.selectors.universal"));
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssUniversalSelector());
                    break;
                case CssTokeniser.TokenType.SelectorType:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    OnFeatureUsed(new("css.selectors.type"));
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssTypeSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorClass:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    OnFeatureUsed(new("css.selectors.class"));
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssClassSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorID:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    OnFeatureUsed(new("css.selectors.id"));
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssIDSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorAttribute:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    OnFeatureUsed(new("css.selectors.attribute"));
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssAttributeSelector());
                    break;
                case CssTokeniser.TokenType.SelectorPseudoClass:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    OnFeatureUsed(new($"css.selectors.{token.Value}"));
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssPseudoClassSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorPseudoElement:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    OnFeatureUsed(new($"css.selectors.{token.Value}"));
                    cssRuleSet.Selectors[^1].Components[^1].Selectors.Add(new CssPseudoElementSelector(token.Value));
                    break;
                case CssTokeniser.TokenType.SelectorFunctionStart:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    // FIXME: OnFeatureUsed(new($"css.types.{token.Value}"));
                    stack.Push(cssRuleSet.Selectors[^1].Components[^1].Selectors[^1]);
                    break;
                case CssTokeniser.TokenType.SelectorFunctionEnd:
                    if (cssSimpleSelector == null) throw new InvalidDataException($"Top of stack expected CssSimpleSelector; got {stack.Peek().GetType().Name}");
                    stack.Pop();
                    break;
                case CssTokeniser.TokenType.SelectorCombinator:
                    if (cssRuleSet == null) throw new InvalidDataException($"Top of stack expected CssRuleSet; got {stack.Peek().GetType().Name}");
                    OnFeatureUsed(new($"css.selectors.{token.Value switch
                    {
                        " " => "descendant",
                        ">" => "child",
                        "+" => "next-sibling",
                        "~" => "subsequent-sibling",
                        _ => throw new InvalidDataException($"Unknown combinator {token.Value}")
                    }}"));
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
                    inGroupingStatements = true;
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
                    OnFeatureUsed(new("css.types.string"));
                    propValues.Add(new CssStringValue(token.Value));
                    yield return propValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueNumber:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    OnFeatureUsed(new("css.types.number"));
                    propValues.Add(new CssUnitValue(double.Parse(token.Value), CssUnit.Number));
                    yield return propValues[^1];
                    break;
                case CssTokeniser.TokenType.ValueDimension:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    var number = NumberRegex().Match(token.Value).Value;
                    if (Enum.TryParse(typeof(CssUnit), token.Value[number.Length..], true, out var unit))
                    {
                        // OnFeatureUsed(new($"css.types.{token.Value[number.Length..].ToLowerInvariant()}"));
                        propValues.Add(new CssUnitValue(double.Parse(number), (CssUnit)unit));
                        yield return propValues[^1];
                    }
                    break;
                case CssTokeniser.TokenType.ValueColor:
                    if (propValues == null) throw new InvalidDataException($"Top of stack expected to have values; got {stack.Peek().GetType().Name}");
                    if (token.Value.Length == 3 || token.Value.Length == 4 || token.Value.Length == 6 || token.Value.Length == 8)
                    {
                        OnFeatureUsed(new("css.types.color"));
                        // FIXME: Add sub-features
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
                    if (PropertyFeatures.Contains(token.Value) && cssDeclaration != null)
                    {
                        OnFeatureUsed(new($"css.properties.{cssDeclaration.Name}.{token.Value}"));
                    }
                    else if (cssAtRule != null && !inGroupingStatements)
                    {
                        OnFeatureUsed(new(GetFeatureGrouped($"css.at-rules.{cssAtRule.Name}.{token.Value}")));
                    }
                    else
                    {
                        OnFeatureUsed(new(GetFeatureGrouped($"css.types.{token.Value}")));
                    }
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

    static string GetFeatureGrouped(string feature) => FeatureGroups.TryGetValue(feature, out var value) ? value : feature;

    public void Parse()
    {
        foreach (var _ in GetNodes()) ;
    }

    [GeneratedRegex("[0-9.]+")]
    private static partial Regex NumberRegex();

    public record FeatureUsedEventArgs(string Feature);
    public event EventHandler<FeatureUsedEventArgs>? FeatureUsed;
    protected virtual void OnFeatureUsed(FeatureUsedEventArgs e) => FeatureUsed?.Invoke(this, e);
}
