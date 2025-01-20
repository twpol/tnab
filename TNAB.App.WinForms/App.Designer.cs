namespace TNAB.App.WinForms
{
    partial class FormApp
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            ButtonLoad = new Button();
            TextBoxUri = new TextBox();
            PanelViewport = new Panel();
            SuspendLayout();
            // 
            // ButtonLoad
            // 
            ButtonLoad.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ButtonLoad.Location = new Point(713, 12);
            ButtonLoad.Name = "ButtonLoad";
            ButtonLoad.Size = new Size(75, 23);
            ButtonLoad.TabIndex = 1;
            ButtonLoad.Text = "Load";
            ButtonLoad.UseVisualStyleBackColor = true;
            ButtonLoad.Click += ButtonLoad_Click;
            // 
            // TextBoxUri
            // 
            TextBoxUri.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            TextBoxUri.BorderStyle = BorderStyle.FixedSingle;
            TextBoxUri.Location = new Point(12, 12);
            TextBoxUri.Name = "TextBoxUri";
            TextBoxUri.Size = new Size(695, 23);
            TextBoxUri.TabIndex = 0;
            TextBoxUri.KeyUp += TextBoxUri_KeyUp;
            // 
            // PanelViewport
            // 
            PanelViewport.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            PanelViewport.BorderStyle = BorderStyle.FixedSingle;
            PanelViewport.Location = new Point(12, 41);
            PanelViewport.Name = "PanelViewport";
            PanelViewport.Size = new Size(776, 397);
            PanelViewport.TabIndex = 2;
            PanelViewport.Paint += PanelViewport_Paint;
            // 
            // FormApp
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(PanelViewport);
            Controls.Add(TextBoxUri);
            Controls.Add(ButtonLoad);
            Name = "FormApp";
            Text = "TNAB";
            Resize += FormApp_Resize;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button ButtonLoad;
        private TextBox TextBoxUri;
        private Panel PanelViewport;
    }
}
