using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    internal sealed class SavedInstallerDialog : DpiAwareForm
    {
        readonly string path;
        readonly TextBox errorDetails = new TextBox { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Visible = false };
        readonly LinkLabel showDetails = new LinkLabel { Text = "Show error details", AutoSize = true, Visible = false };
        readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill };
        readonly Button open = new SoftButton { Text = "Open folder", AutoSize = true, Enabled = false };
        readonly Button retry = new SoftButton { Text = "Retry check", AutoSize = true, Visible = false };
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        bool checking, resourcesDisposed;

        internal SavedInstallerDialog(RecentBuild entry)
        {
            path = entry.DownloadPath;
            Text = "Saved installer";
            Font = Theme.CreateFont(10, FontStyle.Regular);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(540, 340);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(LayoutMetrics.PanelPadding), ColumnCount = 1, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = entry.Build + " · " + entry.Type, AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
            layout.Controls.Add(new TextBox { Text = path, ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, BackColor = Theme.Input, ForeColor = Theme.Text, ScrollBars = ScrollBars.Vertical }, 0, 1);
            layout.Controls.Add(status, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoSize = true, Margin = Padding.Empty, Padding = new Padding(0, LayoutMetrics.Gap, 0, 0) };
            var again = new SoftButton { Text = "Download again", AutoSize = true, DialogResult = DialogResult.Retry };
            var close = new SoftButton { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
            actions.Controls.AddRange(new Control[] { open, retry, again, close });
            foreach (Control action in actions.Controls)
            {
                action.AutoSize = false;
                action.Size = new Size(TextRenderer.MeasureText(action.Text, action.Font).Width + action.Padding.Horizontal + 8, LayoutMetrics.ButtonHeight);
                action.Margin = new Padding(0, 0, LayoutMetrics.Gap, 0);
            }
            errorDetails.BackColor = Theme.Input;
            errorDetails.ForeColor = Theme.Text;
            showDetails.LinkColor = Theme.Text;
            showDetails.LinkClicked += delegate
            {
                errorDetails.Visible = !errorDetails.Visible;
                layout.RowStyles[4].Height = errorDetails.Visible ? 80 : 0;
                showDetails.Text = errorDetails.Visible ? "Hide error details" : "Show error details";
            };
            layout.Controls.Add(showDetails, 0, 3);
            layout.Controls.Add(errorDetails, 0, 4);
            layout.Controls.Add(actions, 0, 5);
            Controls.Add(layout);
            CancelButton = close;
            Shown += async delegate
            {
                var area = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                await RefreshAsync(false);
            };
            retry.Click += async delegate { await RefreshAsync(false); };
            open.Click += async delegate { await RefreshAsync(true); };
            FormClosing += delegate { cancellation.Cancel(); };
        }

        async Task RefreshAsync(bool openFolder)
        {
            if (checking)
                return;
            checking = true;
            open.Enabled = retry.Enabled = false;
            status.Text = "Checking the saved location…";
            showDetails.Visible = false;
            errorDetails.Visible = false;
            ((TableLayoutPanel)errorDetails.Parent).RowStyles[4].Height = 0;
            showDetails.Text = "Show error details";
            try
            {
                var result = await SavedInstallerService.InspectAsync(path, cancellation.Token);
                if (IsDisposed || cancellation.IsCancellationRequested)
                    return;
                open.Enabled = result == SavedInstallerState.Available;
                retry.Visible = result == SavedInstallerState.Inaccessible;
                status.Text = DescribeInstallerState(result);
                if (openFolder && result == SavedInstallerState.Available)
                    WindowsFileActions.OpenFolder(path);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed)
                {
                    status.Text = "Couldn’t open or check the saved location. Retry, or view error details.";
                    errorDetails.Text = error.Message;
                    showDetails.Visible = true;
                    retry.Visible = true;
                }
            }
            finally
            {
                checking = false;
                if (!IsDisposed)
                    retry.Enabled = true;
            }
        }

        static string DescribeInstallerState(SavedInstallerState state)
        {
            switch (state)
            {
                case SavedInstallerState.Available:
                    return "Installer found at its saved location.";
                case SavedInstallerState.Missing:
                    return "Installer not found at its saved location. It may have been moved or deleted.";
                default:
                    return "Couldn’t check the saved location. Reconnect the drive or check permissions, then retry.";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                cancellation.Cancel();
                cancellation.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
