using System;
using Eto.Forms;
using Eto.Drawing;
using TNAB.Network;
using TNAB.Browser;
using TNAB.Layout;
using SkiaSharp;
using TNAB.Renderer.Skia;
using System.Threading.Tasks;

namespace TNAB.App.EtoForms
{
    public partial class MainForm : Form
    {
        readonly NetworkManager NetworkManager;
        readonly Navigable Navigable;

        BoxParser BoxParser;
        Bitmap BitmapViewport;
        SKImageInfo SKImageInfoViewport;

        public MainForm()
        {
            InitializeComponent();

            NetworkManager = new NetworkManager();
            Navigable = new Navigable(NetworkManager);
            BoxParser = new BoxParser(Navigable.ActiveDocument);
        }

        void DoResize()
        {
            var rectangleViewport = new Rectangle(Point.Empty, DrawableViewport.Size);
            BitmapViewport = new Bitmap(rectangleViewport.Width, rectangleViewport.Height, PixelFormat.Format32bppRgba);
            SKImageInfoViewport = new SKImageInfo(rectangleViewport.Width, rectangleViewport.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        }

        void DoLayout()
        {
            BoxParser = new BoxParser(Navigable.ActiveDocument)
            {
                Viewport = new SKSizeI(BitmapViewport.Width, BitmapViewport.Height)
            };
            BoxParser.Parse();
            DrawableViewport.Invalidate();
        }

        void TextBoxUri_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter && e.Modifiers == Keys.None) ButtonLoad_Click(sender, e);
        }

        async void ButtonLoad_Click(object sender, EventArgs e)
        {
            if (!Uri.TryCreate(TextBoxUri.Text, UriKind.Absolute, out var uri) && !Uri.TryCreate("https://" + TextBoxUri.Text, UriKind.Absolute, out uri)) return;
            TextBoxUri.Text = uri.ToString();
            try
            {
                await Navigable.Navigate(uri);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), MessageBoxType.Error);
            }
            _ = Task.Run(DoLayout);
        }

        void DrawableViewport_SizeChanged(object sender, EventArgs e)
        {
            var size = DrawableViewport.Size;
            Task.Delay(100).ContinueWith(task =>
            {
                if (size == DrawableViewport.Size)
                {
                    DoResize();
                    DoLayout();
                }
                task.Dispose();
            });
        }
        void DrawableViewport_Paint(object sender, PaintEventArgs e)
        {
            var renderer = new SkiaRenderer(BoxParser.Root);
            using (var data = BitmapViewport.Lock())
            {
                using var surface = SKSurface.Create(SKImageInfoViewport, data.Data, data.ScanWidth);
                renderer.Render(surface.Canvas);
                surface.Canvas.Flush();
            }
            e.Graphics.DrawImage(BitmapViewport, 0, 0);
        }
    }
}
