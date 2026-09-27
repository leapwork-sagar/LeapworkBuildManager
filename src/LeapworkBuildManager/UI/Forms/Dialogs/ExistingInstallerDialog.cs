using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed class ExistingInstallerDialog : Form
    {
        public ExistingInstallerChoice Choice { get; private set; }
        public string Destination { get; private set; }

        readonly TableLayoutPanel root;
        readonly Panel content;
        readonly TableLayoutPanel footer;
        bool stackedFooter;
        readonly Button saveCopy, replace, cancel;
        readonly ToolTip tips = new ToolTip();
        public bool FooterActionsFit
        {
            get
            {
                return Fits(saveCopy) && Fits(replace) && Fits(cancel);
            }
        }

        bool Fits(Control control)
        {
            return ClientRectangle.Contains(RectangleToClient(control.RectangleToScreen(control.ClientRectangle)));
        }

        public ExistingInstallerDialog(string destination)
        {
            Destination = Path.GetFullPath(destination);
            Text = "Installer already exists";
            Font = Theme.CreateFont(10, FontStyle.Regular);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(620, 450);
            var title = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };
            var caption = new Label
            {
                Text = "Existing installer",
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 8, 0, 0)
            };
            caption.MouseDown += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                }
            };
            var exit = new SoftButton
            {
                Text = "×",
                Width = 44,
                Dock = DockStyle.Right,
                AutoSize = false,
                AccessibleName = "Close dialog"
            };
            exit.Click += delegate
            {
                Close();
            };
            title.Controls.Add(caption);
            title.Controls.Add(exit);
            root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 12, 20, 16),
                ColumnCount = 1,
                RowCount = 2
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Margin = Padding.Empty
            };
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                Margin = Padding.Empty
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.Controls.Add(new Label { Text = "This installer already exists.", Font = Theme.CreateFont(15, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 16) });
            body.Controls.Add(Caption("Installer"));
            body.Controls.Add(PathField(Path.GetFileName(Destination), 44));
            body.Controls.Add(Caption("Destination folder"));
            var location = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 12)
            };
            location.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            location.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            location.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            location.Controls.Add(PathField(Path.GetDirectoryName(Destination), 58), 0, 0);
            var open = ChoiceButton("Open folder", ExistingInstallerChoice.OpenFolder);
            tips.SetToolTip(open, Path.GetDirectoryName(Destination));
            open.Margin = new Padding(10, 9, 0, 15);
            location.Controls.Add(open, 1, 0);
            body.Controls.Add(location);
            body.Controls.Add(new Label { Text = "Save another copy keeps both files with a numbered filename.", AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10) });
            body.Controls.Add(new Label { Text = "Replace keeps the existing installer until the new download succeeds.", AutoSize = true, Dock = DockStyle.Top, ForeColor = Theme.SecondaryText, Margin = new Padding(0, 0, 0, 8) });
            content.Controls.Add(body);
            root.Controls.Add(content, 0, 0);
            footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(0, 16, 0, 4),
                Margin = Padding.Empty
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            cancel = ChoiceButton("Cancel", ExistingInstallerChoice.Cancel);
            replace = ChoiceButton("Replace", ExistingInstallerChoice.Replace);
            saveCopy = ChoiceButton("Save another copy", ExistingInstallerChoice.SaveCopy);
            saveCopy.BackColor = Theme.Accent;
            saveCopy.ForeColor = Theme.AccentText;
            cancel.Margin = new Padding(0, 0, 10, 0);
            replace.Margin = new Padding(0, 0, 10, 0);
            saveCopy.Margin = Padding.Empty;
            footer.Controls.Add(cancel, 0, 0);
            footer.Controls.Add(replace, 1, 0);
            footer.Controls.Add(saveCopy, 2, 0);
            root.Controls.Add(footer, 0, 1);
            Controls.Add(root);
            Controls.Add(title);
            AcceptButton = saveCopy;
            CancelButton = cancel;
            Shown += delegate
            {
                FitToArea(Screen.FromControl(this).WorkingArea);
                ActiveControl = saveCopy;
            };
            FormClosed += delegate
            {
                tips.Dispose();
            };
        }

        public void FitToArea(Rectangle area)
        {
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            PerformLayout();
            if (!stackedFooter && saveCopy.Width < TextRenderer.MeasureText(saveCopy.Text, saveCopy.Font).Width + 16)
            {
                stackedFooter = true;
                root.RowStyles[1].Height *= 2;
                footer.RowCount = 2;
                footer.RowStyles[0].Height = 50;
                footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                footer.SetCellPosition(cancel, new TableLayoutPanelCellPosition(0, 1));
                footer.SetCellPosition(replace, new TableLayoutPanelCellPosition(1, 1));
                footer.SetColumnSpan(replace, 2);
                footer.SetCellPosition(saveCopy, new TableLayoutPanelCellPosition(0, 0));
                footer.SetColumnSpan(saveCopy, 3);
                saveCopy.Margin = new Padding(0, 0, 0, 6);
            }

            Location = WindowLayoutPlan.Center(area, Size);
        }

        Label Caption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = Theme.Caption,
                Margin = new Padding(0, 0, 0, 6)
            };
        }

        Control PathField(string text, int height)
        {
            var field = new DarkTextBox
            {
                Text = text,
                ReadOnly = true,
                Multiline = true,
                Dock = DockStyle.Top,
                Height = height,
                BackColor = Theme.Input,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 12),
                ScrollBars = ScrollBars.None
            };
            tips.SetToolTip(field, text);
            return field;
        }

        Button ChoiceButton(string text, ExistingInstallerChoice choice)
        {
            var button = new SoftButton
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill
            };
            button.Click += delegate
            {
                Choice = choice;
                DialogResult = choice == ExistingInstallerChoice.Cancel ? DialogResult.Cancel : DialogResult.OK;
                Close();
            };
            return button;
        }

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    }
}
