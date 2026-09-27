using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using LeapworkBuildManager;

sealed class PreparationHttp : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Reply;
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Interlocked.Increment(ref Calls);
        return Reply(request, token);
    }
}

sealed class InlineProgress<T> : IProgress<T>
{
    readonly Action<T> report;
    public InlineProgress(Action<T> report)
    {
        this.report = report;
    }

    public void Report(T value)
    {
        report(value);
    }
}

static class PreparationTests
{
    static int checks;
    static void Assert(bool value, string description)
    {
        if (!value)
            throw new Exception(description);
        checks++;
        Console.WriteLine("PASS " + description);
    }

    static HttpResponseMessage Reply(HttpStatusCode status = HttpStatusCode.OK, long size = 3)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        };
        response.Content.Headers.ContentLength = size;
        return response;
    }

    [STAThread]
    static void Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BuildPreparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Run(directory).GetAwaiter().GetResult();
            Ui(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        Console.WriteLine(checks + " preparation/progress checks passed.");
    }

    static async Task Run(string directory)
    {
        string destination = Path.Combine(directory, "installer.msi");
        long reserve = OperationalSettings.DiskSpaceReserveBytes;
        var low = new DownloadPreparationService(p => false, p => reserve + 9);
        var enough = new DownloadPreparationService(p => false, p => reserve + 10);
        Assert(!low.Inspect(destination, 10).HasEnoughSpace, "insufficient space blocked including reserve");
        Assert(enough.Inspect(destination, 10).HasEnoughSpace, "exact size plus reserve accepted");
        Assert(!enough.Inspect(destination, null).SpaceVerified, "unknown installer size is explicit");
        Assert(!new DownloadPreparationService(p => false, p => null).Inspect(destination, 10).SpaceVerified, "unreadable free space is explicit");
        Assert(new DownloadPreparationService(p => false, p => 0).Inspect(destination, long.MaxValue).RequiredBytes == long.MaxValue, "required-space calculation cannot overflow");
        Assert(!new DownloadPreparationService(p => true, p => reserve).Inspect(destination, 10).HasEnoughSpace, "replacement requires room for complete temporary copy");
        File.WriteAllText(destination, "original");
        File.WriteAllText(Path.Combine(directory, "installer (1).msi"), "another");
        var preparation = new DownloadPreparationService(File.Exists, p => long.MaxValue);
        Assert(preparation.Inspect(destination, 3).Exists, "existing installer detected");
        Assert(preparation.CopyDestination(destination).EndsWith("installer (2).msi"), "save copy skips existing filenames");
        Assert(new DownloadPreparationService().Inspect(destination, 3).AvailableBytes > 0, "real destination drive space can be read");
        var http = new PreparationHttp
        {
            Reply = (q, t) => Task.FromResult(Reply())
        };
        using (var service = new BuildService(http, low))
        {
            var controller = new BuildController(service);
            controller.Reset();
            controller.Select(new BuildMatch { BuildNumber = "2026.2.257", BuildType = BuildKind.Release, Available = true, Url = new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi"), SizeBytes = 10 });
            try
            {
                await controller.DownloadAsync(destination, null, CancellationToken.None);
                throw new Exception("Expected disk-space error");
            }
            catch (IOException)
            {
                Assert(http.Calls == 0 && controller.State == ApplicationState.Failed, "known low disk space prevents network transfer");
            }
        }

        http = new PreparationHttp
        {
            Reply = (q, t) => Task.FromResult(Reply(size: 100))
        };
        using (var service = new BuildService(http, low))
        {
            try
            {
                await service.DownloadAsync(new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi"), destination, null, CancellationToken.None);
                throw new Exception("Expected disk-space error");
            }
            catch (IOException)
            {
                Assert(File.ReadAllText(destination) == "original" && Directory.GetFiles(directory, "*.part").Length == 0, "response size rechecked before temporary file creation");
            }
        }

        http = new PreparationHttp
        {
            Reply = (q, t) => Task.FromResult(Reply())
        };
        using (var service = new BuildService(http, preparation))
        {
            try
            {
                await service.DownloadAsync(new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi"), destination, null, CancellationToken.None, false);
                throw new Exception("Expected collision");
            }
            catch (IOException)
            {
                Assert(File.ReadAllText(destination) == "original", "unexpected destination collision never overwrites without permission");
            }

            Assert(Directory.GetFiles(directory, "*.part").Length == 0, "collision cleans temporary file");
            string copy = preparation.CopyDestination(destination);
            await service.DownloadAsync(new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi"), copy, null, CancellationToken.None, false);
            Assert(File.ReadAllText(destination) == "original" && new FileInfo(copy).Length == 3, "save another copy preserves original installer");
            await service.DownloadAsync(new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi"), destination, null, CancellationToken.None, true);
            Assert(new FileInfo(destination).Length == 3, "authorized replacement installs completed file");
        }

        http = new PreparationHttp
        {
            Reply = (q, t) => Task.FromResult(Reply(size: 8))
        };
        using (var service = new BuildService(http, preparation))
        {
            string warning = null;
            service.DeletePartialFile = p =>
            {
                throw new UnauthorizedAccessException("File locked");
            };
            service.CleanupWarning = text => warning = text;
            try
            {
                await service.DownloadAsync(new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi"), destination, null, CancellationToken.None);
                throw new Exception("Expected incomplete transfer");
            }
            catch (IOException error)
            {
                Assert(error.Message.Contains("incomplete"), "cleanup failure preserves original transfer error");
            }

            Assert(warning != null && warning.Contains("File locked") && warning.Contains(".part"), "cleanup failure records path and cause");
            Assert(new FileInfo(destination).Length == 3, "failed replacement preserves previous installer");
            service.CleanupWarning = text =>
            {
                throw new Exception("Diagnostic failure");
            };
            service.CleanupPartial(Directory.GetFiles(directory, "*.part")[0]);
            Assert(true, "diagnostic failure cannot mask original error");
        }

        foreach (BuildKind kind in Enum.GetValues(typeof(BuildKind)))
            Assert(BuildKinds.Parse(BuildKinds.Display(kind)) == kind && BuildKinds.Parse(BuildKinds.Token(kind)) == kind, "build type mapping round trip " + kind);
        Assert(BuildKinds.Candidates(false).Length == 4 && BuildKinds.Candidates(true).Length == 5, "runtime determines candidate count centrally");
        Assert(!BuildController.CanTransition(ApplicationState.Searching, ApplicationState.Completed, true), "search cannot transition to download completion");
        Assert(!BuildController.CanTransition(ApplicationState.Checking, ApplicationState.Ready, false), "ready state requires verified selection");
        Assert(BuildController.CanTransition(ApplicationState.Downloading, ApplicationState.Cancelled, true), "active download allows cancellation");
        var item = new DiagnosticEvent(new DateTime(2026, 9, 25), "Message | includes delimiter", "2026.2.257", new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi"));
        string report = DiagnosticEvent.FormatReport(new[] { item });
        Assert(report.Contains(item.Message) && report.Contains(item.Url.AbsoluteUri) && report.Contains(item.Build), "structured diagnostics retain message delimiters and full context");
        var gate = new TaskCompletionSource<bool>();
        var first = new TaskCompletionSource<SearchProgressInfo>();
        http = new PreparationHttp
        {
            Reply = async (q, t) =>
            {
                if (!q.RequestUri.AbsoluteUri.Contains("Release_x64"))
                    await gate.Task;
                return Reply();
            }
        };
        using (var service = new BuildService(http, preparation))
        {
            var updates = new List<SearchProgressInfo>();
            var progress = new InlineProgress<SearchProgressInfo>(p =>
            {
                lock (updates)
                    updates.Add(p);
                first.TrySetResult(p);
            });
            var search = service.FindAsync("2026.2.257", progress, CancellationToken.None);
            var received = await first.Task;
            Assert(!search.IsCompleted && received.Total == 5 && received.Result.Available, "structured result arrives before entire search completes");
            gate.SetResult(true);
            var results = await search;
            Assert(updates.Count == 5 && results.Length == 5, "search reports each checked build once");
            var counts = new HashSet<int>();
            foreach (var update in updates)
                counts.Add(update.Completed);
            Assert(counts.Count == 5 && counts.Contains(1) && counts.Contains(5), "concurrent search has complete progress counts");
        }
    }

    static object Field(MainForm form, string name)
    {
        return typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    }

    static void PumpUntil(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done())
        {
            Application.DoEvents();
            Thread.Sleep(5);
            if (clock.ElapsedMilliseconds > 8000)
                throw new Exception("UI condition timed out");
        }

        Application.DoEvents();
    }

    static void Ui(string directory)
    {
        Application.EnableVisualStyles();
        Control.CheckForIllegalCrossThreadCalls = true;
        var gate = new TaskCompletionSource<bool>();
        var handler = new PreparationHttp
        {
            Reply = async (q, t) =>
            {
                if (!q.RequestUri.AbsoluteUri.Contains("Leapwork_Release_"))
                    await gate.Task;
                return Reply();
            }
        };
        using (var service = new BuildService(handler))
        using (var form = new MainForm(Path.Combine(directory, "ui.xml"), service))
        {
            form.Show();
            PumpUntil(() => (bool)Field(form, "preferencesLoaded"));
            ((TextBox)Field(form, "build")).Text = "2026.2.257";
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            var task = (Task)typeof(MainForm).GetMethod("FindBuildsAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
            var matches = (ListBox)Field(form, "matches");
            PumpUntil(() => matches.Items.Count == 1);
            Assert(!task.IsCompleted && ((Label)Field(form, "resultSummary")).Text.Contains("1 of 5"), "UI displays live result and checked count during search");
            Assert(!matches.Enabled && !((Button)Field(form, "download")).Enabled, "partial results cannot start a download while search is active");
            gate.SetResult(true);
            PumpUntil(() => task.IsCompleted);
            task.GetAwaiter().GetResult();
            Assert(matches.Items.Count == 5 && matches.SelectedIndex == -1, "final results have no duplicates or implicit multi-result selection");
            Assert(((BuildMatch)matches.Items[0]).BuildType == BuildKind.Release && ((BuildMatch)matches.Items[1]).BuildType == BuildKind.EarlyAccess, "live results retain stable build-type ordering");
            form.Close();
            PumpUntil(() => form.IsDisposed);
        }

        using (var dialog = new ExistingInstallerDialog(Path.Combine(directory, "installer.msi")))
        {
            dialog.Show();
            Application.DoEvents();
            Assert(dialog.Choice == ExistingInstallerChoice.Cancel, "existing-file dialog defaults to no action");
            Assert(((Button)dialog.AcceptButton).Text == "Save another copy", "default affirmative action preserves existing file");
            ((Button)dialog.AcceptButton).PerformClick();
            Assert(dialog.Choice == ExistingInstallerChoice.SaveCopy, "save another copy returns explicit choice");
        }
    }
}
