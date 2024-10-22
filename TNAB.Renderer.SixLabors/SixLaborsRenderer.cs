using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TNAB.Layout;
using TNAB.Parsers;

namespace TNAB.Renderer.SixLabors;

public class SixLaborsRenderer(BoxNode root)
{
    readonly BoxNode Root = root;

    public Image Render()
    {
        var image = new Image<Rgba32>((int)Root.Rectangle.Width, (int)Root.Rectangle.Height);
        image.Mutate(canvas =>
        {
            canvas.Clear(Color.White);
            RenderNode(canvas, Root);
        });
        return image;
    }

    static void RenderNode(IImageProcessingContext canvas, BoxNode node)
    {
        if (node.Style.Color?.BackgroundColor != null)
        {
            canvas.Fill(new SolidBrush(node.Style.Color.BackgroundColor.Value), node.Rectangle);
        }
        if (node.Node is MarkupText)
        {
            if (node.Style.Font != null && node.Style.Text != null && node.Style.Color?.Color != null)
            {
                canvas.DrawText(node.Style.Font, node.Style.Text.Text, node.Style.Color.Color.Value);
            }
        }
        foreach (var child in node.Children)
        {
            RenderNode(canvas, child);
        }
        if (node.Style.Box?.Border != null && node.Style.Box.Border.Width > 0 && node.Style.Box.Border.Style != "none" && node.Style.Box.Border.Color != Color.Transparent)
        {
            Pen pen = node.Style.Box.Border.Style switch
            {
                "dotted" => Pens.Dot(node.Style.Box.Border.Color, node.Style.Box.Border.Width),
                "dashed" => Pens.Dash(node.Style.Box.Border.Color, node.Style.Box.Border.Width),
                "solid" => Pens.Solid(node.Style.Box.Border.Color, node.Style.Box.Border.Width),
                _ => Pens.Solid(Color.Transparent),
            };
            var rectangle = node.Rectangle;
            rectangle.Inflate(-pen.StrokeWidth / 2, -pen.StrokeWidth / 2);
            canvas.Draw(pen, rectangle);
        }
    }
}
