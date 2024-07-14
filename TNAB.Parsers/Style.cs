namespace TNAB.Parsers;

public abstract record StyleNode();

public abstract record StyleSheet(string Type) : StyleNode();
public record CssStyleSheet(CustomList<CssStatement> Statements) : StyleSheet("text/css");

public abstract record CssStatement() : StyleNode();
public abstract record CssGroupingStatement(CustomList<CssStatement> Statements) : CssStatement();
public record CssAtRule(string Name, CustomList<CssStyleValue> Values) : CssGroupingStatement([]);
public record CssRuleSet(CustomList<CssSelector> Selectors) : CssGroupingStatement([]);
public record CssDeclaration(string Name, CustomList<CssStyleValue> Values, bool Important) : CssStatement();

public record CssSelector(CustomList<CssSelectorComponent> Components) : StyleNode();
public record CssSelectorComponent(CssCombinator Combinator, CustomList<CssSimpleSelector> Selectors) : StyleNode();
public enum CssCombinator
{
    Unset,
    Descendant,
    Child,
    NextSibling,
    SubsequentSibling,
}

public abstract record CssSimpleSelector(CustomList<CssStyleValue> Values) : StyleNode();
public record CssUniversalSelector() : CssSimpleSelector([]);
public record CssTypeSelector(string Type) : CssSimpleSelector([]);
public record CssClassSelector(string Class) : CssSimpleSelector([]);
public record CssIDSelector(string ID) : CssSimpleSelector([]);
public record CssAttributeSelector() : CssSimpleSelector([]);
public record CssPseudoClassSelector(string PseudoClass) : CssSimpleSelector([]);
public record CssPseudoElementSelector(string PseudoElement) : CssSimpleSelector([]);

public abstract record CssStyleValue() : StyleNode();
public record CssOperatorValue(string Value) : CssStyleValue();
public record CssKeywordValue(string Value) : CssStyleValue();
public record CssStringValue(string Value) : CssStyleValue();
public abstract record CssNumericValue(double Value) : CssStyleValue();
public record CssUnitValue(double Value, CssUnit Unit) : CssNumericValue(Value)
{
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
    public CssUnitGroup UnitGroup => Unit switch
    {
        CssUnit.Number => CssUnitGroup.Number,
        CssUnit.Percent => CssUnitGroup.Percentage,
        CssUnit.Cap or CssUnit.Ch or CssUnit.Em or CssUnit.Ex or CssUnit.Ic or CssUnit.Lh or CssUnit.Rcap or CssUnit.Rch or CssUnit.Rem or CssUnit.Rex or CssUnit.Ric or CssUnit.Rlh or CssUnit.Vw or CssUnit.Vh or CssUnit.Vi or CssUnit.Vb or CssUnit.Vmin or CssUnit.Vmax or CssUnit.Svw or CssUnit.Svh or CssUnit.Svi or CssUnit.Svb or CssUnit.Svmin or CssUnit.Svmax or CssUnit.Lvw or CssUnit.Lvh or CssUnit.Lvi or CssUnit.Lvb or CssUnit.Lvmin or CssUnit.Lvmax or CssUnit.Dvw or CssUnit.Dvh or CssUnit.Dvi or CssUnit.Dvb or CssUnit.Dvmin or CssUnit.Dvmax or CssUnit.Cqw or CssUnit.Cqh or CssUnit.Cqi or CssUnit.Cqb or CssUnit.Cqmin or CssUnit.Cqmax or CssUnit.Cm or CssUnit.Mm or CssUnit.Q or CssUnit.In or CssUnit.Pt or CssUnit.Pc or CssUnit.Px => CssUnitGroup.Length,
        CssUnit.Deg or CssUnit.Grad or CssUnit.Rad or CssUnit.Turn => CssUnitGroup.Angle,
        CssUnit.S or CssUnit.Ms => CssUnitGroup.Time,
        CssUnit.Hz or CssUnit.KHz => CssUnitGroup.Frequency,
        CssUnit.Dpi or CssUnit.Dpcm or CssUnit.Dppx => CssUnitGroup.Resolution,
        CssUnit.Fr => CssUnitGroup.Flex,
    };
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
}
public record CssColorValue(byte R, byte G, byte B, byte A) : CssStyleValue();
public record CssFunctionValue(string Name, CustomList<CssStyleValue> Values) : CssStyleValue();

// <integer> = /[+-]?[0-9]+/
//   <number> = <integer> | /[+-]?[0-9]+\.[0-9]+/
//     <percentage> = <number> <percentage-unit>
//     <length> = <number> <length-unit>
//     <angle> = <number> <angle-unit>
//     <time> = <number> <time-unit>
//     <frequency> = <number> <frequency-unit>
//     <resolution> = <number> <resolution-unit>
//     <flex> = <number> <flex-unit>
// <length-percentage> = <length> | <percentage>
// <color> = <named-color> | <system-color> | 'currentcolor' | <hex-color> | <color-functions> ...
// <image> = url() | <gradient> | element() | image() | cross-fade() | image-set()
// <url> = url()
// <custom-ident> = /(?:[A-Za-z_]|-[A-Za-z_-])[A-Za-z0-9_-]*/
// <transform-function>

public enum CssUnitGroup
{
    Number,
    Percentage,
    Length,
    Angle,
    Time,
    Frequency,
    Resolution,
    Flex,
}

public enum CssUnit
{
    Number,

    // <percentage>
    Percent,

    // <length>
    Cap,
    Ch,
    Em,
    Ex,
    Ic,
    Lh,
    Rcap,
    Rch,
    Rem,
    Rex,
    Ric,
    Rlh,
    Vw,
    Vh,
    Vi,
    Vb,
    Vmin,
    Vmax,
    Svw,
    Svh,
    Svi,
    Svb,
    Svmin,
    Svmax,
    Lvw,
    Lvh,
    Lvi,
    Lvb,
    Lvmin,
    Lvmax,
    Dvw,
    Dvh,
    Dvi,
    Dvb,
    Dvmin,
    Dvmax,
    Cqw,
    Cqh,
    Cqi,
    Cqb,
    Cqmin,
    Cqmax,
    Cm,
    Mm,
    Q,
    In,
    Pt,
    Pc,
    Px,

    // <angle>
    Deg,
    Grad,
    Rad,
    Turn,

    // <time>
    S,
    Ms,

    // <frequency>
    Hz,
    KHz,

    // <resolution>
    Dpi,
    Dpcm,
    Dppx,

    // <flex>
    Fr,
}
