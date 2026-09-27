using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        async Task CheckBuildAsync()
        {
            UpdateBuildFeedback(true);
            string validationError;
            if (operation != null) return;
            if (!BuildService.Validate(build.Text, out validationError))
            {
                build.Focus();
                return;
            }
            int focusRevision = interactionRevision;
            Generate();
            var source = StartOperation(ApplicationState.Checking);
            try
            {
                status.Text = "Checking build availability…";
                var result = await controller.CheckAsync(build.Text.Trim(), selectedBuildType, source.Token);
                status.Text = result.Detail;
            }
            catch (OperationCanceledException)
            {
                status.Text = closing ? "Cancelling search and closing…" : "Check cancelled.";
            }
            catch (Exception error)
            {
                RecordFailure(error);
            }
            finally
            {
                FinishOperation(source, false);
                if (controller.IsVerified) FocusAfterOperation(download, focusRevision);
            }
        }

        async Task ChooseDownloadAsync()
        {
            if (operation != null || !controller.IsVerified)
                return;
            string destination;
            using (var picker = new SaveFileDialog
            {
                Filter = "Windows Installer (*.msi)|*.msi",
                FileName = Path.GetFileName(current.LocalPath),
                InitialDirectory = preferences.DownloadFolder ?? "",
                OverwritePrompt = false
            }

            )
            {
                if (picker.ShowDialog(this) != DialogResult.OK)
                    return;
                destination = picker.FileName;
            }

            await PrepareAndDownloadAsync(destination);
        }

        async Task PrepareAndDownloadAsync(string destination)
        {
            if (operation != null || !controller.IsVerified)
                return;
            var previous = appState == ApplicationState.Completed ? ApplicationState.Completed : ApplicationState.Ready;
            BeginDiagnostics("Preparation");
            var source = operations.Begin();
            appState = ApplicationState.Preparing;
            status.Text = "Checking destination…";
            UpdateControls();
            bool ready = false, replaceExisting = false;
            string inspectedDestination = destination;
            try
            {
                var preparation = await service.Preparation.InspectAsync(destination, controller.Selected.SizeBytes, source.Token);
                source.Token.ThrowIfCancellationRequested();
                if (preparation.Exists)
                {
                    using (var dialog = new ExistingInstallerDialog(destination))
                    {
                        dialog.ShowDialog(this);
                        switch (dialog.Choice)
                        {
                            case ExistingInstallerChoice.OpenFolder:
                                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + destination + "\"");
                                return;
                            case ExistingInstallerChoice.SaveCopy:
                                destination = await service.Preparation.CopyDestinationAsync(destination, source.Token);
                                break;
                            case ExistingInstallerChoice.Replace:
                                replaceExisting = true;
                                break;
                            default:
                                return;
                        }
                    }
                }

                if (!String.Equals(destination, inspectedDestination, StringComparison.OrdinalIgnoreCase))
                    preparation = await service.Preparation.InspectAsync(destination, controller.Selected.SizeBytes, source.Token);
                source.Token.ThrowIfCancellationRequested();
                if (!preparation.HasEnoughSpace)
                {
                    status.Text = preparation.Message;
                    RecordDiagnostic(preparation.Message, "Warning", "DiskSpace", null);
                    MessageBox.Show(this, preparation.Message, "Not enough disk space", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!preparation.SpaceVerified && MessageBox.Show(this, preparation.Message + "\nContinue downloading?", "Disk space could not be verified", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
                source.Token.ThrowIfCancellationRequested();
                preferences.DownloadFolder = Path.GetDirectoryName(destination);
                ready = true;
            }
            catch (OperationCanceledException)
            {
                status.Text = "Destination check cancelled.";
            }
            catch (Exception error)
            {
                RecordFailure(error);
            }
            finally
            {
                operations.Complete(source);
                appState = previous;
                if (status.Text == "Checking destination…")
                    status.Text = "Ready to download.";
                UpdateControls();
                if (closing && !IsDisposed)
                    BeginInvoke(new Action(Close));
            }

            if (ready && !closing)
                await DownloadPreparedBuildAsync(destination, replaceExisting);
        }

        Task DownloadBuildAsync(string destination, bool replaceExisting)
        {
            return TransferBuildAsync(destination, replaceExisting, false);
        }

        Task DownloadPreparedBuildAsync(string destination, bool replaceExisting)
        {
            return TransferBuildAsync(destination, replaceExisting, true);
        }

        async Task TransferBuildAsync(string destination, bool replaceExisting, bool spaceChecked)
        {
            if (operation != null || !controller.IsVerified)
                return;
            int focusRevision = interactionRevision;
            var source = StartOperation(ApplicationState.Downloading);
            bool success = false;
            try
            {
                downloadIdentity.Text = controller.Selected.BuildNumber + " · " + controller.Selected.DisplayKind;
                SetDestination(destination, false);
                SetDownloadPanel(true);
                status.Text = "Starting download…";
                eta.Text = "Estimating time remaining…";
                var progress = new Progress<DownloadProgressInfo>(p => ReportProgress(source, p));
                var result = await controller.DownloadAsync(destination, progress, source.Token, replaceExisting, spaceChecked);
                success = true;
                RememberBuild(destination);
                SavePreferences();
                bool settingsSaved = await TryFlushPreferencesAsync();
                status.Text = result.Summary + (settingsSaved ? "" : " History could not be saved.");
                bar.Style = ProgressBarStyle.Continuous;
                bar.Value = 100;
                percentage.Text = "100%";
                eta.Text = "";
                SetDestination(result.Destination, true);
                SetDownloadPanel(true);
                if (WindowState == FormWindowState.Minimized && CompletionNotice != null)
                    CompletionNotice(controller.Selected.BuildNumber + " — " + controller.Selected.DisplayKind);
            }
            catch (OperationCanceledException error)
            {
                if (!source.IsCancellationRequested)
                    RecordDiagnostic("Download timeout", "Warning", "Timeout", error);
                status.Text = controller.InterruptedBySleep ? "Download interrupted by sleep. Select Retry download to restart." : source.IsCancellationRequested ? "Cancelled." : "No data arrived before the timeout. Retry download to restart from the beginning.";
            }
            catch (Exception error)
            {
                RecordFailure(error);
            }
            finally
            {
                FinishOperation(source, success);
                if (success) FocusAfterOperation(folder, focusRevision);
            }
        }

        CancellationTokenSource StartOperation(ApplicationState state)
        {
            BeginDiagnostics(state == ApplicationState.Downloading ? "Download" : "Check");
            RememberBuild(null);
            SavePreferences();
            var source = operations.Begin();
            appState = state;
            ClearProgress();
            bar.Style = ProgressBarStyle.Marquee;
            UpdateControls();
            return source;
        }

        void FinishOperation(CancellationTokenSource source, bool completed)
        {
            RecordDiagnostic(status.Text);
            operations.Complete(source);
            if (!completed)
                ClearProgress();
            bar.Style = ProgressBarStyle.Continuous;
            UpdateControls();
            if (closing && !IsDisposed)
                BeginInvoke(new Action(Close));
        }
    }
}
