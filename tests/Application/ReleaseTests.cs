using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LeapworkBuildManager;

static class ReleaseTests
{
    static int count;
    static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
        count++;
        Console.WriteLine("PASS " + message);
    }

    [STAThread]
    static void Main()
    {
        var raw = new BuildMatch
        {
            BuildNumber = "2026.2.257",
            Kind = "Release",
            Available = true,
            Url = new Uri("https://sawindowreleasedata.blob.core.windows.net/original.msi"),
            SizeBytes = 1024
        };
        using (var service = new BuildService())
        {
            var controller = new BuildController(service);
            controller.Reset();
            controller.Select(raw);
            raw.Url = new Uri("https://sawindowreleasedata.blob.core.windows.net/changed.msi");
            raw.Kind = "Custom";
            raw.SizeBytes = 1;
            Assert(controller.Selected.Url.AbsolutePath == "/original.msi" && controller.Selected.Kind == "Release" && controller.Selected.SizeBytes == 1024, "verified selection cannot be changed through discovery row");
            foreach (var property in typeof(VerifiedBuild).GetProperties())
                Assert(property.GetSetMethod() == null, "verified property has no public setter: " + property.Name);
            var ready = DownloadUiModel.Create(controller, false, false, true);
            Assert(ready.ShowDownload && ready.ShowLinkToggle && !ready.ShowLink && ready.CanCopy, "ready UI offers download and collapsed link");
            controller.Transition(ApplicationState.Downloading);
            var active = DownloadUiModel.Create(controller, true, false, true);
            Assert(active.Busy && !active.ShowDownload && !active.ShowLink && !active.ShowHistory, "active UI hides unrelated sections");
            controller.Transition(ApplicationState.Completed);
            var complete = DownloadUiModel.Create(controller, true, false, true);
            Assert(complete.DownloadText == "Download again" && !complete.ShowCopy && complete.ShowLink, "completed UI has repeat action and no copy button");
        }

        var progress = new DownloadProgressInfo(250, 1000, 50, 5);
        Assert(progress.Percentage == 25 && progress.TimeRemaining.Value.TotalSeconds == 15, "structured progress calculates percentage and ETA");
        Assert(ProgressText.Remaining(progress) == "About 15s remaining", "progress text retains seconds precision");
        Assert(new DownloadProgressInfo(10, null, 0, 0).Percentage == null && new DownloadProgressInfo(10, null, 0, 0).TimeRemaining == null, "unknown length and speed stay unknown");
        Assert(new DownloadProgressInfo(100, 100, Double.NaN, 5).BytesPerSecond == 0, "invalid rate cannot leak into UI");
        string directory = Path.Combine(Path.GetTempPath(), "BuildTransactions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.xml");
        try
        {
            var prefs = new Preferences
            {
                Build = "2026.2.100"
            };
            prefs.Remember(new RecentBuild { Build = "2026.2.100", Type = "Release" });
            PreferenceStore.Save(path, prefs);
            var snapshot = PreferenceStore.Capture(prefs);
            prefs.Build = "2026.2.200";
            prefs.History[0].Type = "Custom";
            PreferenceStore.Commit(path, snapshot);
            var captured = PreferenceStore.Load(path);
            Assert(captured.Build == "2026.2.100" && captured.History[0].Type == "Release", "settings snapshot deep copies preferences and history");
            var first = PreferenceStore.Load(path);
            var stale = PreferenceStore.Load(path);
            first.Build = "2026.2.300";
            PreferenceStore.Save(path, first);
            try
            {
                PreferenceStore.Save(path, stale);
                throw new Exception("Stale write accepted");
            }
            catch (SettingsConflictException)
            {
                Assert(true, "older revision cannot overwrite newer settings");
            }

            Assert(PreferenceStore.Load(path).Build == "2026.2.300", "rejected write leaves latest data intact");
            var a = PreferenceStore.Capture(PreferenceStore.Load(path));
            var b = PreferenceStore.Capture(PreferenceStore.Load(path));
            int committed = 0, conflicts = 0;
            Action<SettingsSnapshot> commit = item =>
            {
                try
                {
                    PreferenceStore.Commit(path, item);
                    Interlocked.Increment(ref committed);
                }
                catch (SettingsConflictException)
                {
                    Interlocked.Increment(ref conflicts);
                }
            };
            Task.WaitAll(Task.Run(() => commit(a)), Task.Run(() => commit(b)));
            Assert(committed == 1 && conflicts == 1, "overlapping settings commits serialize and reject stale snapshot");
            Assert(Directory.GetFiles(directory, "*.tmp").Length == 0, "transactions leave no temporary files");
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        Application.EnableVisualStyles();
        using (var dialog = new HelpDialog(null, "2026.2.257", "Completed", "https://sawindowreleasedata.blob.core.windows.net/build.msi", new[] { new DiagnosticEvent(DateTime.Now, "Download completed", "2026.2.257", null) }, "diagnostic report"))
        {
            dialog.Show();
            Application.DoEvents();
            Assert(!dialog.DiagnosticsVisible && !dialog.CopyDiagnosticsVisible, "Help starts with instructions and no diagnostic action");
            dialog.SelectDiagnostics();
            Application.DoEvents();
            Assert(dialog.DiagnosticsVisible && dialog.CopyDiagnosticsVisible && dialog.SelectedLogCharacters == 0, "Diagnostics tab shows copy action without selected text");
            dialog.SelectHelp();
            Assert(!dialog.CopyDiagnosticsVisible, "Copy diagnostics remains scoped to Diagnostics");
            dialog.Close();
        }

        Console.WriteLine(count + " release checks passed.");
    }
}
