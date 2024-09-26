using SkiaSharp;
using TNAB.Parsers;

namespace TNAB.Layout;

public class BoxParser
{
    static readonly string[] DisplayOutsideValues = ["block", "inline"];
    static readonly string[] DisplayInsideValues = ["none", "flow"];
    static readonly string[] InheritedProperties = [
        "color"
    ];

    public BoxNode Root;
    public SKSizeI Viewport { get; set; } = new(800, 600);

    readonly MarkupDocument Document;
    readonly SKPaint FontDefault;

    List<CssGroupingStatement> CssStatements = [];

    public BoxParser(MarkupDocument document)
    {
        Document = document;
        FontDefault = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 16,
            Typeface = SKTypeface.Default,
            IsAntialias = true,
            LcdRenderText = true,
            SubpixelText = true,
        };
        Root = new(Document, SKRect.Create(Viewport), new("", "flow"), []);
    }

    public void Parse()
    {
        CssStatements = [.. Document.OfType<MarkupStyleSheet>().Select(node => node.StyleSheet).OfType<CssStyleSheet>().SelectMany(sheet => sheet.Statements).OfType<CssGroupingStatement>()];
        // NOTE: Forcing the rectangle of the root to be the viewport should probably not be required here
        Root = Layout(SKRect.Create(Viewport), Measure([], Root.Node, SKRect.Create(Viewport))) with { Rectangle = SKRect.Create(Viewport) };
    }

    static BoxNode Layout(SKRect rectangle, BoxMeasure measure, BoxFlow? flow = null)
    {
        if (measure.Node is MarkupText)
        {
            // FIXME: Implement text flow here
            if (flow != null) rectangle = flow.Add(measure.MaxContent);
            return new(measure.Node, SKRect.Create(rectangle.Location, measure.MaxContent), measure.Style, []);
        }
        else if (flow != null && measure.Style.DisplayOutside == "inline")
        {
            return measure.Style.DisplayInside switch
            {
                "none" => new(measure.Node, SKRect.Empty, measure.Style, []),
                "flow" => LayoutFlow(rectangle, measure, flow),
                _ => throw new NotImplementedException(),
            };
        }
        else
        {
            var borderRectangle = rectangle;
            borderRectangle.Left += measure.Style.Box?.Margin?.Left ?? 0;
            borderRectangle.Top += measure.Style.Box?.Margin?.Top ?? 0;
            borderRectangle.Right -= measure.Style.Box?.Margin?.Right ?? 0;
            borderRectangle.Bottom -= measure.Style.Box?.Margin?.Bottom ?? 0;
            var contentRectangle = rectangle;
            contentRectangle.Left += measure.Style.Box?.Edges.Left ?? 0;
            contentRectangle.Top += measure.Style.Box?.Edges.Top ?? 0;
            contentRectangle.Right -= measure.Style.Box?.Edges.Right ?? 0;
            contentRectangle.Bottom -= measure.Style.Box?.Edges.Bottom ?? 0;
            switch (measure.Style.DisplayInside)
            {
                case "none":
                    return new(measure.Node, SKRect.Empty, measure.Style, []);
                case "flow":
                    flow = new(contentRectangle);
                    var layout = LayoutFlow(contentRectangle, measure, flow);
                    borderRectangle.Bottom -= flow.End();
                    return layout with { Rectangle = borderRectangle };
                default:
                    throw new NotImplementedException();
            }
        }
    }

    static BoxNode LayoutFlow(SKRect rectangle, BoxMeasure measure, BoxFlow flow)
    {
        rectangle = SKRect.Create(flow.OffsetRectangle.Location, new(0, 0));
        var children = new CustomList<BoxNode>();
        foreach (var child in measure.Children)
        {
            if (child.Style.DisplayOutside == "block") flow.Newline();
            children.Add(Layout(flow.OffsetRectangle, child, flow));
            if (child.Style.DisplayOutside == "block") flow.Rectangle.Top += children[^1].Rectangle.Height;
        }
        return new(measure.Node, rectangle, measure.Style, children);
    }

    class BoxFlow(SKRect box)
    {
        public SKRect Rectangle = box;
        public float Offset = 0;
        public float MaxHeight = 0;

        public SKRect OffsetRectangle => new(Rectangle.Left + Offset, Rectangle.Top, Rectangle.Right, Rectangle.Bottom);

        public SKRect Add(SKSize size)
        {
            if (Offset > 0 && Offset + size.Width > Rectangle.Width) Newline();
            var rectangle = OffsetRectangle;
            Offset += size.Width;
            MaxHeight = Math.Max(MaxHeight, size.Height);
            return rectangle;
        }

        public void Newline()
        {
            Rectangle.Top += MaxHeight;
            Offset = 0;
            MaxHeight = 0;
        }

        public float End()
        {
            Newline();
            return Rectangle.Bottom - Rectangle.Top;
        }
    }

    record BoxMeasure(MarkupNode Node, BoxStyle Style, SKSize MinContent, SKSize MaxContent, CustomList<BoxMeasure> Children);

    BoxMeasure Measure(CustomDictionary<string, CustomList<CssStyleValue>> parentStyles, MarkupNode node, SKRect rectangle)
    {
        var styles = GetStyles(parentStyles, node);
        var color = new BoxColor(
            styles.GetValueOrDefault("color")?.OfType<CssColorValue>().Select(ccv => (SKColor?)new SKColor(ccv.R, ccv.G, ccv.B, ccv.A)).FirstOrDefault(),
            (styles.GetValueOrDefault("background-color") ?? styles.GetValueOrDefault("background") ?? []).OfType<CssColorValue>().Select<CssColorValue, SKColor?>(ccv => new SKColor(ccv.R, ccv.G, ccv.B, ccv.A)).FirstOrDefault(),
            null,
            null,
            null,
            null
        );

        if (node is MarkupText text)
        {
            if (string.IsNullOrWhiteSpace(text.Value))
            {
                return new(node, new("FIXME:", "FIXME:"), SKSize.Empty, SKSize.Empty, []);
            }

            var style = new BoxStyle("FIXME:", "FIXME:")
            {
                Color = color,
                Font = FontDefault,
                Text = new(text.Value.Replace("\n", "").Replace("\r", "").Replace("\t", ""), null, null, null, null, null, null, null, null)
            };
            // TODO: SkiaSharp does not have any means of flowing text
            var bounds = new SKRect(0, 0, 0, 0);
            style.Font.MeasureText(style.Text.Text, ref bounds);
            // NOTE: SkiaSharp only measures the exact text rendered, i.e. it will not include space for descenders if there are none!
            var fontSize = new SKSize(bounds.Width, style.Font.FontMetrics.Descent - style.Font.FontMetrics.Ascent);
            return new(node, style, fontSize, fontSize, []);
        }
        else
        {
            var style = new BoxStyle(
                (styles.GetValueOrDefault("display-outside") ?? styles.GetValueOrDefault("display") ?? [new CssKeywordValue("inline")]).OfType<CssKeywordValue>().FirstOrDefault(keyword => DisplayOutsideValues.Contains(keyword.Value))?.Value ?? "block",
                (styles.GetValueOrDefault("display-inside") ?? styles.GetValueOrDefault("display") ?? []).OfType<CssKeywordValue>().FirstOrDefault(keyword => DisplayInsideValues.Contains(keyword.Value))?.Value ?? "flow"
            )
            {
                Color = color,
                Box = new(
                    GetRect(styles, "margin"),
                    GetRect(styles, "padding"),
                    GetBorder(styles, "border"),
                    null,
                    null,
                    null,
                    null
                )
            };
            var contentRectangle = rectangle;
            contentRectangle.Left += style.Box.Edges.Left;
            contentRectangle.Top += style.Box.Edges.Top;
            contentRectangle.Right -= style.Box.Edges.Right;
            contentRectangle.Bottom -= style.Box.Edges.Bottom;
            var children = new CustomList<BoxMeasure>(node.Children.Select(child => Measure(styles, child, contentRectangle)));
            var minContent = SKSize.Empty;
            var maxContent = SKSize.Empty;
            var line = new CustomList<BoxMeasure>();
            foreach (var child in children)
            {
                if (child.Style.DisplayOutside == "block") line.Clear();
                line.Add(child);
                minContent.Width = Math.Max(minContent.Width, child.MinContent.Width);
                minContent.Height += child.MinContent.Height;
                maxContent.Width = Math.Max(maxContent.Width, line.Sum(item => item.MaxContent.Width));
                maxContent.Height = Math.Max(maxContent.Height, line.Max(item => item.MaxContent.Height));
                if (child.Style.DisplayOutside == "block") line.Clear();
            }
            minContent.Width += style.Box.Edges.Left + style.Box.Edges.Right;
            minContent.Height += style.Box.Edges.Top + style.Box.Edges.Bottom;
            maxContent.Width += style.Box.Edges.Left + style.Box.Edges.Right;
            maxContent.Height += style.Box.Edges.Top + style.Box.Edges.Bottom;
            return new(node, style, minContent, maxContent, children);
        }
    }

    CustomDictionary<string, CustomList<CssStyleValue>> GetStyles(CustomDictionary<string, CustomList<CssStyleValue>> parentStyles, MarkupNode node)
    {
        var styles = new CustomDictionary<string, CustomList<CssStyleValue>>(parentStyles.Where(style => InheritedProperties.Contains(style.Key)).ToDictionary());
        foreach (var statements in CssStatements)
        {
            if (statements.IsMatch(node))
            {
                foreach (var declaration in statements.Statements.OfType<CssDeclaration>())
                {
                    // TODO: Proper specificity and ordering
                    styles[declaration.Name] = declaration.Values;
                }
            }
        }
        return styles;
    }

    static SKRect? GetRect(CustomDictionary<string, CustomList<CssStyleValue>> styles, string prefix, string suffix = "")
    {
        var shv = styles.GetValueOrDefault($"{prefix}{suffix}")?.OfType<CssUnitValue>().Select(GetLength).ToList() ?? [];
        var top = styles.GetValueOrDefault($"{prefix}-top{suffix}")?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault();
        var right = styles.GetValueOrDefault($"{prefix}-right{suffix}")?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault();
        var bottom = styles.GetValueOrDefault($"{prefix}-bottom{suffix}")?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault();
        var left = styles.GetValueOrDefault($"{prefix}-left{suffix}")?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault();
        if (shv.Count == 0 && top == null && right == null && bottom == null && left == null) return null;
        var sh = shv.Count == 4 ? new SKRect(shv[3], shv[0], shv[1], shv[2]) :
                 shv.Count == 3 ? new SKRect(shv[1], shv[0], shv[1], shv[2]) :
                 shv.Count == 2 ? new SKRect(shv[1], shv[0], shv[1], shv[0]) :
                 shv.Count == 1 ? new SKRect(shv[0], shv[0], shv[0], shv[0]) :
                 SKRect.Empty;
        sh.Top = top ?? sh.Top;
        sh.Right = right ?? sh.Right;
        sh.Bottom = bottom ?? sh.Bottom;
        sh.Left = left ?? sh.Left;
        return sh;
    }

    static BoxBorder? GetBorder(CustomDictionary<string, CustomList<CssStyleValue>> styles, string prefix)
    {
        return new BoxBorder(
            // GetRect(rules, prefix, "-width") ?? SKRect.Empty,
            // TODO: Resolve these units properly
            (styles.GetValueOrDefault($"{prefix}-width") ?? styles.GetValueOrDefault(prefix))?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault() ?? 0,
            (styles.GetValueOrDefault($"{prefix}-style") ?? styles.GetValueOrDefault(prefix))?.OfType<CssKeywordValue>().Select(cuv => cuv.Value).FirstOrDefault() ?? "none",
            (styles.GetValueOrDefault($"{prefix}-color") ?? styles.GetValueOrDefault(prefix))?.OfType<CssColorValue>().Select(ccv => (SKColor?)new SKColor(ccv.R, ccv.G, ccv.B, ccv.A)).FirstOrDefault() ?? SKColors.Transparent
        );
    }

    static float GetLength(CssUnitValue unitValue)
    {
        return unitValue.Unit switch
        {
            CssUnit.Cm => (float)unitValue.Value * 96 / 2.54f,
            CssUnit.Mm => (float)unitValue.Value * 96 / 25.4f,
            CssUnit.Q => (float)unitValue.Value * 96 / 101.6f,
            CssUnit.In => (float)unitValue.Value * 96,
            CssUnit.Pc => (float)unitValue.Value * 96 / 6,
            CssUnit.Pt => (float)unitValue.Value * 96 / 72,
            CssUnit.Px => (float)unitValue.Value,
            _ => 0,
        };
    }
}
