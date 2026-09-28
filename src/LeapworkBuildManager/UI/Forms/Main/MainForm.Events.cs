using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        void OnClearHistory(object sender, EventArgs e)
        {
            if (IsBusy)
                return;
            preferences.History.Clear();
            RefreshHistory();
            SavePreferences();
            ReflowDownloadPanel();
        }

        void OnRecentFormat(object sender, ListControlConvertEventArgs e)
        {
            var entry = e.ListItem as RecentBuild;
            if (entry != null)
                e.Value = HistoryFormatter.Format(entry);
        }

        void OnAdvancedClick(object sender, EventArgs e)
        {
            if (operation != null)
                return;
            advancedMode = !advancedMode;
            EnsureAdvancedControls();
            type.Visible = typeLabel.Visible = advancedMode;
            advanced.Text = advancedMode ? "▾ Advanced: manual build type" : "▸ Advanced: choose build type";
            Changed();
            ReflowDownloadPanel();
        }

        void OnBuildSelected(object sender, EventArgs e)
        {
            var choice = matches.SelectedItem as BuildMatch;
            if (choice == null || operation != null)
                return;
            restoring = true;
            SelectedBuildType = choice.DisplayKind;
            inferredRuntime = choice.Modern ? 0 : 1;
            restoring = false;
            controller.Select(choice);
            linkExpanded = false;
            url.Text = current.AbsoluteUri;
            ClearProgress();
            SetDownloadPanel(false);
            status.Text = "Selected: " + choice.DisplayKind + " · " + build.Text.Trim() + ". Ready to download.";
            RememberBuild(null);
            SavePreferences();
            UpdateControls();
        }

        async void OnRecentSelected(object sender, EventArgs e)
        {
            UpdateActionTooltips();
            if (restoring || !(recent.SelectedItem is RecentBuild))
                return;
            var entry = (RecentBuild)recent.SelectedItem;
            if (!String.IsNullOrEmpty(entry.DownloadPath))
            {
                using (var dialog = new SavedInstallerDialog(entry))
                {
                    if (dialog.ShowDialog(this) != DialogResult.Retry)
                    {
                        RefreshHistory();
                        if (recent.CanFocus) recent.Focus();
                        return;
                    }
                }
            }
            restoring = true;
            build.Text = entry.Build;
            if (!String.IsNullOrEmpty(entry.DownloadPath)) SelectedBuildType = entry.Type;
            restoring = false;
            Changed();
            if (!advancedMode)
                await RunUiAsync(FindBuildsAsync);
            else
                await RunUiAsync(CheckBuildAsync);
        }

        async void OnCheckClick(object sender, EventArgs e)
        {
            if (operation != null && !downloading)
            {
                RequestCancellation(CancellationReason.CancelButton);
                return;
            }

            if (advancedMode)
                await RunUiAsync(CheckBuildAsync);
            else
                await RunUiAsync(FindBuildsAsync);
        }

        void OnCopyLink(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(current.AbsoluteUri);
                status.Text = "Link copied.";
            }
            catch (Exception error)
            {
                status.Text = "Could not copy: " + error.Message;
            }
        }

        void OnOpenLink(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(current.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception error)
            {
                status.Text = "Could not open browser: " + error.Message;
            }
        }

        void OnOpenFolder(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(completedPath))
                    throw new IOException("The downloaded file has been moved or removed.");
                WindowsFileActions.OpenFolder(completedPath);
            }
            catch (Exception error)
            {
                status.Text = "Could not open folder: " + error.Message;
            }
        }

        void OnOperationClosing(object sender, FormClosingEventArgs e)
        {
            if (operation == null)
                return;
            if (!closing && !RequestCancellation(CancellationReason.WindowClosing))
            {
                e.Cancel = true;
                return;
            }

            if (operation == null)
                return;
            closing = true;
            e.Cancel = true;
            if (!downloading)
            {
                status.Text = appState == ApplicationState.Preparing ? "Cancelling destination check and closing…" : "Cancelling search and closing…";
                resultSummary.Text = status.Text;
                RecordDiagnostic(status.Text);
            }
        }
    }
}
