using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
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
    public Size Viewport { get; set; } = new(800, 600);

    readonly MarkupDocument Document;
    readonly RichTextOptions FontDefault;

    List<CssGroupingStatement> CssStatements = [];

    public BoxParser(MarkupDocument document)
    {
        Document = document;
        FontDefault = new RichTextOptions(SystemFonts.CreateFont("Arial", 16));
        Root = new(Document, new RectangleF { Size = Viewport }, new("", "flow"), []);
    }

    public void Parse()
    {
        CssStatements = [.. Document.OfType<MarkupStyleSheet>().Select(node => node.StyleSheet).OfType<CssStyleSheet>().SelectMany(sheet => sheet.Statements).OfType<CssGroupingStatement>()];
        // NOTE: Forcing the rectangle of the root to be the viewport should probably not be required here
        Root = Layout(new RectangleF { Size = Viewport }, Measure([], Root.Node, new RectangleF { Size = Viewport })) with { Rectangle = new RectangleF { Size = Viewport } };
    }

    static BoxNode Layout(RectangleF rectangle, BoxMeasure measure, BoxFlow? flow = null)
    {
        if (measure.Node is MarkupText)
        {
            // FIXME: Implement text flow here
            if (flow != null) rectangle = flow.Add(measure.MaxContent);
            if (measure.Style.Font != null) measure.Style.Font.Origin = rectangle.Location;
            return new(measure.Node, new RectangleF(rectangle.Location, measure.MaxContent), measure.Style, []);
        }
        else if (flow != null && measure.Style.DisplayOutside == "inline")
        {
            return measure.Style.DisplayInside switch
            {
                "none" => new(measure.Node, RectangleF.Empty, measure.Style, []),
                "flow" => LayoutFlow(rectangle, measure, flow),
                _ => throw new NotImplementedException(),
            };
        }
        else
        {
            var borderRectangle = rectangle;
            borderRectangle.Inflate(measure.Style.Box?.Margin?.Inflate ?? SizeF.Empty);
            borderRectangle.Offset(measure.Style.Box?.Margin?.Offset ?? PointF.Empty);
            var contentRectangle = rectangle;
            contentRectangle.Inflate(measure.Style.Box?.Edges.Inflate ?? SizeF.Empty);
            contentRectangle.Offset(measure.Style.Box?.Edges.Offset ?? PointF.Empty);
            switch (measure.Style.DisplayInside)
            {
                case "none":
                    return new(measure.Node, RectangleF.Empty, measure.Style, []);
                case "flow":
                    flow = new(contentRectangle);
                    var layout = LayoutFlow(contentRectangle, measure, flow);
                    borderRectangle.Height -= flow.End();
                    return layout with { Rectangle = borderRectangle };
                default:
                    throw new NotImplementedException();
            }
        }
    }

    static BoxNode LayoutFlow(RectangleF rectangle, BoxMeasure measure, BoxFlow flow)
    {
        rectangle = flow.OffsetRectangle;
        var children = new CustomList<BoxNode>();
        foreach (var child in measure.Children)
        {
            if (child.Style.DisplayOutside == "block") flow.Newline();
            children.Add(Layout(flow.OffsetRectangle, child, flow));
            if (child.Style.DisplayOutside == "block") flow.Newline();
        }
        return new(measure.Node, rectangle, measure.Style, children);
    }

    class BoxFlow(RectangleF box)
    {
        public RectangleF Rectangle = box;
        public SizeF Offset = SizeF.Empty;
        public float MaxHeight = 0;

        public RectangleF OffsetRectangle => new(Rectangle.X + Offset.Width, Rectangle.Y + Offset.Height, Rectangle.Width - Offset.Width, Rectangle.Height - Offset.Height);

        public RectangleF Add(SizeF size)
        {
            if (size.Width > OffsetRectangle.Width) Newline();
            var rectangle = OffsetRectangle;
            Offset.Width += size.Width;
            MaxHeight = Math.Max(MaxHeight, size.Height);
            return rectangle;
        }

        public void Newline()
        {
            Offset.Height += MaxHeight;
            Offset.Width = 0;
            MaxHeight = 0;
        }

        public float End()
        {
            Newline();
            return OffsetRectangle.Height;
        }
    }

    record BoxMeasure(MarkupNode Node, BoxStyle Style, SizeF MinContent, SizeF MaxContent, CustomList<BoxMeasure> Children);

    BoxMeasure Measure(CustomDictionary<string, CustomList<CssStyleValue>> parentStyles, MarkupNode node, RectangleF rectangle)
    {
        var styles = GetStyles(parentStyles, node);
        var color = new BoxColor(
            styles.GetValueOrDefault("color")?.OfType<CssColorValue>().Select(ccv => (Color?)Color.FromRgba(ccv.R, ccv.G, ccv.B, ccv.A)).FirstOrDefault(),
            (styles.GetValueOrDefault("background-color") ?? styles.GetValueOrDefault("background") ?? []).OfType<CssColorValue>().Select<CssColorValue, Color?>(ccv => Color.FromRgba(ccv.R, ccv.G, ccv.B, ccv.A)).FirstOrDefault(),
            null,
            null,
            null,
            null
        );

        if (node is MarkupText text)
        {
            if (string.IsNullOrWhiteSpace(text.Value))
            {
                return new(node, new("FIXME:", "FIXME:"), SizeF.Empty, SizeF.Empty, []);
            }

            var style = new BoxStyle("FIXME:", "FIXME:")
            {
                Color = color,
                Font = new(FontDefault),
                Text = new(text.Value.Replace("\n", "").Replace("\r", "").Replace("\t", ""), null, null, null, null, null, null, null, null)
            };
            style.Font.WrappingLength = rectangle.Width;
            var bounds = TextMeasurer.MeasureAdvance(style.Text.Text, style.Font);
            var textSize = new SizeF(bounds.Width, bounds.Height);
            return new(node, style, textSize, textSize, []);
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
            contentRectangle.Inflate(style.Box.Edges.Inflate);
            contentRectangle.Offset(style.Box.Edges.Offset);
            var children = new CustomList<BoxMeasure>(node.Children.Select(child => Measure(styles, child, contentRectangle)));
            var minContent = SizeF.Empty;
            var maxContent = SizeF.Empty;
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

    static BoxEdges? GetRect(CustomDictionary<string, CustomList<CssStyleValue>> styles, string prefix, string suffix = "")
    {
        var shv = styles.GetValueOrDefault($"{prefix}{suffix}")?.OfType<CssUnitValue>().Select(GetLength).ToList() ?? [];
        var top = styles.GetValueOrDefault($"{prefix}-top{suffix}")?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault();
        var right = styles.GetValueOrDefault($"{prefix}-right{suffix}")?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault();
        var bottom = styles.GetValueOrDefault($"{prefix}-bottom{suffix}")?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault();
        var left = styles.GetValueOrDefault($"{prefix}-left{suffix}")?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault();
        if (shv.Count == 0 && top == null && right == null && bottom == null && left == null) return null;
        var sh = shv.Count == 4 ? new BoxEdges(shv[3], shv[0], shv[1], shv[2]) :
                 shv.Count == 3 ? new BoxEdges(shv[1], shv[0], shv[1], shv[2]) :
                 shv.Count == 2 ? new BoxEdges(shv[1], shv[0], shv[1], shv[0]) :
                 shv.Count == 1 ? new BoxEdges(shv[0], shv[0], shv[0], shv[0]) :
                 new BoxEdges(0, 0, 0, 0);
        return new(left ?? sh.Left, top ?? sh.Top, right ?? sh.Right, bottom ?? sh.Bottom);
    }

    static BoxBorder? GetBorder(CustomDictionary<string, CustomList<CssStyleValue>> styles, string prefix)
    {
        return new BoxBorder(
            // GetRect(rules, prefix, "-width") ?? RectangleF.Empty,
            // TODO: Resolve these units properly
            (styles.GetValueOrDefault($"{prefix}-width") ?? styles.GetValueOrDefault(prefix))?.OfType<CssUnitValue>().Select(GetLength).FirstOrDefault() ?? 0,
            (styles.GetValueOrDefault($"{prefix}-style") ?? styles.GetValueOrDefault(prefix))?.OfType<CssKeywordValue>().Select(cuv => cuv.Value).FirstOrDefault() ?? "none",
            (styles.GetValueOrDefault($"{prefix}-color") ?? styles.GetValueOrDefault(prefix))?.OfType<CssColorValue>().Select(ccv => (Color?)Color.FromRgba(ccv.R, ccv.G, ccv.B, ccv.A)).FirstOrDefault() ?? Color.Transparent
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
