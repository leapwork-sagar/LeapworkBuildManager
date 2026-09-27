using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LeapworkBuildManager;

class Fake : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> Reply;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t)
    {
        return Task.FromResult(Reply(r, t));
    }
}

class BuildServiceTests
{
    static int count;
    static void Assert(bool ok, string name)
    {
        if (!ok)
            throw new Exception(name);
        count++;
        Console.WriteLine("PASS " + name);
    }

    static void Main()
    {
        Run().GetAwaiter().GetResult();
        Console.WriteLine(count + " checks passed.");
    }

    static async Task Run()
    {
        string error;
        foreach (var value in new[]
        {
            "2025.2.990",
            "2025.3.990",
            " 2026.1.12 "
        }

        )
            Assert(BuildService.Validate(value, out error), "valid " + value);
        foreach (var value in new[]
        {
            "2025.10.1",
            "2026.0.1",
            "2026.5.1",
            "2026.01.1",
            "",
            "2025.2",
            "2025.2.abc",
            "202.2.3",
            "2025..3",
            "2025.2.3/evil",
            "2025.2.3\nextra"
        }

        )
            Assert(!BuildService.Validate(value, out error), "invalid " + value);
        var url = BuildService.BuildUrl("2025.3.990", "Experimental", true);
        Assert(url.AbsoluteUri.EndsWith("leaptest-unstable-builds/Leapwork_NETCore_Experimental_x64_2025.3.990.msi"), "Core mapping");
        Assert(BuildService.BuildUrl("2025.2.990", "Release", true) == BuildService.BuildUrl("2025.2.990", "Release", false), "Release mapping preserved");
        Assert(BuildService.BuildUrl("2026.1.329", "PreRelease", true).AbsoluteUri == "https://sawindowreleasedata.blob.core.windows.net/leaptest-unstable-builds/Leapwork_NETCore_PreRelease_x64_2026.1.329.msi", "exact PreRelease URL");
        try
        {
            BuildService.BuildUrl("2026.1.329", "PreRelease", false);
            throw new Exception("Expected rejection");
        }
        catch (ArgumentException)
        {
            Assert(true, "unsupported PreRelease Framework rejected");
        }

        var settingsFolder = Path.Combine(Path.GetTempPath(), "BuildPrefsTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsFolder);
        try
        {
            var settingsFile = Path.Combine(settingsFolder, "preferences.xml");
            Assert(PreferenceStore.Load(settingsFile).History.Count == 0, "missing preferences defaults");
            var prefs = new Preferences
            {
                Build = "2026.1.329",
                Type = "Pre-release",
                Runtime = 0,
                DownloadFolder = settingsFolder
            };
            for (int i = 0; i < 25; i++)
                prefs.Remember(new RecentBuild { Build = "2026.1." + i, Type = "Experimental", Runtime = 0 });
            Assert(prefs.History.Count == 20 && prefs.History[0].Build == "2026.1.24", "history bounded newest first");
            prefs.Remember(new RecentBuild { Build = "2026.1.24", Type = "Experimental", Runtime = 0, DownloadPath = "test.msi" });
            prefs.Remember(new RecentBuild { Build = "2026.1.24", Type = "Experimental", Runtime = 0 });
            Assert(prefs.History.Count == 20 && prefs.History[0].DownloadPath == "test.msi", "history deduplicates and retains download");
            PreferenceStore.Save(settingsFile, prefs);
            var loaded = PreferenceStore.Load(settingsFile);
            Assert(loaded.Build == prefs.Build && loaded.Type == prefs.Type && loaded.Runtime == 0 && loaded.DownloadFolder == settingsFolder && loaded.History.Count == 20, "preferences and history persist across reload");
            prefs.Runtime = 1;
            PreferenceStore.Save(settingsFile, prefs);
            Assert(PreferenceStore.Load(settingsFile).Runtime == 1, "existing preferences updated");
            File.WriteAllText(settingsFile, "invalid xml");
            Assert(PreferenceStore.Load(settingsFile).History.Count == 20, "corrupt preferences recovered from backup");
        }
        finally
        {
            Directory.Delete(settingsFolder, true);
        }

        Assert(!BuildService.IsModern("2025.2.999999999999999999999999"), "pre-cutoff runtime");
        Assert(BuildService.IsModern("2025.3.0"), "cutoff inclusive");
        Assert(BuildService.IsModern("2026.1.0"), "later year modern");
        Assert(!BuildService.IsModern("2024.4.999"), "earlier year legacy");
        var fake = new Fake();
        using (var service = new BuildService(fake))
        {
            foreach (var pair in new[]
            {
                Tuple.Create(HttpStatusCode.OK, "available"),
                Tuple.Create(HttpStatusCode.NotFound, "not found"),
                Tuple.Create(HttpStatusCode.Forbidden, "Access denied")
            }

            )
            {
                fake.Reply = (r, t) => new HttpResponseMessage(pair.Item1);
                Assert((await service.CheckAsync(url, CancellationToken.None)).Detail.Contains(pair.Item2), "availability " + pair.Item1);
            }

            int calls = 0;
            fake.Reply = (r, t) =>
            {
                calls++;
                if (r.Method == HttpMethod.Head)
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                Assert(r.Headers.Range.ToString() == "bytes=0-0", "bounded fallback request");
                return new HttpResponseMessage(HttpStatusCode.PartialContent);
            };
            Assert((await service.CheckAsync(url, CancellationToken.None)).Detail.Contains("available") && calls == 2, "HEAD fallback");
            fake.Reply = (r, t) => new HttpResponseMessage(HttpStatusCode.InternalServerError);
            Assert((await service.CheckAsync(url, CancellationToken.None)).Status == AvailabilityStatus.ServerError, "server error distinct from missing build");
            var folder = Path.Combine(Path.GetTempPath(), "BuildUrlTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "build.msi");
            try
            {
                fake.Reply = (r, t) => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
                };
                await service.DownloadAsync(url, file, null, CancellationToken.None);
                Assert(File.ReadAllBytes(file).Length == 3, "download content");
                File.WriteAllText(file, "existing");
                fake.Reply = (r, t) => new HttpResponseMessage(HttpStatusCode.NotFound);
                try
                {
                    await service.DownloadAsync(url, file, null, CancellationToken.None);
                }
                catch (HttpRequestException)
                {
                }

                Assert(File.ReadAllText(file) == "existing", "failed download preserves existing file");
                fake.Reply = (r, t) =>
                {
                    var c = new ByteArrayContent(new byte[] { 1, 2, 3 });
                    c.Headers.ContentLength = 10;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = c
                    };
                };
                try
                {
                    await service.DownloadAsync(url, file, null, CancellationToken.None);
                }
                catch (IOException)
                {
                }

                Assert(File.ReadAllText(file) == "existing" && Directory.GetFiles(folder).Length == 1, "truncated download cleaned up");
                var cancel = new CancellationTokenSource();
                fake.Reply = (r, t) =>
                {
                    cancel.Cancel();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
                    };
                };
                try
                {
                    await service.DownloadAsync(url, file, null, cancel.Token);
                }
                catch (OperationCanceledException)
                {
                }

                Assert(File.ReadAllText(file) == "existing" && Directory.GetFiles(folder).Length == 1, "cancellation preserves existing file and removes partial");
                fake.Reply = (r, t) => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 4, 5 })
                };
                await service.DownloadAsync(url, file, null, CancellationToken.None);
                Assert(File.ReadAllBytes(file)[0] == 4, "successful replacement");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
