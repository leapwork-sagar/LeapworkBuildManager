using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        void Changed()
        {
            if (restoring)
                return;
            if (preferencesLoaded)
                controller.Reset();
            linkExpanded = false;
            url.Clear();
            matches.Items.Clear();
            ClearProgress();
            SetDownloadPanel(false);
            SetDestination(null, false);
            status.Text = "Ready.";
            string error;
            bool valid = BuildService.Validate(build.Text, out error);
            restoring = true;
            if (valid)
            {
                inferredRuntime = BuildService.IsModern(build.Text) ? 0 : 1;
                if (inferredRuntime == 1 && SelectedBuildType == "Pre-release")
                    SelectedBuildType = "Experimental";
            }

            restoring = false;
            UpdateBuildFeedback(false);
            resultSummary.Text = advancedMode ? "Check the selected build type to verify its download." : "Enter a build number, then select Find available builds.\r\nWe will check all build types for you.";
            check.Text = advancedMode ? "Check selected type" : "Find available builds";
            UpdateControls();
            if (preferencesLoaded)
                QueuePreferences();
        }

        void UpdateBuildFeedback(bool submitted)
        {
            var feedback = BuildInputFeedback.Evaluate(build.Text, submitted);
            validation.ForeColor = feedback.IsError ? Theme.Error : Theme.SecondaryText;
            validation.Text = feedback.Message;
            validation.AccessibleName = (feedback.IsError ? "Build number error: " : "Build number guidance: ") + feedback.Message;
            build.AccessibleDescription = feedback.Message.Length == 0 ? "Enter digits only. Dots are added automatically. Example: 20262257 becomes 2026.2.257." : feedback.Message;
            detailsTip.SetToolTip(validation, feedback.Message);
            if (submitted && feedback.IsError)
                Announce(validation);
        }

        async Task FindBuildsAsync()
        {
            UpdateBuildFeedback(true);
            string error;
            if (operation != null) return;
            if (!BuildService.Validate(build.Text, out error))
            {
                build.Focus();
                return;
            }
            int focusRevision = interactionRevision;
            linkExpanded = false;
            url.Clear();
            matches.Items.Clear();
            ClearProgress();
            controller.Reset();
            bool searchFinished = false;
            BeginDiagnostics("Search");
            var source = operations.Begin();
            appState = ApplicationState.Searching;
            int total = BuildKinds.Candidates(BuildService.IsModern(build.Text)).Length;
            int shownCompleted = 0;
            UpdateControls();
            resultSummary.Text = "Checking 0 of " + total + " build types…";
            try
            {
                var progress = new Progress<SearchProgressInfo>(update =>
                {
                    if (IsDisposed || !IsHandleCreated)
                        return;
                    BeginInvoke(new Action(() =>
                    {
                        if (searchFinished || !operations.Accepts(source))
                            return;
                        shownCompleted = Math.Max(shownCompleted, update.Completed);
                        AddSearchResult(update.Result);
                        resultSummary.Text = "Checked " + shownCompleted + " of " + update.Total + " build types · " + matches.Items.Count + " available";
                        status.Text = "Checking build types… Results appear as they become available.";
                        ReflowDownloadPanel();
                    }));
                });
                var results = await controller.SearchAsync(build.Text.Trim(), progress, source.Token);
                preferences.Remember(new RecentBuild { Build = build.Text.Trim(), Type = "All build types", Runtime = BuildService.IsModern(build.Text) ? 0 : 1 });
                RefreshHistory();
                SavePreferences();
                searchFinished = true;
                source.Token.ThrowIfCancellationRequested();
                int unverified = 0;
                var details = new System.Text.StringBuilder();
                foreach (var result in results)
                {
                    if (result.Available)
                        AddSearchResult(result);
                    else if (!result.Missing)
                    {
                        unverified++;
                        details.Append(result.DisplayKind + ": " + result.Detail + ". ");
                    }
                }

                appState = ApplicationState.Results;
                resultSummary.Text = build.Text.Trim() + " — " + matches.Items.Count + " available" + (unverified > 0 ? " · " + unverified + " could not be verified — retry search" : "");
                status.Text = unverified > 0 ? unverified + " checks failed. Retry Find, or open Help for details." : matches.Items.Count == 0 ? "No matching downloads found." : "Select an available build to download.";
                if (unverified > 0)
                    RecordDiagnostic(details.ToString());
            }
            catch (OperationCanceledException)
            {
                appState = ApplicationState.Cancelled;
                matches.Items.Clear();
                resultSummary.Text = "Search cancelled. You can retry.";
                status.Text = closing ? "Cancelling search and closing…" : "Cancelled.";
            }
            catch (Exception e)
            {
                appState = ApplicationState.Failed;
                resultSummary.Text = "Unable to search. Please retry.";
                RecordFailure(e);
            }
            finally
            {
                searchFinished = true;
                RecordDiagnostic("Search finished: " + status.Text);
                operations.Complete(source);
                UpdateControls();
                if (appState == ApplicationState.Results && matches.Items.Count == 1)
                    matches.SelectedIndex = 0;
                if (matches.Items.Count > 0)
                    FocusAfterOperation(matches, focusRevision);
                if (closing && !IsDisposed)
                    BeginInvoke(new Action(Close));
            }
        }

        void AddSearchResult(BuildMatch result)
        {
            if (result == null || !result.Available)
                return;
            foreach (BuildMatch existing in matches.Items)
                if (existing.BuildType == result.BuildType)
                    return;
            // Stable ordering even when network requests finish in a different order.
            var order = BuildKinds.Candidates(result.Modern);
            int index = 0;
            while (index < matches.Items.Count && Array.IndexOf(order, ((BuildMatch)matches.Items[index]).BuildType) < Array.IndexOf(order, result.BuildType))
                index++;
            matches.Items.Insert(index, result);
        }

        void ClearProgress()
        {
            bar.Style = ProgressBarStyle.Continuous;
            bar.Value = 0;
            percentage.Text = "0%";
            eta.Text = "";
        }

        void Generate()
        {
            controller.Prepare(build.Text, selectedBuildType);
            url.Text = current.AbsoluteUri;
        }

        void UpdateControls()
        {
            string error;
            bool valid = BuildService.Validate(build.Text, out error);
            var ui = UiModel;
            clearHistory.Enabled = build.Enabled = recent.Enabled = matches.Enabled = advanced.Enabled = reset.Enabled = !ui.Busy;
            if (type != null)
                type.Enabled = !ui.Busy;
            check.Enabled = valid && !ui.Busy || operation != null && !downloading;
            check.Text = ui.CheckText;
            download.Text = ui.DownloadText;
            download.Enabled = ui.ShowDownload && !ui.Busy;
            viewLink.Text = ui.LinkText;
            viewLink.Enabled = !ui.Busy;
            copy.Visible = ui.ShowCopy;
            copy.Enabled = ui.CanCopy;
            open.Enabled = current != null && !ui.Busy;
            cancel.Visible = operation != null;
            cancel.Enabled = operation != null && !operation.IsCancellationRequested;
            if (folder.ContainsFocus && !ui.CanOpenFolder)
                ActiveControl = ui.Busy ? (Control)cancel : build;
            folder.Enabled = ui.CanOpenFolder;
            folder.Size = ui.CanOpenFolder ? new Size(Px(132), Px(LayoutMetrics.ButtonHeight)) : Size.Empty;
            eta.Visible = !ui.Completed;
            UpdateActionTooltips();
            ReflowDownloadPanel();
        }

        void CancelOperation()
        {
            if (operation == null)
                return;
            ClearProgress();
            status.Text = closing ? "Cancelling operation and closing…" : "Cancelling…";
            operations.Cancel();
            UpdateControls();
        }

        void ResetState()
        {
            if (operation != null)
                return;
            restoring = true;
            build.Clear();
            SelectedBuildType = "Experimental";
            inferredRuntime = 0;
            restoring = false;
            var area = WorkingAreaProvider();
            RefreshHistory();
            Changed();
            // Finish the collapsed layout before centering so the final window stays centered.
            expandTimer.Stop();
            panelHeight = expansionTarget = 0;
            ReflowDownloadPanel();
            Location = WindowLayoutPlan.Center(area, Size);
            SavePreferences();
            build.Focus();
        }

        void ReportProgress(CancellationTokenSource source, DownloadProgressInfo progress)
        {
            if (!operations.Accepts(source) || controller.State != ApplicationState.Downloading)
                return;
            if (progress.Percentage.HasValue)
            {
                bar.Style = ProgressBarStyle.Continuous;
                bar.Value = (int)progress.Percentage.Value;
                percentage.Text = progress.Percentage.Value.ToString("F1") + "%";
            }
            else
            {
                bar.Style = ProgressBarStyle.Marquee;
                percentage.Text = "Size unknown";
            }

            if (progress.IsStalled)
            {
                eta.Text = "Waiting for data — time remaining unavailable";
                status.Text = "No data received for at least 30 seconds. Waiting for the connection…";
                return;
            }
            eta.Text = ProgressText.Remaining(progress);
            status.Text = String.Format("{0:F1} MB{1} · {2:F1} MB/s", progress.BytesReceived / 1048576.0, progress.TotalBytes.HasValue ? " of " + (progress.TotalBytes.Value / 1048576.0).ToString("F1") + " MB" : "", progress.BytesPerSecond / 1048576.0);
            TrackProgress(progress);
        }
    }
}
