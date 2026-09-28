using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed class HelpDialog : DpiAwareForm
    {
        readonly SoftButton helpTab = new SoftButton
        {
            Text = "Help",
            AutoSize = false
        }, diagnosticsTab = new SoftButton
        {
            Text = "Diagnostics",
            AutoSize = false
        };
        readonly SoftButton copy = new SoftButton
        {
            Text = "Copy diagnostics",
            AutoSize = false
        }, closeButton = new SoftButton
        {
            Text = "Close",
            AutoSize = false,
            DialogResult = DialogResult.Cancel
        };
        readonly Panel helpMarker = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 3,
            BackColor = Theme.Accent
        }, diagnosticsMarker = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 3,
            BackColor = Theme.Accent
        };
        readonly Panel helpPage = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        }, diagnosticsPage = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };
        readonly string buildValue, statusValue, fullUrl, report;
        readonly DiagnosticEvent[] sessionLog;
        readonly ToolTip tips = new ToolTip();
        RichTextBox log;
        TextBox link;
        SoftButton reveal;
        bool linkExpanded;
        float scale = 1;
        PictureBox logo;
        TableLayoutPanel rootLayout, footerLayout;
        Control brandHeader;
        public bool DiagnosticsCreated
        {
            get
            {
                return log != null;
            }
        }

        public bool DiagnosticsVisible
        {
            get
            {
                return diagnosticsPage.Visible;
            }
        }

        public bool CopyDiagnosticsVisible
        {
            get
            {
                return copy.Visible;
            }
        }

        public int SelectedLogCharacters
        {
            get
            {
                return log == null ? 0 : log.SelectionLength;
            }
        }

        public string DisplayedUrl
        {
            get
            {
                return link == null ? "" : link.Text;
            }
        }

        public bool FooterActionsFit
        {
            get
            {
                return Fits(closeButton) && (!copy.Visible || Fits(copy));
            }
        }

        bool Fits(Control button)
        {
            var bounds = RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
            return ClientRectangle.Contains(bounds) && button.Height >= Px(32);
        }

        int Px(int value)
        {
            return UiScale.Pixels(value, scale);
        }

        public HelpDialog(Image image, string build, string status, string url, DiagnosticEvent[] events, string diagnostics)
        {
            buildValue = build;
            statusValue = status;
            fullUrl = url;
            sessionLog = events ?? new DiagnosticEvent[0];
            report = diagnostics;
            Text = "About / Help";
            Font = Theme.CreateFont(10, FontStyle.Regular);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 660);
            var title = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36
            };
            var caption = new Label
            {
                Text = "About / Help",
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 8, 0, 0)
            };
            var exit = new SoftButton
            {
                Text = "×",
                SymbolOnly = true,
                Padding = Padding.Empty,
                Dock = DockStyle.Right,
                Width = 44,
                AutoSize = false,
                AccessibleName = "Close help"
            };
            exit.Click += delegate
            {
                Close();
            };
            caption.MouseDown += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                }
            };
            title.Controls.Add(caption);
            title.Controls.Add(exit);
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(LayoutMetrics.PanelPadding),
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            logo = new PictureBox
            {
                Image = image == null ? null : new Bitmap(image),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(46, 46),
                Margin = new Padding(0, 4, 0, 0)
            };
            header.Controls.Add(logo, 0, 0);
            header.SetRowSpan(logo, 2);
            header.Controls.Add(new Label { Text = "Leapwork Build Manager", Dock = DockStyle.Fill, AutoEllipsis = true, Font = Theme.CreateFont(17, FontStyle.Bold) }, 1, 0);
            header.Controls.Add(new Label { Text = VersionInfo.Display, ForeColor = Theme.SecondaryText, Dock = DockStyle.Fill }, 1, 1);
            rootLayout = root;
            brandHeader = header;
            root.Controls.Add(header, 0, 0);
            var tabs = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty
            };
            tabs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabs.Controls.Add(TabHost(helpTab, helpMarker), 0, 0);
            tabs.Controls.Add(TabHost(diagnosticsTab, diagnosticsMarker), 1, 0);
            root.Controls.Add(tabs, 0, 1);
            var pages = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                Padding = new Padding(LayoutMetrics.PanelPadding),
                Margin = Padding.Empty
            };
            pages.Controls.Add(helpPage);
            pages.Controls.Add(diagnosticsPage);
            root.Controls.Add(pages, 0, 2);
            // A separate footer row keeps actions outside scrolling content.
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(0, LayoutMetrics.PanelPadding, 0, 0),
                Margin = Padding.Empty
            };
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            footerLayout = footer;
            copy.Dock = closeButton.Dock = DockStyle.Top;
            copy.Height = closeButton.Height = LayoutMetrics.ButtonHeight;
            copy.Margin = closeButton.Margin = new Padding(LayoutMetrics.Gap, 0, 0, 0);
            footer.Controls.Add(copy, 1, 0);
            footer.Controls.Add(closeButton, 2, 0);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);
            Controls.Add(title);
            CancelButton = closeButton;
            closeButton.Click += delegate
            {
                Close();
            };
            var helpContent = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                Margin = Padding.Empty
            };
            helpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddHelp(helpContent, "How to download", true);
            AddHelp(helpContent, "1. Enter the build number. Dots are inserted automatically.", false);
            AddHelp(helpContent, "2. Find available builds and select a result.", false);
            AddHelp(helpContent, "3. Download and choose where to save the installer.", false);
            AddHelp(helpContent, "Advanced checks a specific build type. A successful check is required before downloading.", false);
            AddHelp(helpContent, "Keyboard shortcuts", true);
            var shortcuts = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2
            };
            shortcuts.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            shortcuts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            string[, ] rows =
            {
                {
                    "Tab / Shift+Tab",
                    "Move between controls"
                },
                {
                    "↑ / ↓",
                    "Select a result"
                },
                {
                    "Enter",
                    "Activate the focused action"
                },
                {
                    "Escape",
                    "Cancel an operation / close Help"
                },
                {
                    "F1",
                    "Open Help"
                }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                shortcuts.Controls.Add(Cell(rows[i, 0]), 0, i);
                shortcuts.Controls.Add(Cell(rows[i, 1]), 1, i);
            }

            helpContent.Controls.Add(shortcuts);
            AddHelp(helpContent, "Troubleshooting", true);
            AddHelp(helpContent, "Retry after checking your connection. Interrupted downloads restart; partial-file resume is not supported. Windows notification settings may suppress completion notices.", false);
            helpPage.Controls.Add(helpContent);
            helpTab.Click += delegate
            {
                SelectHelp();
            };
            diagnosticsTab.Click += delegate
            {
                SelectDiagnostics();
            };
            helpTab.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Right)
                {
                    SelectDiagnostics();
                    e.Handled = true;
                }
            };
            diagnosticsTab.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left)
                {
                    SelectHelp();
                    helpTab.Focus();
                    e.Handled = true;
                }
            };
            copy.Click += delegate
            {
                var choice = MessageBox.Show(this, "Hide local folder paths in the diagnostic report? URL credentials and query values are always removed.", "Diagnostic privacy", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                if (choice != DialogResult.Cancel)
                    CopyText(DiagnosticPrivacy.Redact(report, choice == DialogResult.Yes), copy);
            };
            using (var graphics = CreateGraphics())
                scale = graphics.DpiX / 96f;
            if (scale != 1)
                UiScale.ApplyTree(this, scale);
            Shown += delegate
            {
                FitToArea(Screen.FromControl(this).WorkingArea);
                SelectHelp();
                helpTab.Focus();
            };
            FormClosed += delegate
            {
                if (logo.Image != null)
                    logo.Image.Dispose();
                tips.Dispose();
            };
            SelectHelp();
        }

        protected override float LayoutScale { get { return scale; } }
        protected override void OnDisplayScaleChanged(float value)
        {
            scale = value;
            FitToArea(Screen.FromRectangle(Bounds).WorkingArea);
        }

        public void FitToArea(Rectangle area)
        {
            SuspendLayout();
            Size = new Size(Math.Min(Px(640), area.Width), Math.Min(Px(660), area.Height));
            bool showBrand = Height >= Px(332);
            brandHeader.Visible = showBrand;
            rootLayout.RowStyles[0].Height = showBrand ? Px(76) : 0;
            bool narrow = Width - Px(32) < Px(286);
            footerLayout.ColumnStyles[0].SizeType = narrow ? SizeType.Absolute : SizeType.Percent;
            footerLayout.ColumnStyles[0].Width = narrow ? 0 : 100;
            footerLayout.ColumnStyles[1].SizeType = narrow ? SizeType.Percent : SizeType.Absolute;
            footerLayout.ColumnStyles[1].Width = narrow ? 65 : Px(178);
            footerLayout.ColumnStyles[2].SizeType = narrow ? SizeType.Percent : SizeType.Absolute;
            footerLayout.ColumnStyles[2].Width = narrow ? 35 : Px(108);
            ResumeLayout(true);
            Location = WindowLayoutPlan.Center(area, Size);
        }

        static Panel TabHost(Button button, Panel marker)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, LayoutMetrics.Gap, 6)
            };
            button.Dock = DockStyle.Fill;
            button.AccessibleRole = AccessibleRole.PageTab;
            panel.Controls.Add(button);
            panel.Controls.Add(marker);
            return panel;
        }

        static Label Cell(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 6)
            };
        }

        static void AddHelp(TableLayoutPanel panel, string text, bool heading)
        {
            panel.Controls.Add(new Label { Text = text, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, heading ? 10 : 3, 0, 8), Font = Theme.CreateFont(heading ? 11 : 10, heading ? FontStyle.Bold : FontStyle.Regular) });
        }

        TextBox Field(string text, bool multiline)
        {
            return new TextBox
            {
                Text = text,
                ReadOnly = true,
                Multiline = multiline,
                ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                Dock = DockStyle.Fill,
                BackColor = Theme.Input,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, Px(6))
            };
        }

        void EnsureDiagnostics()
        {
            if (log != null)
                return;
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = Px(300),
                ColumnCount = 1,
                RowCount = 6,
                Margin = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int[] heights =
            {
                74,
                24,
                36,
                42,
                28
            };
            foreach (int height in heights)
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, Px(height)));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Px(74)));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            fields.Controls.Add(Cell("Build"), 0, 0);
            fields.Controls.Add(Field(String.IsNullOrWhiteSpace(buildValue) ? "No build selected" : buildValue, false), 1, 0);
            fields.Controls.Add(Cell("Status"), 0, 1);
            fields.Controls.Add(Field(statusValue, false), 1, 1);
            content.Controls.Add(fields, 0, 0);
            var linkHeading = new Label { Text = "Download link", Font = Theme.CreateFont(11, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
            content.Controls.Add(linkHeading, 0, 1);
            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                Margin = Padding.Empty
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Px(140)));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Px(140)));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            reveal = new SoftButton
            {
                Text = "Show full URL",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, Px(LayoutMetrics.Gap), Px(LayoutMetrics.Gap)),
                Enabled = !String.IsNullOrEmpty(fullUrl)
            };
            var copyUrl = new SoftButton
            {
                Text = "Copy URL",
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, Px(LayoutMetrics.Gap)),
                Enabled = !String.IsNullOrEmpty(fullUrl)
            };
            tips.SetToolTip(reveal, "Expand the complete download address.");
            tips.SetToolTip(copyUrl, "Copy the complete download address, even when only the filename is shown.");
            actions.Controls.Add(reveal, 0, 0);
            actions.Controls.Add(copyUrl, 1, 0);
            content.Controls.Add(actions, 0, 2);
            link = Field(ShortUrl(fullUrl), true);
            link.AccessibleName = "Download URL";
            tips.SetToolTip(link, fullUrl);
            content.Controls.Add(link, 0, 3);
            reveal.Click += delegate
            {
                ToggleUrl();
            };
            copyUrl.Click += delegate
            {
                CopyText(fullUrl, copyUrl);
            };
            content.Controls.Add(new Label { Text = "Session log", Font = Theme.CreateFont(11, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 4);
            log = new RichTextBox
            {
                Font = Font,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = Theme.Input,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                AccessibleName = "Session diagnostic log",
                Margin = Padding.Empty
            };
            if (sessionLog.Length == 0)
                log.Text = "No diagnostic events in this session.";
            else
                foreach (var item in sessionLog)
                {
                    string timestamp = item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    int start = log.TextLength;
                    log.AppendText(item.DisplayText(item.Url == null ? "" : ShortUrl(item.Url.AbsoluteUri)));
                    log.Select(start, timestamp.Length);
                    log.SelectionColor = Theme.Accent;
                    using (var bold = Theme.CreateFont(10, FontStyle.Bold))
                        log.SelectionFont = bold;
                    log.Select(log.TextLength, 0);
                    log.SelectionColor = Theme.Text;
                    log.SelectionFont = …887 tokens truncated…rtUrl(fullUrl);
            reveal.Text = linkExpanded ? "Hide full URL" : "Show full URL";
        }

        void CopyText(string text, Button action)
        {
            try
            {
                Clipboard.SetText(text);
                action.Text = "Copied";
            }
            catch (ExternalException)
            {
                action.Text = "Copy failed";
            }
        }

        public void SelectHelp()
        {
            helpPage.Visible = true;
            diagnosticsPage.Visible = false;
            copy.Visible = false;
            helpMarker.Visible = true;
            diagnosticsMarker.Visible = false;
            helpTab.BackColor = Theme.Selection;
            diagnosticsTab.BackColor = Theme.Button;
            helpTab.AccessibleDescription = "Selected tab";
            diagnosticsTab.AccessibleDescription = "Diagnostics tab";
        }

        public void SelectDiagnostics()
        {
            EnsureDiagnostics();
            helpPage.Visible = false;
            diagnosticsPage.Visible = true;
            copy.Visible = true;
            helpMarker.Visible = false;
            diagnosticsMarker.Visible = true;
            helpTab.BackColor = Theme.Button;
            diagnosticsTab.BackColor = Theme.Selection;
            diagnosticsTab.AccessibleDescription = "Selected tab";
            helpTab.AccessibleDescription = "Help tab";
            log.Select(0, 0);
            diagnosticsTab.Focus();
        }

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, IntPtr text);
    }
}
