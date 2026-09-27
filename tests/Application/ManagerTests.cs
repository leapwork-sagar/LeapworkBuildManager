using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LeapworkBuildManager;

sealed class ManagerHttp : HttpMessageHandler
{
    public bool Fail;
    public TaskCompletionSource<bool> Gate;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (Gate != null)
            await Gate.Task;
        await Task.Delay(30, token);
        return new HttpResponseMessage(Fail ? HttpStatusCode.NotFound : HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 })
        };
    }
}

static class ManagerTests
{
    static int checks;
    static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Assert(bool value, string description)
    {
        if (!value)
            throw new Exception(description);
        checks++;
        Console.WriteLine("PASS " + description);
    }

    static object Field(MainForm form, string name)
    {
        return typeof(MainForm).GetField(name, flags).GetValue(form);
    }

    static Task Call(MainForm form, string name, params object[] args)
    {
        return (Task)typeof(MainForm).GetMethod(name, flags).Invoke(form, args);
    }

    static void PumpUntil(Func<bool> predicate)
    {
        var watch = Stopwatch.StartNew();
        while (!predicate())
        {
            Application.DoEvents();
            Thread.Sleep(5);
            if (watch.ElapsedMilliseconds > 8000)
                throw new Exception("UI timeout");
        }

        Application.DoEvents();
    }

    static BuildMatch Match()
    {
        return new BuildMatch
        {
            BuildNumber = "2026.2.257",
            BuildType = BuildKind.Release,
            Available = true,
            SizeBytes = 4,
            Url = new Uri("https://sawindowreleasedata.blob.core.windows.net/build.msi")
        };
    }

    [STAThread]
    static void Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ManagerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Core(directory).GetAwaiter().GetResult();
            Ui(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        Console.WriteLine(checks + " manager/result checks passed.");
    }

    static async Task Core(string directory)
    {
        var sample = new DownloadResult(Path.Combine(directory, "sample.msi"), 704L * 1048576, TimeSpan.FromSeconds(135));
        Assert(sample.Summary == "Downloaded 704.0 MB in 2m 15s.", "completion summary formats bytes and duration");
        Assert(new DownloadResult(sample.Destination, 0, TimeSpan.Zero).Summary.EndsWith("in 1s."), "subsecond completion has useful duration");
        Assert(new DownloadResult(sample.Destination, 0, TimeSpan.FromSeconds(3661)).Summary.EndsWith("in 1h 1m 1s."), "long completion formats hours");
        Assert(Path.IsPathRooted(sample.Destination), "result stores absolute destination");
        foreach (var property in typeof(DownloadResult).GetProperties())
            Assert(property.GetSetMethod() == null, "download result immutable: " + property.Name);
        using (var owner = new OperationCoordinator())
        {
            var first = owner.Begin();
            var token = first.Token;
            Assert(owner.Accepts(first), "current operation accepts progress");
            try
            {
                owner.Begin();
                throw new Exception("Concurrent operation allowed");
            }
            catch (InvalidOperationException)
            {
                Assert(true, "coordinator rejects overlapping operations");
            }

            owner.Cancel();
            Assert(token.IsCancellationRequested && !owner.Accepts(first), "cancellation rejects further progress");
            owner.Complete(first);
            Assert(owner.Current == null, "completion clears current operation");
            var second = owner.Begin();
            owner.Complete(first);
            Assert(owner.Current == second && !owner.Accepts(first), "stale completion cannot clear newer operation");
            var secondToken = second.Token;
            owner.Dispose();
            Assert(secondToken.IsCancellationRequested && owner.Current == null, "disposing coordinator cancels and releases current operation");
            try
            {
                owner.Begin();
                throw new Exception("Disposed owner reused");
            }
            catch (ObjectDisposedException)
            {
                Assert(true, "disposed coordinator cannot restart");
            }
        }

        using (var release = new ManualResetEventSlim())
        using (var started = new ManualResetEventSlim())
        using (var cancel = new CancellationTokenSource())
        {
            int caller = Thread.CurrentThread.ManagedThreadId, worker = caller;
            var preparation = new DownloadPreparationService(p => false, p =>
            {
                worker = Thread.CurrentThread.ManagedThreadId;
                started.Set();
                release.Wait(3000);
                return long.MaxValue;
            });
            var task = preparation.InspectAsync(sample.Destination, 4, cancel.Token);
            Assert(started.Wait(2000) && !task.IsCompleted && worker != caller, "destination probe runs off caller thread");
            cancel.Cancel();
            Assert(await Task.WhenAny(task, Task.Delay(1000)) == task, "cancellation stops waiting for blocked drive probe");
            try
            {
                await task;
                throw new Exception("Expected cancellation");
            }
            catch (OperationCanceledException)
            {
                Assert(true, "cancelled probe cannot return stale result");
            }

            release.Set();
        }

        var handler = new ManagerHttp();
        using (var service = new BuildService(handler))
        {
            var controller = new BuildController(service);
            controller.Reset();
            controller.Select(Match());
            var result = await controller.DownloadAsync(Path.Combine(directory, "result.msi"), null, CancellationToken.None, false);
            Assert(result.BytesWritten == 4 && File.ReadAllBytes(result.Destination).Length == 4, "result reports committed actual bytes");
            Assert(result.Elapsed.TotalMilliseconds >= 20, "elapsed time includes request and transfer");
            Assert(controller.CompletedDownload == result && controller.CompletedPath == result.Destination, "controller retains successful structured result");
            controller.Reset();
            Assert(controller.CompletedDownload == null, "reset discards prior completion result");
            controller.Select(Match());
            handler.Fail = true;
            try
            {
                await controller.DownloadAsync(Path.Combine(directory, "failure.msi"), null, CancellationToken.None);
                throw new Exception("Expected failure");
            }
            catch (HttpRequestException)
            {
                Assert(controller.CompletedDownload == null && !File.Exists(Path.Combine(directory, "failure.msi")), "failed transfer never exposes successful result");
            }
        }

        Assert(BuildController.CanTransition(ApplicationState.Ready, ApplicationState.Preparing, true), "verified build can begin preparation");
        Assert(!BuildController.CanTransition(ApplicationState.Ready, ApplicationState.Preparing, false), "unverified build cannot begin preparation");
    }

