using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LeapworkBuildManager;

sealed class ControlledHttp : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Reply;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        return Reply(request, token);
    }
}

static class ControllerTests
{
    static int checks;
    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        checks++;
        Console.WriteLine("PASS " + message);
    }

    static HttpResponseMessage Response(HttpStatusCode status, long? size)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        };
        response.Content.Headers.ContentLength = size;
        return response;
    }

    static void Main()
    {
        Run().GetAwaiter().GetResult();
        Console.WriteLine(checks + " controller/model checks passed.");
    }

    static async Task Run()
    {
        var transport = new ControlledHttp();
        using (var service = new BuildService(transport))
        {
            var url = new Uri("https://sawindowreleasedata.blob.core.windows.net/build.msi");
            transport.Reply = (request, token) => Task.FromResult(Response(HttpStatusCode.OK, 1572864000));
            var result = await service.CheckAsync(url, CancellationToken.None);
            Assert(result.IsAvailable && result.SizeBytes == 1572864000, "HEAD returns typed status and installer size");
            transport.Reply = (request, token) =>
            {
                if (request.Method == HttpMethod.Head)
                    return Task.FromResult(Response(HttpStatusCode.MethodNotAllowed, null));
                var response = Response(HttpStatusCode.PartialContent, 1);
                response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, 0, 987654321);
                return Task.FromResult(response);
            };
            result = await service.CheckAsync(url, CancellationToken.None);
            Assert(result.SizeBytes == 987654321, "range fallback uses total length rather than one-byte payload");
            transport.Reply = (request, token) => Task.FromResult(Response(request.Method == HttpMethod.Head ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.PartialContent, 1));
            Assert(!(await service.CheckAsync(url, CancellationToken.None)).SizeBytes.HasValue, "missing range total is unknown size");
            transport.Reply = (request, token) =>
            {
                throw new HttpRequestException("offline");
            };
            Assert((await service.CheckAsync(url, CancellationToken.None)).Status == AvailabilityStatus.ConnectionFailed, "connection error has its own status");
            transport.Reply = (request, token) =>
            {
                throw new TaskCanceledException();
            };
            Assert((await service.CheckAsync(url, CancellationToken.None)).Status == AvailabilityStatus.TimedOut, "timeout distinguished from missing");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                try
                {
                    await service.CheckAsync(url, cancelled.Token);
                    throw new Exception("Cancellation swallowed");
                }
                catch (OperationCanceledException)
                {
                    Assert(true, "caller cancellation remains cancellation");
                }
            }

            var controller = new BuildController(service);
            controller.Reset();
            Assert(controller.State == ApplicationState.Idle, "controller initialization");
            try
            {
                await controller.DownloadAsync("unused", null, CancellationToken.None);
                throw new Exception("Unverified download allowed");
            }
            catch (InvalidOperationException)
            {
                Assert(true, "controller blocks unverified download");
            }

            transport.Reply = (request, token) => Task.FromResult(Response(HttpStatusCode.OK, 3));
            var results = await controller.SearchAsync("2026.2.257", null, CancellationToken.None);
            Assert(results.Length == 5 && controller.State == ApplicationState.Results, "controller discovers modern candidates");
            Assert(results[0].SizeBytes == 3, "search carries size into selection model");
            controller.Select(results[0]);
            Assert(controller.State == ApplicationState.Ready && controller.Selected.Url == results[0].Url, "verified selection owned by controller");
            var destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".msi");
            try
            {
                await controller.DownloadAsync(destination, null, CancellationToken.None);
                Assert(controller.State == ApplicationState.Completed && File.ReadAllBytes(destination).Length == 3, "controller completes selected download");
            }
            finally
            {
                if (File.Exists(destination))
                    File.Delete(destination);
            }

            controller.Reset();
            Assert(controller.Selected == null, "reset removes stale verified selection");
            transport.Reply = (request, token) => Task.FromResult(Response(HttpStatusCode.NotFound, null));
            await controller.CheckAsync("2026.2.257", "Release", CancellationToken.None);
            Assert(controller.State == ApplicationState.Failed && controller.Selected == null, "failed manual check cannot retain selection");
            transport.Reply = async (request, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return Response(HttpStatusCode.OK, 3);
            };
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = controller.SearchAsync("2026.2.257", null, cancellation.Token);
                Assert(controller.IsBusy, "controller exposes active search state");
                try
                {
                    controller.Reset();
                    throw new Exception("Reset accepted");
                }
                catch (InvalidOperationException)
                {
                    Assert(true, "reset blocked during active operation");
                }

                try
                {
                    controller.Transition(ApplicationState.Completed);
                    throw new Exception("Invalid transition accepted");
                }
                catch (InvalidOperationException)
                {
                    Assert(true, "invalid search-to-completion transition rejected");
                }

                try
                {
                    await controller.SearchAsync("2026.2.257", null, cancellation.Token);
                    throw new Exception("Concurrent search accepted");
                }
                catch (InvalidOperationException)
                {
                    Assert(true, "concurrent operations rejected");
                }

                cancellation.Cancel();
                try
                {
                    await pending;
                }
                catch (OperationCanceledException)
                {
                }

                Assert(controller.State == ApplicationState.Cancelled && !controller.IsBusy, "cancelled search restores nonbusy state");
            }
        }

        var prefs = new Preferences();
        prefs.Remember(new RecentBuild { Build = "2026.2.257", Type = "Release" });
        Assert(prefs.History[0].Outcome == HistoryOutcome.Searched && prefs.History[0].LastSearchedUtc.HasValue, "searched history has explicit outcome and timestamp");
        prefs.Remember(new RecentBuild { Build = "2026.2.257", Type = "Release", DownloadPath = "installer.msi" });
        var completed = prefs.History[0].DownloadedUtc;
        prefs.Remember(new RecentBuild { Build = "2026.2.257", Type = "Release" });
        Assert(prefs.History.Count == 1 && prefs.History[0].DownloadedUtc == completed && prefs.History[0].Outcome == HistoryOutcome.Downloaded, "repeat search retains completed history metadata");
        Assert(HistoryFormatter.Format(prefs.History[0]).Contains("Downloaded"), "history presentation reads structured outcome");
        var directory = Path.Combine(Path.GetTempPath(), "ControllerSettings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "preferences.xml");
        try
        {
            File.WriteAllText(path, "<Preferences><Build>2025.2.100</Build><History><RecentBuild><Build>2025.2.100</Build><Type>Release</Type><DownloadPath>old.msi</DownloadPath></RecentBuild></History></Preferences>");
            var legacy = PreferenceStore.Load(path);
            Assert(legacy.SchemaVersion == Preferences.CurrentVersion && legacy.History[0].Outcome == HistoryOutcome.Downloaded, "legacy settings migrate without losing downloaded history");
            Assert(!legacy.History[0].DownloadedUtc.HasValue, "migration does not invent old download dates");
            PreferenceStore.Save(path, legacy);
            File.WriteAllText(path, "broken");
            var recovered = PreferenceStore.Load(path);
            Assert(recovered.Build == "2025.2.100" && recovered.LoadNotice != null, "backup recovery reports notice");
            PreferenceStore.Save(path, recovered);
            Assert(Directory.GetFiles(directory, "*.preserved-*").Length == 1, "damaged original preserved before replacement");
            File.WriteAllText(path, "damaged again");
            Assert(PreferenceStore.Load(path).Build == "2025.2.100", "recovery save retains last valid backup");
            File.WriteAllText(path, "<Preferences><SchemaVersion>99</SchemaVersion></Preferences>");
            var future = PreferenceStore.Load(path);
            PreferenceStore.Save(path, future);
            Assert(future.IsReadOnly && File.ReadAllText(path).Contains("99"), "future schema never overwritten");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
