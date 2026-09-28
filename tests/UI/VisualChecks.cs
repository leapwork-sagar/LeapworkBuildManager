using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LeapworkBuildManager;

static class VisualChecks
{
    sealed class Transport : HttpMessageHandler
    {
        public readonly TaskCompletionSource<HttpResponseMessage> Download = new TaskCompletionSource<HttpResponseMessage>();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method == HttpMethod.Get) return Download.Task;
            var code = request.RequestUri.AbsolutePath.Contains("_Release_") ? HttpStatusCode.OK : HttpStatusCode.NotFound;
            return Task.FromResult(new HttpResponseMessage(code) { Content = new ByteArrayContent(new byte[1024]) });
        }
    }
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(MainForm form, string name) { return typeof(MainForm).GetField(name, Flags).GetValue(form); }
    static object Call(MainForm form, string name, params object[] args) { return typeof(MainForm).GetMethod(name, Flags).Invoke(form, args); }
    static void Await(Task task) { UiTestPump.Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    static string output;
    static void Capture(Form form, string name)
    {
        form.ActiveControl = null;
        Application.DoEvents(); form.Refresh();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(output, name + ".png"), ImageFormat.Png);
        }
    }
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        output = Environment.GetEnvironmentVariable("TEST_ARTIFACT_DIR");
        if (String.IsNullOrEmpty(output)) throw new Exception("Visual output directory required");
        Directory.CreateDirectory(output);
        string temp = Path.Combine(Path.GetTempPath(), "BuildManager-Visual-" + Guid.NewGuid());
        Directory.CreateDirectory(temp);
        try
        {
            var transport = new Transport();
            using (var form = new MainForm(Path.Combine(temp, "preferences.xml"), new BuildService(transport)))
            {
                form.Show(); UiTestPump.Until(() => (bool)Get(form, "preferencesLoaded"));
                Capture(form, "01-idle");
                ((Button)Get(form, "advanced")).PerformClick(); Capture(form, "02-advanced");
                ((Button)Get(form, "advanced")).PerformClick();
                ((TextBox)Get(form, "build")).Text = "2026.2.257";
                Await((Task)Call(form, "FindBuildsAsync")); Capture(form, "03-results");
                var download = (Task)Call(form, "DownloadBuildAsync", Path.Combine(temp, "synthetic.msi"), true);
                UiTestPump.Until(() => form.State == ApplicationState.Downloading && !((System.Windows.Forms.Timer)Get(form, "expandTimer")).Enabled);
                // Replace only the displayed destination so captures contain no local user paths.
                ((Label)Get(form, "destinationLabel")).Text = "Saving to: Downloads";
                Capture(form, "04-downloading");
                transport.Download.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1024]) });
                Await(download);
                ((Label)Get(form, "destinationLabel")).Text = "Saved to: Downloads";
                Capture(form, "05-completed");
                ((Button)Get(form, "reset")).PerformClick();
                foreach (float scale in new[] { 1.5f, 2f, 1f })
                {
                    form.ApplyDisplayScale(scale, new Rectangle(10, 10, 620, 600));
                    Capture(form, "06-scale-" + (int)(scale * 100));
                }
            }
            using (var help = new HelpDialog(null, "2026.2.257", "Ready to download", "https://example.invalid/" + new string('x', 180) + "/build.msi", new DiagnosticEvent[0], "Synthetic diagnostics"))
            {
                help.Show(); Capture(help, "07-help"); help.SelectDiagnostics(); help.ToggleUrl(); Capture(help, "08-diagnostics");
                help.FitToArea(new Rectangle(0, 0, 480, 500)); Capture(help, "09-diagnostics-narrow");
            }
            using (var existing = new ExistingInstallerDialog(Path.Combine(temp, "synthetic.msi")))
            {
                existing.Show();
                RedactPaths(existing, temp);
                Capture(existing, "10-existing-installer");
            }
            using (var saved = new SavedInstallerDialog(new RecentBuild { Build = "2026.2.257", Type = "Release", DownloadPath = Path.Combine(temp, "synthetic.msi") }))
            {
                saved.Show(); UiTestPump.Until(() => !(bool)typeof(SavedInstallerDialog).GetField("checking", Flags).GetValue(saved));
                RedactPaths(saved, temp); Capture(saved, "11-saved-installer");
            }
        }
        finally { Directory.Delete(temp, true); }
        File.WriteAllText(Path.Combine(output, "README.txt"), "Synthetic app-window captures for visual review. Compare matching scenarios on the same Windows runner/font/DPI. No desktop capture. Pixel baselines require explicit review before adoption.");
        Console.WriteLine("PASS visual review captures generated for main states, dialogs and scaling");
    }
    static void RedactPaths(Control control, string path)
    {
        if (control.Text.Contains(path)) control.Text = control.Text.Replace(path, "Downloads");
        foreach (Control child in control.Controls) RedactPaths(child, path);
    }
}
