using SkiaSharp;
using TNAB.Parsers;

namespace TNAB.Layout;

public record BoxNode(MarkupNode Node, SKRect Rectangle, BoxStyle Style, CustomList<BoxNode> Children);
public record BoxStyle(string DisplayOutside, string DisplayInside)
{
    // CSS 1 $ 5.2
    public SKPaint? Font;
    // CSS 1 $ 5.3
    public BoxColor? Color;
    // CSS 1 $ 5.4
    public BoxText? Text;
    // CSS 1 $ 5.5
    public BoxBox? Box;
    // CSS 1 $ 5.6
    public BoxClassification? Classification;
}
public record BoxColor(SKColor? Color, SKColor? BackgroundColor, object? BackgroundImage, object? BackgroundRepeat, object? BackgroundAttachment, object? BackgroundPosition);
public record BoxText(string Text, object? WordSpacing, object? LetterSpacing, object? TextDecoration, object? VerticalAlign, object? TextTransform, object? TextAlign, object? TextIndent, object? LineHeight);
public record BoxBox(SKRect? Margin, SKRect? Padding, BoxBorder? Border, object? Width, object? Height, object? Float, object? Clear)
{
    public SKRect Edges => new(
        (Margin?.Left ?? 0) + (Border?.Width ?? 0) + (Padding?.Left ?? 0),
        (Margin?.Top ?? 0) + (Border?.Width ?? 0) + (Padding?.Top ?? 0),
        (Margin?.Right ?? 0) + (Border?.Width ?? 0) + (Padding?.Right ?? 0),
        (Margin?.Bottom ?? 0) + (Border?.Width ?? 0) + (Padding?.Bottom ?? 0)
    );
}
public record BoxBorder(float Width, string Style, SKColor Color);
public record BoxClassification(object? WhiteSpace, object? ListStyleType, object? ListStyleImage, object? ListStylePosition);