    static void Ui(string directory)
    {
        Application.EnableVisualStyles();
        Control.CheckForIllegalCrossThreadCalls = true;
        using (var release = new ManualResetEventSlim())
        using (var started = new ManualResetEventSlim())
        {
            var preparation = new DownloadPreparationService(p => false, p =>
            {
                started.Set();
                release.Wait(3000);
                return long.MaxValue;
            });
            using (var form = new MainForm(Path.Combine(directory, "prefs.xml"), new BuildService(new ManagerHttp(), preparation)))
            {
                form.Show();
                PumpUntil(() => (bool)Field(form, "preferencesLoaded"));
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                ((TextBox)Field(form, "build")).Text = "2026.2.257";
                ((BuildController)Field(form, "controller")).Select(Match());
                var task = Call(form, "PrepareAndDownloadAsync", Path.Combine(directory, "cancel.msi"));
                PumpUntil(() => started.IsSet);
                Assert(form.State == ApplicationState.Preparing && ((Label)Field(form, "status")).Text.Contains("Checking destination"), "UI shows destination checking state");
                Assert(!((Button)Field(form, "reset")).Enabled && ((Button)Field(form, "check")).Text == "Cancel check", "preparation has cancel action and blocks conflicting controls");
                ((Button)Field(form, "check")).PerformClick();
                PumpUntil(() => task.IsCompleted);
                task.GetAwaiter().GetResult();
                Assert(form.State == ApplicationState.Ready && ((Button)Field(form, "reset")).Enabled, "cancelled destination check restores ready controls");
                Assert(!File.Exists(Path.Combine(directory, "cancel.msi")), "cancelled preparation never downloads");
                release.Set();
                form.Close();
                PumpUntil(() => form.IsDisposed);
            }
        }

        var handler = new ManagerHttp
        {
            Gate = new TaskCompletionSource<bool>()
        };
        using (var form = new MainForm(Path.Combine(directory, "download-prefs.xml"), new BuildService(handler)))
        {
            form.Show();
            PumpUntil(() => (bool)Field(form, "preferencesLoaded"));
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ((TextBox)Field(form, "build")).Text = "2026.2.257";
            var controller = (BuildController)Field(form, "controller");
            controller.Select(Match());
            var task = Call(form, "DownloadBuildAsync", Path.Combine(directory, "ui.msi"), false);
            Assert(((Label)Field(form, "destinationLabel")).Text.StartsWith("Saving to: ") && ((Label)Field(form, "destinationLabel")).AccessibleDescription == directory, "active download displays selected folder");
            Assert(typeof(MainForm).GetField("runtime", flags) == null, "obsolete runtime dropdown removed");
            Assert(form.Text.StartsWith("Leapwork Build Manager"), "window displays new app name");
            handler.Gate.SetResult(true);
            PumpUntil(() => task.IsCompleted);
            task.GetAwaiter().GetResult();
            Assert(((Label)Field(form, "status")).Text == controller.CompletedDownload.Summary, "completion UI uses actual result summary");
            Assert(((Label)Field(form, "destinationLabel")).Text.StartsWith("Saved to: ") && ((Label)Field(form, "destinationLabel")).AccessibleDescription == directory, "completed download retains destination");
            PumpUntil(() => !((System.Windows.Forms.Timer)Field(form, "expandTimer")).Enabled);
            var path = (Label)Field(form, "destinationLabel");
            var status = (Label)Field(form, "status");
            Assert(path.Top >= status.Bottom && path.Bottom <= path.Parent.ClientSize.Height, "destination row does not overlap completion text or panel edge");
            form.Close();
            PumpUntil(() => form.IsDisposed);
        }
    }
}
