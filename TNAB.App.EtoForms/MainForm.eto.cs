using System;
using Eto.Forms;
using Eto.Drawing;

namespace TNAB.App.EtoForms
{
    partial class MainForm : Form
    {
        Button ButtonLoad;
        Command CommandQuit;
        Drawable DrawableViewport;
        Scrollable ScrollableViewport;
        TextBox TextBoxUri;

        void InitializeComponent()
        {
            var layoutGap = GetLayoutGap();

            Title = "TNAB";
            MinimumSize = new Size(30, 40) * layoutGap;
            Size = new Size(100, 75) * layoutGap;
            Padding = new Padding(layoutGap.Width, layoutGap.Height);

            ButtonLoad = new() { Text = "Load" };
            ButtonLoad.Click += ButtonLoad_Click;

            CommandQuit = new() { MenuText = "Quit", Shortcut = Application.Instance.CommonModifier | Keys.Q };
            CommandQuit.Executed += (sender, e) => Application.Instance.Quit();

            DrawableViewport = new();
            DrawableViewport.Paint += DrawableViewport_Paint;
            DrawableViewport.SizeChanged += DrawableViewport_SizeChanged;

            ScrollableViewport = new() { Content = DrawableViewport };

            TextBoxUri = new();
            TextBoxUri.KeyUp += TextBoxUri_KeyUp;

            Menu = new MenuBar
            {
                Items =
                {
                    new SubMenuItem { Text = "&File", Items = {} },
                },
                QuitItem = CommandQuit,
            };

            var layout = new DynamicLayout()
            {
                DefaultSpacing = layoutGap,
            };

            layout.BeginVertical();
            layout.BeginHorizontal();
            layout.Add(TextBoxUri, xscale: true);
            layout.Add(ButtonLoad);
            layout.EndHorizontal();
            layout.EndVertical();

            layout.BeginVertical(yscale: true);
            layout.Add(ScrollableViewport);
            layout.EndVertical();

            Content = layout;
        }

        /// <summary>
        /// Calculate a UI font-based gap to use for separating controls
        /// </summary>
        static Size GetLayoutGap()
        {
            var size = SystemFonts.Message().MeasureString("X");
            return new Size((int)Math.Round(size.Height / 2), (int)Math.Round(size.Height / 2));
        }
    }
}
