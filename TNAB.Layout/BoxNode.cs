using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using TNAB.Parsers;

namespace TNAB.Layout;

public record BoxNode(MarkupNode Node, RectangleF Rectangle, BoxStyle Style, CustomList<BoxNode> Children);
public record BoxStyle(string DisplayOutside, string DisplayInside)
{
    // CSS 1 $ 5.2
    public RichTextOptions? Font;
    // CSS 1 $ 5.3
    public BoxColor? Color;
    // CSS 1 $ 5.4
    public BoxText? Text;
    // CSS 1 $ 5.5
    public BoxBox? Box;
    // CSS 1 $ 5.6
    public BoxClassification? Classification;
}
public record BoxColor(Color? Color, Color? BackgroundColor, object? BackgroundImage, object? BackgroundRepeat, object? BackgroundAttachment, object? BackgroundPosition);
public record BoxText(string Text, object? WordSpacing, object? LetterSpacing, object? TextDecoration, object? VerticalAlign, object? TextTransform, object? TextAlign, object? TextIndent, object? LineHeight);
public record BoxBox(BoxEdges? Margin, BoxEdges? Padding, BoxBorder? Border, object? Width, object? Height, object? Float, object? Clear)
{
    public BoxEdges Edges => new(
        (Margin?.Left ?? 0) + (Border?.Width ?? 0) + (Padding?.Left ?? 0),
        (Margin?.Top ?? 0) + (Border?.Width ?? 0) + (Padding?.Top ?? 0),
        (Margin?.Right ?? 0) + (Border?.Width ?? 0) + (Padding?.Right ?? 0),
        (Margin?.Bottom ?? 0) + (Border?.Width ?? 0) + (Padding?.Bottom ?? 0)
    );
}
public record BoxEdges(float Left, float Top, float Right, float Bottom)
{
    public SizeF Inflate => new(-Left - Right, -Top - Bottom);
    public PointF Offset => new(Left, Top);
}
public record BoxBorder(float Width, string Style, Color Color);
public record BoxClassification(object? WhiteSpace, object? ListStyleType, object? ListStyleImage, object? ListStylePosition);
