using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using SkiaSharp;
using TNAB.Browser;
using TNAB.Layout;
using TNAB.Network;
using TNAB.Renderer.Skia;

namespace TNAB.App.WinForms
{
    public partial class FormApp : Form
    {
        readonly NetworkManager NetworkManager;
        readonly Navigable Navigable;

        BoxParser BoxParser;
        Rectangle RectangleViewport;
        Bitmap BitmapViewport;
        SKImageInfo SKImageInfoViewport;

        public FormApp()
        {
            InitializeComponent();

            NetworkManager = new NetworkManager();
            Navigable = new Navigable(NetworkManager);
            BoxParser = new BoxParser(Navigable.ActiveDocument);
            ResizeViewport();
        }

        [MemberNotNull(nameof(BitmapViewport))]
        private void ResizeViewport()
        {
            RectangleViewport = new Rectangle(Point.Empty, PanelViewport.Size);
            BitmapViewport = new Bitmap(RectangleViewport.Width, RectangleViewport.Height, PixelFormat.Format32bppPArgb);
            SKImageInfoViewport = new SKImageInfo(RectangleViewport.Width, RectangleViewport.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        }

        private void DoLayout()
        {
            BoxParser = new BoxParser(Navigable.ActiveDocument)
            {
                Viewport = new SKSizeI(BitmapViewport.Width, BitmapViewport.Height)
            };
            BoxParser.Parse();
            PanelViewport.Invalidate();
        }

        private void TextBoxUri_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None) ButtonLoad_Click(sender, e);
        }

        private async void ButtonLoad_Click(object sender, EventArgs e)
        {
            if (!Uri.TryCreate(TextBoxUri.Text, UriKind.Absolute, out var uri) && !Uri.TryCreate("https://" + TextBoxUri.Text, UriKind.Absolute, out uri)) return;
            TextBoxUri.Text = uri.ToString();
            try
            {
                await Navigable.Navigate(uri);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            DoLayout();
        }

        private void FormApp_Resize(object sender, EventArgs e)
        {
            var size = PanelViewport.Size;
            Task.Delay(100).ContinueWith(task =>
            {
                if (size == PanelViewport.Size)
                {
                    ResizeViewport();
                    DoLayout();
                }
                task.Dispose();
            });
        }

        private void PanelViewport_Paint(object sender, PaintEventArgs e)
        {
            var renderer = new SkiaRenderer(BoxParser.Root);
            var image = renderer.Render();
            var data = BitmapViewport.LockBits(RectangleViewport, ImageLockMode.WriteOnly, BitmapViewport.PixelFormat);
            using (var surface = SKSurface.Create(SKImageInfoViewport, data.Scan0, data.Stride))
            {
                surface.Canvas.DrawImage(image, 0, 0);
                surface.Canvas.Flush();
            }
            BitmapViewport.UnlockBits(data);
            e.Graphics.DrawImage(BitmapViewport, 0, 0);
        }
    }
}
