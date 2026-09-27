using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    internal sealed class SavedInstallerDialog : Form
    {
        readonly string path;
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
            BackColor = Theme.Background; ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(540, 260);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.Controls.Add(new Label { Text = entry.Build + " · " + entry.Type, AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
            layout.Controls.Add(new TextBox { Text = path, ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, BackColor = Theme.Input, ForeColor = Theme.Text, ScrollBars = ScrollBars.Vertical }, 0, 1);
            layout.Controls.Add(status, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            var again = new SoftButton { Text = "Download again", AutoSize = true, DialogResult = DialogResult.Retry };
            var close = new SoftButton { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
            actions.Controls.AddRange(new Control[] { open, retry, again, close });
            layout.Controls.Add(actions, 0, 3); Controls.Add(layout);
            CancelButton = close;
            Shown += async delegate { await RefreshAsync(false); };
            retry.Click += async delegate { await RefreshAsync(false); };
            open.Click += async delegate { await RefreshAsync(true); };
            FormClosing += delegate { cancellation.Cancel(); };
        }

        async Task RefreshAsync(bool openFolder)
        {
            if (checking) return;
            checking = true; open.Enabled = retry.Enabled = false;
            status.Text = "Checking the saved location…";
            try
            {
                var result = await SavedInstallerService.InspectAsync(path, cancellation.Token);
                if (IsDisposed || cancellation.IsCancellationRequested) return;
                open.Enabled = result == SavedInstallerState.Available;
                retry.Visible = result == SavedInstallerState.Inaccessible;
                status.Text = result == SavedInstallerState.Available ? "Installer found at its saved location." :
                    result == SavedInstallerState.Missing ? "Installer not found at its saved location. It may have been moved or deleted." :
                    "Couldn’t check the saved location. Reconnect the drive or check permissions, then retry.";
                if (openFolder && result == SavedInstallerState.Available)
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsDisposed) { status.Text = "Couldn’t open or check the saved location: " + error.Message; retry.Visible = true; }
            }
            finally { checking = false; if (!IsDisposed) retry.Enabled = true; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                cancellation.Cancel(); cancellation.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
