using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        bool resourcesDisposed;
        protected override void Dispose(bool disposing)
        {
            // Direct disposal must release the same resources as normal window closure.
            if (disposing)
                DisposeOwnedResources();
            base.Dispose(disposing);
        }

        void OnApplicationClosed(object sender, FormClosedEventArgs e)
        {
            DisposeOwnedResources();
        }

        void DisposeOwnedResources()
        {
            if (resourcesDisposed)
                return;
            resourcesDisposed = true;
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            settingsSaveTimer.Stop();
            settingsSaveTimer.Dispose();
            expandTimer.Stop();
            expandTimer.Dispose();
            notification.Visible = false;
            notification.Dispose();
            operations.Dispose();
            if (service != null)
            {
                service.Diagnostic = null;
                service.DiagnosticWithUrl = null;
                service.CleanupWarning = null;
                service.Dispose();
            }

            if (brandLogo != null && brandLogo.Image != null)
            {
                brandLogo.Image.Dispose();
                brandLogo.Image = null;
            }

            if (Icon != null)
            {
                Icon.Dispose();
                Icon = null;
            }

            detailsTip.Dispose();
            if (persistentLog != null)
                persistentLog.Dispose();
        }

        DiagnosticContext diagnosticContext;
        void ConfigureServiceDiagnostics()
        {
            service.DiagnosticWithUrl = (message, error, address) =>
            {
                var context = diagnosticContext ?? new DiagnosticContext("", "", null);
                PublishDiagnostic(new DiagnosticEvent(DateTime.Now, message, context.Build, address ?? context.Url, error == null ? "Information" : "Warning", "Network", context.OperationId, error));
            };
            // Cleanup failures already use the shared diagnostic callback.
            service.CleanupWarning = null;
        }

        void PublishDiagnostic(DiagnosticEvent item)
        {
            if (resourcesDisposed)
                return;
            if (persistentLog != null)
                persistentLog.Write(DiagnosticEvent.FormatReport(new[] { item }));
            Action add = () =>
            {
                if (resourcesDisposed || IsDisposed)
                    return;
                diagnostics.Add(item);
                if (diagnostics.Count > OperationalSettings.DiagnosticCapacity)
                    diagnostics.RemoveAt(0);
            };
            try
            {
                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                        BeginInvoke(add);
                }
                else
                    add();
            }
            catch (InvalidOperationException)
            {
            }
        }

        DiagnosticLog persistentLog;
        string diagnosticOperation = "", diagnosticCategory = "Application";
        long receivedBytes;
        int loggedMilestone, announcedMilestone, loggedMinute;
        readonly System.Diagnostics.Stopwatch transferAge = new System.Diagnostics.Stopwatch();
        public Func<bool> ConfirmCancelDownload { get; set; }

        void BeginDiagnostics(string category)
        {
            diagnosticOperation = Guid.NewGuid().ToString("N");
            diagnosticCategory = category;
            diagnosticContext = new DiagnosticContext(diagnosticOperation, build.Text, current);
            receivedBytes = 0;
            loggedMilestone = announcedMilestone = loggedMinute = 0;
            transferAge.Reset();
            if (category == "Download")
                transferAge.Start();
            RecordDiagnostic(category + " started");
        }

        void RecordFailure(Exception error)
        {
            var failure = FailureInfo.From(error);
            RecordDiagnostic(failure.Message, "Error", failure.Category, error);
            status.Text = failure.Message;
        }

        async Task RunUiAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception error)
            {
                if (IsDisposed)
                    return;
                var source = operation;
                if (source != null)
                {
                    operations.Cancel();
                    operations.Complete(source);
                }

                if (controller.State != ApplicationState.Loading && controller.State != ApplicationState.Completed)
                    controller.Transition(ApplicationState.Failed);
                if (controller.State == ApplicationState.Loading)
                {
                    controller.Reset();
                    preferencesLoaded = true;
                    settingsWriter = new SettingsWriter(preferencesPath, preferences);
                }

                RecordFailure(error);
                ClearProgress();
                UpdateControls();
            }
        }

        bool RequestCancellation(CancellationReason reason)
        {
            if (operation == null || operation.IsCancellationRequested)
                return true;
            var requestedOperation = operation;
            if (CancellationPolicy.NeedsConfirmation(downloading, receivedBytes, transferAge.Elapsed.TotalSeconds, reason))
            {
                bool accepted = reason == CancellationReason.WindowClosing ? ConfirmCloseDownload() : ConfirmCancelDownload();
                if (!accepted)
                {
                    RecordDiagnostic("Cancellation declined: " + reason);
                    return false;
                }
            }

            if (!ReferenceEquals(operation, requestedOperation))
                return true;
            RecordDiagnostic("Cancellation requested: " + reason, "Information", "Cancellation", null);
            // Cancellation callbacks can complete synchronously; set closing first.
            if (reason == CancellationReason.WindowClosing)
                closing = true;
            CancelOperation();
            return true;
        }

        void TrackProgress(DownloadProgressInfo progress)
        {
            receivedBytes = progress.BytesReceived;
            if (!progress.Percentage.HasValue && (int)transferAge.Elapsed.TotalMinutes > loggedMinute)
            {
                loggedMinute = (int)transferAge.Elapsed.TotalMinutes;
                RecordDiagnostic("Download progress: " + receivedBytes + " bytes; size unknown");
                Announce(status);
            }

            int milestone = progress.Percentage.HasValue ? Math.Min(9, (int)progress.Percentage.Value / OperationalSettings.ProgressMilestonePercent) : 0;
            if (milestone > loggedMilestone)
            {
                loggedMilestone = milestone;
                RecordDiagnostic("Download progress: " + milestone * OperationalSettings.ProgressMilestonePercent + "% (" + receivedBytes + " bytes)");
            }

            if (milestone > announcedMilestone)
            {
                announcedMilestone = milestone;
                Announce(percentage);
            }
        }
    }
}
