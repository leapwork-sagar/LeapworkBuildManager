using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using LeapworkBuildManager;

static partial class HardeningTests
{
    static int count;
    static string Host = "https://" + OperationalSettings.DownloadHost;
    static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception(message);
        count++;
        Console.WriteLine("PASS " + message);
    }

    sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Reply;
        public int Calls, Disposals;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t)
        {
            Calls++;
            t.ThrowIfCancellationRequested();
            return Task.FromResult(Reply(r));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Disposals++;
            base.Dispose(disposing);
        }
    }

    static HttpResponseMessage Redirect(string uri, int status = 302)
    {
        var r = new HttpResponseMessage((HttpStatusCode)status);
        if (uri != null)
            r.Headers.Location = new Uri(uri, UriKind.RelativeOrAbsolute);
        return r;
    }

    [STAThread]
    static void Main()
    {
        UiChecks();
        Run().GetAwaiter().GetResult();
        Console.WriteLine(count + " hardening checks passed.");
    }

    static async Task Run()
    {
        foreach (string path in new[]
        {
            @"C:\Users\Alice\file.msi",
            "C:/Users/Alice/file.msi",
            "file:///C:/Users/Alice/file.msi",
            "file://server/share/Alice/file.msi",
            @"\\server\Alice\file.msi"
        }

        )
        {
            Assert(!DiagnosticPrivacy.Redact(path, true).Contains("Alice"), "redacts path " + path.Split(':')[0]);
            Assert(DiagnosticPrivacy.Redact(path, false).Contains("Alice"), "optional export keeps requested path");
        }

        Assert(DiagnosticPrivacy.Redact(Host + "/a?sig=secret", true).Contains("[redacted]"), "URL query remains redacted");
        foreach (int code in new[]
        {
            301,
            302,
            303,
            307,
            308
        }

        )
        {
            var h = new Handler();
            h.Reply = r => h.Calls == 1 ? Redirect("/final", code) : new HttpResponseMessage(HttpStatusCode.OK);
            using (var client = new HttpClient(new RestrictedRedirectHandler(h)))
            using (var response = await client.GetAsync(Host + "/start"))
                Assert(response.IsSuccessStatusCode && h.Calls == 2, "relative redirect accepted " + code);
        }

        foreach (string target in new[]
        {
            "http://" + OperationalSettings.DownloadHost + "/bad",
            "https://evil.invalid/file",
            Host + ".evil.invalid/file",
            "https://user:pass@" + OperationalSettings.DownloadHost + "/file",
            Host + ":8443/file",
            "file:///C:/file",
            "//evil.invalid/file"
        }

        )
        {
            var h = new Handler
            {
                Reply = r => Redirect(target)
            };
            using (var client = new HttpClient(new RestrictedRedirectHandler(h)))
            {
                try
                {
                    await client.GetAsync(Host + "/start");
                    throw new Exception("Unsafe redirect accepted");
                }
                catch (RedirectPolicyException)
                {
                    Assert(h.Calls == 1, "unsafe target blocked before second request: " + target);
                }
            }
        }

        var loop = new Handler
        {
            Reply = r => Redirect("/loop")
        };
        using (var client = new HttpClient(new RestrictedRedirectHandler(loop)))
        {
            try
            {
                await client.GetAsync(Host + "/start");
                throw new Exception("Loop accepted");
            }
            catch (RedirectPolicyException)
            {
                Assert(loop.Calls == OperationalSettings.MaximumRedirects + 1, "redirect chain is bounded");
            }
        }

        var missing = new Handler
        {
            Reply = r => Redirect(null)
        };
        using (var client = new HttpClient(new RestrictedRedirectHandler(missing)))
        {
            try
            {
                await client.GetAsync(Host + "/start");
                throw new Exception("Missing Location accepted");
            }
            catch (RedirectPolicyException)
            {
                Assert(missing.Calls == 1, "missing redirect destination rejected");
            }
        }

        var range = new Handler();
        range.Reply = r =>
        {
            Assert(r.Method == HttpMethod.Head && r.Headers.Range != null, "method and Range preserved");
            return range.Calls == 1 ? Redirect("/final") : new HttpResponseMessage(HttpStatusCode.OK);
        };
        using (var client = new HttpClient(new RestrictedRedirectHandler(range)))
        using (var request = new HttpRequestMessage(HttpMethod.Head, Host + "/start"))
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using (var response = await client.SendAsync(request))
            {
            }
        }

        var denied = new Handler
        {
            Reply = r => new HttpResponseMessage(HttpStatusCode.OK)
        };
        using (var client = new HttpClient(new RestrictedRedirectHandler(denied)))
        {
            try
            {
                await client.GetAsync("https://evil.invalid/");
                throw new Exception("Initial host accepted");
            }
            catch (RedirectPolicyException)
            {
                Assert(denied.Calls == 0, "initial address also validated before transport");
            }
        }

        var native = new HttpClientHandler();
        using (var redirects = new RestrictedRedirectHandler(native))
        {
            Assert(!native.AllowAutoRedirect, "native automatic redirects disabled");
        }

        var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var ch = new Handler
        {
            Reply = r => new HttpResponseMessage(HttpStatusCode.OK)
        };
        using (var client = new HttpClient(new RestrictedRedirectHandler(ch)))
        {
            try
            {
                await client.GetAsync(Host, cancelled.Token);
                throw new Exception("Ignored cancellation");
            }
            catch (OperationCanceledException)
            {
                Assert(ch.Calls == 0, "cancelled request never reaches transport");
            }
        }

        cancelled.Dispose();
        var prefs = new Preferences
        {
            SchemaVersion = 2,
            Revision = 7,
            IsReadOnly = true,
            NeedsSave = true,
            LoadNotice = "notice"
        };
        prefs.History.Add(new RecentBuild { Build = "2026.2.257" });
        var snapshot = PreferenceStore.Capture(prefs);
        prefs.History[0].Build = "changed";
        Assert(snapshot.Value.History[0].Build == "2026.2.257" && snapshot.Value.Revision == 7 && snapshot.Value.IsReadOnly && snapshot.Value.NeedsSave && snapshot.Value.LoadNotice == "notice", "explicit snapshot preserves metadata and deep history isolation");
    }
}
