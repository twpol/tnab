using SkiaSharp;
using TNAB.Layout;
using TNAB.Parsers;

namespace TNAB.Renderer.Skia;

public class SkiaRenderer(BoxNode root)
{
    readonly BoxNode Root = root;

    public SKImage Render()
    {
        var surface = SKSurface.Create(new SKImageInfo((int)Root.Rectangle.Width, (int)Root.Rectangle.Height));
        Render(surface.Canvas);
        return surface.Snapshot();
    }

    public void Render(SKCanvas canvas)
    {
        canvas.Clear(SKColors.White);
        RenderNode(canvas, Root);
    }

    static void RenderNode(SKCanvas canvas, BoxNode node)
    {
        if (node.Style.Color?.BackgroundColor != null)
        {
            var paint = new SKPaint
            {
                Color = node.Style.Color.BackgroundColor.Value,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawRect(node.Rectangle, paint);
        }
        if (node.Node is Text)
        {
            if (node.Style.Text != null && node.Style.Font != null)
            {
                canvas.DrawText(node.Style.Text.Text, node.Rectangle.Left, node.Rectangle.Top - node.Style.Font.FontMetrics.Ascent, node.Style.Font);
            }
        }
        foreach (var child in node.Children)
        {
            RenderNode(canvas, child);
        }
        if (node.Style.Box?.Border != null && node.Style.Box.Border.Width > 0 && node.Style.Box.Border.Style != "none" && node.Style.Box.Border.Color != SKColors.Transparent)
        {
            var paint = new SKPaint
            {
                Color = node.Style.Box.Border.Color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = node.Style.Box.Border.Width,
            };
            switch (node.Style.Box.Border.Style)
            {
                case "dotted":
                    paint.PathEffect = SKPathEffect.CreateDash([1, 1], 0);
                    break;
                case "dashed":
                    paint.PathEffect = SKPathEffect.CreateDash([3, 3], 0);
                    break;
                case "solid":
                    break;
                case "double":
                    paint.StrokeWidth *= 2;
                    break;
                case "groove":
                case "ridge":
                case "inset":
                case "outset":
                    break;
            }
            var rectangle = node.Rectangle;
            rectangle.Inflate(-paint.StrokeWidth / 2, -paint.StrokeWidth / 2);
            canvas.DrawRect(rectangle, paint);
        }
    }
}
