using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using LeapworkBuildManager;

static class ReliabilityTests
{
    static int count;
    static void Assert(bool b, string name)
    {
        if (!b)
            throw new Exception(name);
        count++;
        Console.WriteLine("PASS " + name);
    }

    [STAThread]
    static void Main()
    {
        UiChecks();
        Run().GetAwaiter().GetResult();
        Console.WriteLine(count + " reliability checks passed.");
    }

    static object Field(MainForm f, string name)
    {
        return typeof(MainForm).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(f);
    }

    static object Call(MainForm f, string name, params object[] args)
    {
        return typeof(MainForm).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(f, args);
    }

    static void Until(Func<bool> condition)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
            if (clock.ElapsedMilliseconds > 5000)
                throw new Exception("UI timeout");
        }
    }

    static void UiChecks()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        string dir = Path.Combine(Path.GetTempPath(), "ReliabilityUi-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            using (var form = new MainForm(Path.Combine(dir, "prefs.xml"), new BuildService(new BlockingHandler())))
            {
                form.Show();
                Until(() => (bool)Field(form, "preferencesLoaded"));
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Forms.WindowsFormsSynchronizationContext());
                ((System.Windows.Forms.TextBox)Field(form, "build")).Text = "2";
                Call(form, "UpdateBuildFeedback", true);
                Assert(((System.Windows.Forms.Label)Field(form, "validation")).AccessibleName.Contains("four-digit"), "validation announcement contains the correction");
                ((System.Windows.Forms.TextBox)Field(form, "build")).Text = "2026.2.257";
                var controller = (BuildController)Field(form, "controller");
                controller.Select(new BuildMatch { BuildNumber = "2026.2.257", Kind = "Release", Available = true, Url = new Uri("https://sawindowreleasedata.blob.core.windows.net/file.msi") });
                var transfer = (Task)Call(form, "DownloadBuildAsync", Path.Combine(dir, "file.msi"), false);
                var operations = (OperationCoordinator)Field(form, "operations");
                Call(form, "ReportProgress", operations.Current, new DownloadProgressInfo(20 * 1048576, 100 * 1048576, 1000, 20));
                int prompts = 0;
                form.ConfirmCancelDownload = () =>
                {
                    prompts++;
                    return false;
                };
                Call(form, "RequestCancellation", CancellationReason.Escape);
                Assert(prompts == 1 && !operations.Current.IsCancellationRequested, "declining Escape preserves active download");
                form.ConfirmCancelDownload = () =>
                {
                    prompts++;
                    return true;
                };
                Call(form, "RequestCancellation", CancellationReason.CancelButton);
                Until(() => transfer.IsCompleted);
                transfer.GetAwaiter().GetResult();
                Assert(prompts == 2 && operations.Current == null && form.State == ApplicationState.Cancelled, "confirmed cancel completes cleanup and restores state");
                Assert(form.DiagnosticText().Contains("CancelButton") && form.DiagnosticText().Contains("Escape"), "cancel reason and decline appear in diagnostics");
                Func<Task> broken = () =>
                {
                    operations.Begin();
                    throw new InvalidOperationException("injected setup failure");
                };
                var guarded = (Task)Call(form, "RunUiAsync", broken);
                Until(() => guarded.IsCompleted);
                guarded.GetAwaiter().GetResult();
                Assert(operations.Current == null && form.State == ApplicationState.Failed, "unexpected setup failure releases operation");
                Assert(form.DiagnosticText().Contains("injected setup failure") && !((System.Windows.Forms.Label)Field(form, "status")).Text.Contains("injected"), "unexpected failure logged separately from friendly text");
                form.Close();
                Until(() => form.IsDisposed);
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(null);
            Directory.Delete(dir, true);
        }
    }

    sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t)
        {
            await Task.Delay(Timeout.Infinite, t);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    static async Task Run()
    {
        Assert(!CancellationPolicy.NeedsConfirmation(true, 0, 0, CancellationReason.CancelButton), "early cancel remains immediate");
        Assert(CancellationPolicy.NeedsConfirmation(true, 10485760, 0, CancellationReason.CancelButton), "substantial bytes require confirmation");
        Assert(CancellationPolicy.NeedsConfirmation(true, 0, 10, CancellationReason.Escape), "elapsed transfer Escape requires confirmation");
        Assert(CancellationPolicy.NeedsConfirmation(true, 0, 0, CancellationReason.WindowClosing), "closing download always confirms");
        Assert(!CancellationPolicy.NeedsConfirmation(true, long.MaxValue, 1000, CancellationReason.SystemSleep), "sleep never prompts");
        Assert(!CancellationPolicy.NeedsConfirmation(false, long.MaxValue, 1000, CancellationReason.CancelButton), "search cancels without download prompt");
        var secret = "https://user:password@example.org/build.msi?sig=SECRET#TOKEN";
        var redacted = DiagnosticPrivacy.Redact(secret, false);
        Assert(!redacted.Contains("password") && !redacted.Contains("SECRET") && !redacted.Contains("TOKEN") && redacted.Contains("build.msi"), "URL export removes credentials query and fragment");
        Assert(DiagnosticPrivacy.Redact(@"Saved C:\Users\Alice\Desktop\a.msi", true).Contains("[local path]"), "export hides local path");
        Assert(DiagnosticPrivacy.Redact(@"Saved C:\Users\Alice\Desktop\a.msi", false).Contains("Alice"), "export can retain requested paths");
        Assert(DiagnosticPrivacy.Redact(@"Saved \\server\share\a.msi", true).Contains("[local path]"), "export hides UNC path");
        Assert(FailureInfo.From(new UnauthorizedAccessException("secret")).Category == "Permissions", "permissions recovery category");
        Assert(FailureInfo.From(new HttpRequestException("secret")).Message.Contains("connection") && !FailureInfo.From(new HttpRequestException("secret")).Message.Contains("secret"), "network error separates technical text");
        Assert(FailureInfo.From(new OperationCanceledException()).Category == "Timeout", "timeout recovery category");
        Assert(FailureInfo.From(new InsufficientDiskSpaceException("details")).Category == "DiskSpace", "disk-space failure has specific recovery");
        Assert(FailureInfo.From(new DownloadHttpException(404)).Category == "NotFound", "missing installer requires fresh search");
        Assert(FailureInfo.From(new DownloadHttpException(403)).Category == "AccessDenied", "server denial separate from offline failure");
        var error = new IOException("outer", new InvalidOperationException("inner"));
        var report = DiagnosticEvent.FormatReport(new[] { new DiagnosticEvent(DateTime.Now, "Failed", "2026.2.257", null, "Error", "Download", "operation-123", error) });
        Assert(report.Contains("operation-123") && report.Contains("Error") && report.Contains("inner") && report.Contains("IOException"), "structured report retains correlated nested failure");
        string dir = Path.Combine(Path.GetTempPath(), "Reliability-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            using (var log = new DiagnosticLog(dir, 1000))
            {
                for (int i = 0; i < 30; i++)
                    log.Write(new string ('x', 200));
                await log.CompleteAsync();
            }

            Assert(File.Exists(Path.Combine(dir, "session.log.3")) && Directory.GetFiles(dir).Length == 4, "logs rotate with three backups");
            using (var log = new DiagnosticLog(dir))
            {
                log.Write(secret + @" C:\Users\Alice\secret");
                await log.CompleteAsync();
            }

            var text = File.ReadAllText(Path.Combine(dir, "session.log"));
            Assert(!text.Contains("SECRET") && !text.Contains("Alice"), "persistent logs redact sensitive values");
            using (var restarted = new DiagnosticLog(dir))
            {
                restarted.Write("after restart");
                await restarted.CompleteAsync();
            }

            Assert(File.ReadAllText(Path.Combine(dir, "session.log")).Contains("after restart"), "logs persist across writer sessions");
            string file = Path.Combine(dir, "not-a-directory");
            File.WriteAllText(file, "x");
            using (var broken = new DiagnosticLog(file))
            {
                broken.Write("failure");
                await broken.CompleteAsync();
                Assert(broken.LastError != null, "logging IO failure does not escape");
                broken.Write("after close");
            }

            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            using (var blocked = new DiagnosticLog(dir, 1048576, line =>
            {
                entered.Set();
                release.Wait();
            }))
            {
                blocked.Write("first");
                Assert(entered.Wait(2000), "background sink starts");
                for (int i = 0; i < 300; i++)
                    blocked.Write("queued");
                Assert(blocked.Dropped > 0, "bounded queue drops rather than blocking UI");
                release.Set();
                await blocked.CompleteAsync();
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }

        using (var service = new BuildService(new FailureHandler()))
        {
            string detail = "";
            service.Diagnostic = (message, e) => detail += message + (e == null ? "" : e.ToString());
            var result = await service.CheckAsync(new Uri("https://sawindowreleasedata.blob.core.windows.net/test"), CancellationToken.None);
            Assert(result.Status == AvailabilityStatus.ConnectionFailed && detail.Contains("nested network failure"), "availability diagnostics retain original exception");
            service.Diagnostic = (message, e) =>
            {
                throw new Exception("broken logger");
            };
            result = await service.CheckAsync(new Uri("https://sawindowreleasedata.blob.core.windows.net/test"), CancellationToken.None);
            Assert(result.Status == AvailabilityStatus.ConnectionFailed, "diagnostic callback failure cannot break availability result");
        }
    }

    sealed class FailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t)
        {
            throw new HttpRequestException("nested network failure");
        }
    }
}
