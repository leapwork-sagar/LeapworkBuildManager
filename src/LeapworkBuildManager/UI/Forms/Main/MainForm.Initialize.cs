using System.IO;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        public MainForm(string settingsPath, BuildService buildService)
        {
            SuspendLayout();
            canvas.SuspendLayout();
            titleBar.SuspendLayout();
            viewport.SuspendLayout();
            viewport.Controls.Add(canvas);
            Controls.Add(viewport);
            var startupArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            WorkingAreaProvider = () => startupCentered ? Screen.FromControl(this).WorkingArea : startupArea;
            service = buildService;
            controller = new BuildController(service);
            preferencesPath = settingsPath;
            persistentLog = new DiagnosticLog(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath)), "Logs"));
            ConfirmCancelDownload = () => MessageBox.Show(this, "Cancel this download? Downloaded progress will be lost. Retrying starts from the beginning.", "Cancel download", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            preferences = new Preferences();
            ConfigureServiceDiagnostics();
            Text = "Leapwork Build Manager " + VersionInfo.Display;
            ClientSize = new Size(620, 718);
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = WindowLayoutPlan.Center(startupArea, Size);
            Font = LauncherFont(10, FontStyle.Regular);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            var heading = CreateHeader();
            CreateBuildControls();
            CreateDownloadControls();
            CreateHistoryAndStatusControls();
            WireWindowBehavior(heading);
            ApplyInitialScaling();
            WireApplicationEvents();
            ConfigureInteraction();
            ConfigureSettingsSaves();
            AcceptButton = check;
            RefreshHistory();
            layoutReady = true;
            Changed();
            canvas.ResumeLayout(false);
            titleBar.ResumeLayout(false);
            viewport.ResumeLayout(false);
            ResumeLayout(false);
        }

        Label CreateHeader()
        {
            titleBar.BackColor = BackColor;
            Place(this, titleBar, 0, 0, 620, 34);
            titleCaption.Text = "Leapwork Build Manager";
            titleCaption.Font = LauncherFont(8, FontStyle.Regular);
            titleCaption.AutoEllipsis = true;
            Place(titleBar, titleCaption, 16, 8, 370, 22);
            minimize.Text = "—";
            close.Text = "×";
            Place(titleBar, minimize, 534, 2, 40, 30);
            Place(titleBar, close, 578, 2, 40, 30);
            minimize.AccessibleName = "Minimize";
            close.AccessibleName = "Close";
            minimize.Click += delegate
            {
                WindowState = FormWindowState.Minimized;
            };
            close.Click += delegate
            {
                Close();
            };
            brandLogo = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom
            };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LeapworkLogo"))
            {
                if (stream != null)
                    using (var original = Image.FromStream(stream))
                        brandLogo.Image = new Bitmap(original);
            }

            Place(canvas, brandLogo, 24, 52, 46, 46);
            brandLogo.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            var heading = new Label
            {
                Text = "Leapwork Build Manager",
                Font = LauncherFont(18, FontStyle.Bold)
            };
            Place(canvas, heading, 82, 49, 514, 38);
            Place(canvas, new Label { Text = "Find your build. Download and get started.", ForeColor = Theme.Subtitle }, 84, 89, 510, 24);
            return heading;
        }

        void CreateBuildControls()
        {
            Place(canvas, details, 24, 124, 572, 158);
            Place(details, Caption("Build number"), 16, 10, 280, 22);
            Place(details, build, 16, 38, 294, 27);
            build.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            build.BorderStyle = BorderStyle.FixedSingle;
            check.Text = "Find available builds";
            Place(details, check, 322, 32, 234, 39);
            check.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            validation.Font = LauncherFont(9, FontStyle.Regular);
            validation.AutoEllipsis = true;
            Place(details, validation, 16, 71, 540, 23);
            advanced.Text = "▸ Advanced: choose build type";
            Place(details, advanced, 16, 110, 426, 32);
            advanced.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            reset.Text = "Reset";
            Place(details, reset, 454, 110, 102, 32);
            reset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            inferredRuntime = 0;
        }

        void CreateDownloadControls()
        {
            Place(canvas, resultsCard, 24, 264, 572, 154);
            Place(resultsCard, resultSummary, 16, 12, 540, 24);
            resultSummary.Text = "Enter a build number to find available downloads.";
            matches.BackColor = Theme.Surface;
            matches.ForeColor = ForeColor;
            matches.BorderStyle = BorderStyle.None;
            matches.IntegralHeight = false;
            matches.ItemHeight = 36;
            matches.DrawMode = DrawMode.OwnerDrawFixed;
            matches.DrawItem += DrawBuildResult;
            Place(resultsCard, matches, 16, 40, 540, 110);
            download.Text = "Download selected build";
            download.BackColor = Theme.Accent;
            download.ForeColor = Theme.AccentText;
            Place(canvas, download, 24, 430, 572, 42);
            Place(canvas, linkCard, 24, 484, 572, 112);
            url.ReadOnly = true;
            url.Multiline = true;
            url.ScrollBars = ScrollBars.Vertical;
            url.BorderStyle = BorderStyle.FixedSingle;
            Place(linkCard, url, 16, 16, 540, 46);
            copy.Text = "Copy link";
            open.Text = "Open link";
            Place(linkCard, copy, 16, 74, 110, 32);
            copy.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Place(linkCard, open, 138, 74, 110, 32);
            open.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            idleStatus.AutoEllipsis = true;
            idleStatus.ForeColor = Theme.SecondaryText;
            Place(canvas, idleStatus, 24, 608, 572, 26);
            Place(canvas, progressCard, 24, 642, 572, 0);
            progressCard.Visible = false;
            percentage.Text = "0%";
            percentage.Font = LauncherFont(11, FontStyle.Bold);
            Place(progressCard, percentage, 16, 12, 350, 25);
            Place(progressCard, bar, 16, 44, 540, 7);
            Place(progressCard, status, 16, 60, 540, 25);
            status.AutoEllipsis = true;
            eta.ForeColor = Theme.SecondaryText;
            eta.AutoEllipsis = true;
            eta.TextChanged += delegate { detailsTip.SetToolTip(eta, eta.Text); };
            Place(progressCard, eta, 16, 88, 540, 24);
            cancel.Text = "Cancel";
            folder.Text = "Open folder";
            Place(progressCard, cancel, 424, 9, 132, 32);
            cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Place(progressCard, folder, 424, 9, 132, 32);
            folder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        }

        void CreateHistoryAndStatusControls()
        {
            Place(canvas, historyCard, 24, 642, 572, 60);
            historyCard.Padding = Padding.Empty;
            recent.IntegralHeight = false;
            recent.MaxDropDownItems = 8;
            recent.DropDownHeight = 200;
            Place(historyCard, recent, 16, 17, 540, 28);
            recent.DropDown += delegate
            {
                recent.DropDownWidth = historyCard.Width - Px(32);
            };
            Place(progressCard, downloadIdentity, 16, 115, 540, 28);
            downloadIdentity.AutoEllipsis = true;
            downloadIdentity.Font = Theme.CreateFont(11, FontStyle.Bold);
            downloadIdentity.AccessibleName = "Downloading build";
            Place(progressCard, destinationLabel, 16, 166, 540, 44);
            Place(progressCard, copyFolderPath, 416, 166, 140, 32);
            copyFolderPath.Visible = false;
            destinationLabel.ForeColor = Theme.SecondaryText;
            destinationLabel.AutoEllipsis = true;
            destinationLabel.AccessibleName = "Download destination";
            recent.Width = 400;
            recent.Top = 38;
            Place(historyCard, historyHeading, 16, 10, 400, 23);
            clearHistory.Text = "Clear history";
            Place(historyCard, clearHistory, 428, 34, 128, 36);
            clearHistory.Click += OnClearHistory;
            recent.Format += OnRecentFormat;
            recent.FormattingEnabled = true;
            foreach (var box in new[]
            {
                build,
                url
            }

            )
            {
                box.BackColor = Theme.Input;
                box.ForeColor = ForeColor;
            }

            foreach (var combo in new[]
            {
                recent
            }

            )
            {
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = Theme.Input;
                combo.ForeColor = ForeColor;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.ItemHeight = 23;
                combo.DrawItem += DrawCombo;
            }
        }

        void WireWindowBehavior(Label heading)
        {
            advanced.Click += OnAdvancedClick;
            matches.SelectedIndexChanged += OnBuildSelected;
            expandTimer.Tick += delegate
            {
                AdvancePanelAnimation();
            };
            MouseEventHandler drag = delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    userPositioned = true;
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, (IntPtr)2, null);
                }
            };
            titleBar.MouseDown += drag;
            foreach (Control child in titleBar.Controls)
                if (child is Label)
                    child.MouseDown += drag;
            heading.MouseDown += drag;
            help.Text = "About / Help";
            Place(titleBar, help, 410, 2, 116, 30);
            help.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            help.Click += delegate
            {
                ShowHelp();
            };
            viewLink.Text = "View download link";
            Place(canvas, viewLink, 24, 484, 572, 32);
            viewLink.Click += delegate
            {
                linkExpanded = !linkExpanded;
                UpdateControls();
            };
        }

        void ApplyInitialScaling()
        {
            foreach (Control control in canvas.Controls)
                control.Top -= 34;
            titleBar.BringToFront();
            using (var g = CreateGraphics())
                layoutScale = g.DpiX / 96f;
            if (layoutScale != 1)
            {
                ScaleTree(canvas, layoutScale);
                ScaleTree(titleBar, layoutScale);
            }

            Resize += delegate
            {
                if (layoutReady)
                {
                    viewport.SetBounds(0, (int)(34 * layoutScale), ClientSize.Width, Math.Max(1, ClientSize.Height - (int)(34 * layoutScale)));
                }
            };
            using (var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LeapworkIcon"))
            {
                if (iconStream != null)
                    Icon = new Icon(iconStream);
            }
        }

        void WireApplicationEvents()
        {
            SetupKeyboard();
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
            notification.Icon = Icon;
            notification.Text = "Leapwork Build Manager";
            notification.BalloonTipClicked += delegate
            {
                WindowState = FormWindowState.Normal;
                Show();
                Activate();
                notification.Visible = false;
            };
            CompletionNotice = ShowCompletionNotice;
            status.TextChanged += delegate
            {
                idleStatus.Text = status.Text;
                detailsTip.SetToolTip(idleStatus, status.Text);
                detailsTip.SetToolTip(status, status.Text);
            };
            resultSummary.TextChanged += delegate
            {
                detailsTip.SetToolTip(resultSummary, resultSummary.Text);
                if (preferencesLoaded) ReflowDownloadPanel();
            };
            Shown += async delegate
            {
                await RunUiAsync(LoadPreferencesAsync);
            };
            build.Leave += delegate
            {
                UpdateBuildFeedback(true);
            };
            build.Enter += delegate
            {
                UpdateBuildFeedback(false);
            };
            build.TextChanged += delegate
            {
                Changed();
            };
            recent.SelectedIndexChanged += OnRecentSelected;
            check.Click += OnCheckClick;
            download.Click += async delegate
            {
                await RunUiAsync(ChooseDownloadAsync);
            };
            cancel.Click += delegate
            {
                RequestCancellation(CancellationReason.CancelButton);
            };
            reset.Click += delegate
            {
                ResetState();
            };
            copy.Click += OnCopyLink;
            open.Click += OnOpenLink;
            folder.Click += OnOpenFolder;
            ConfirmCloseDownload = ShowCloseWarning;
            FormClosing += OnOperationClosing;
            FormClosed += OnApplicationClosed;
        }
    }
}
