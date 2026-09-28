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
using Microsoft.Win32;
using LeapworkBuildManager;

sealed class LifecycleHttp : HttpMessageHandler
{
    public bool Block;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (Block)
            await Task.Delay(Timeout.Infinite, token);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        };
    }
}

static class LifecycleTests
{
    static int count;
    static BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(MainForm form, string name)
    {
        var field = typeof(MainForm).GetField(name, flags);
        return field != null ? field.GetValue(form) : typeof(MainForm).GetProperty(name, flags).GetValue(form, null);
    }

    static object Call(MainForm form, string name, params object[] args)
    {
        return typeof(MainForm).GetMethod(name, flags).Invoke(form, args);
    }

    static void Assert(bool value, string label)
    {
        if (!value)
            throw new Exception(label);
        count++;
        Console.WriteLine("PASS " + label);
    }

    static void Pump(int milliseconds = 100)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    static void Await(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Pump(10);
            if (watch.ElapsedMilliseconds > 5000)
                throw new Exception("Operation timed out");
        }

        task.GetAwaiter().GetResult();
        Pump();
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--instance-client")
            using (var client = new SingleInstance(args[1]))
            {
                if (client.IsPrimary)
                    return 4;
                client.ActivateExisting();
                return 0;
            }

        Application.EnableVisualStyles();
        string name = "LeapworkTest-" + Guid.NewGuid().ToString("N");
        using (var activation = new AutoResetEvent(false))
        using (var primary = new SingleInstance(name))
        {
            Assert(primary.IsPrimary, "first instance owns session mutex");
            primary.Listen(() => activation.Set());
            using (var child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--instance-client " + name) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
            {
                Assert(child.WaitForExit(5000) && child.ExitCode == 0, "second process exits instead of opening another instance");
                Assert(activation.WaitOne(2000), "second process signals primary activation");
            }
        }

        using (var next = new SingleInstance(name))
            Assert(next.IsPrimary, "instance ownership released on exit");
        foreach (float scale in new[]
        {
            1f,
            1.25f,
            1.5f,
            2f
        }

        )
            foreach (var area in new[]
            {
                new Rectangle(0, 0, 1920, 1080),
                new Rectangle(20, 30, 480, 400)
            }

            )
            {
                var plan = WindowLayoutPlan.Calculate(area, scale, 480, 17);
                Assert(plan.WindowSize.Width <= area.Width && plan.WindowSize.Height <= area.Height && plan.CanvasWidth <= plan.WindowSize.Width, "layout plan fits work area at " + scale);
                var centered = WindowLayoutPlan.Center(area, plan.WindowSize);
                Assert(area.Contains(new Rectangle(centered, plan.WindowSize)), "centering respects work area origin at " + scale);
            }

        var folder = Path.Combine(Path.GetTempPath(), "Lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var handler = new LifecycleHttp();
            using (var form = new MainForm(Path.Combine(folder, "prefs.xml"), new BuildService(handler)))
            {
                var work = new Rectangle(100, 100, 1000, 850);
                form.WorkingAreaProvider = () => work;
                Assert(!form.AdvancedControlsCreated, "Advanced controls are not created during startup");
                form.Show();
                while (!(bool)Get(form, "preferencesLoaded"))
                    Pump(10);
                Assert(form.Location == WindowLayoutPlan.Center(work, form.Size), "startup centers after final preference-driven layout");
                Point moved = new Point(150, 150);
                form.Location = moved;
                ((Button)Get(form, "advanced")).PerformClick();
                Pump(250);
                Assert(form.AdvancedControlsCreated, "Advanced controls created on first expansion");
                var viewport = (Panel)Get(form, "viewport");
                Assert(!viewport.HorizontalScroll.Visible && !viewport.VerticalScroll.Visible, "Advanced expansion has no scrollbars when content fits");
                Assert(form.Location == moved, "later expansion preserves user window position");
                ((Button)Get(form, "reset")).PerformClick();
                Pump(250);
                Assert(form.Location == WindowLayoutPlan.Center(work, form.Size), "Reset centers final layout in current work area");
                Assert(((SoftButton)Get(form, "minimize")).SymbolOnly && ((SoftButton)Get(form, "close")).SymbolOnly, "title-bar controls use symbol rendering");
                ((Button)Get(form, "advanced")).PerformClick();
                Pump(250);
                Assert(!viewport.HorizontalScroll.Visible && !viewport.VerticalScroll.Visible, "collapse clears unused scrollbars");
                foreach (float scale in new[]
                {
                    1f,
                    1.5f,
                    2f
                }

                )
                {
                    typeof(MainForm).GetField("layoutScale", flags).SetValue(form, scale);
                    work = new Rectangle(0, 0, 480, 400);
                    Call(form, "ReflowDownloadPanel");
                    Pump();
                    Assert(!viewport.HorizontalScroll.Visible && ((Panel)Get(form, "canvas")).Width <= viewport.ClientSize.Width, "narrow layout has no horizontal scrollbar at " + scale);
                    var detail = (Panel)Get(form, "details");
                    var build = (Control)Get(form, "build");
                    var check = (Control)Get(form, "check");
                    var summary = (Label)Get(form, "resultSummary");
                    summary.Text = new string('W', 150) + " — checks failed; retry search for full availability.";
                    Call(form, "ReflowDownloadPanel");
                    Pump();
                    var needed = TextRenderer.MeasureText(summary.Text, summary.Font, new Size(summary.Width, int.MaxValue), TextFormatFlags.WordBreak);
                    Assert(summary.Height >= needed.Height && summary.Bottom <= summary.Parent.Height, "long summary stays readable at " + scale);
                    Assert(((TextBox)Get(form, "url")).ScrollBars == ScrollBars.Vertical, "long download URL remains scrollable");
                    Assert(build.Right <= detail.Width && check.Right <= detail.Width && !build.Bounds.IntersectsWith(check.Bounds), "narrow build input and search action do not overlap at " + scale);
                }

                typeof(MainForm).GetField("layoutScale", flags).SetValue(form, 1f);
                work = new Rectangle(0, 0, 1000, 900);
                Call(form, "ReflowDownloadPanel");
                ((TextBox)Get(form, "build")).Text = "2026.2.257";
                var controller = (BuildController)Get(form, "controller");
                Await(controller.CheckAsync("2026.2.257", "Release", CancellationToken.None));
                handler.Block = true;
                var task = (Task)Call(form, "DownloadBuildAsync", Path.Combine(folder, "sleep.msi"), true);
                Pump();
                form.HandlePowerChange(PowerModes.Suspend);
                Await(task);
                form.HandlePowerChange(PowerModes.Resume);
                Assert(controller.InterruptedBySleep && controller.State == ApplicationState.Cancelled, "sleep cancels an active download safely");
                Assert(((Button)Get(form, "download")).Text == "Retry download" && ((Button)Get(form, "download")).Enabled, "wake offers explicit restart action");
                Assert(!File.Exists(Path.Combine(folder, "sleep.msi")) && Directory.GetFiles(folder, "*.part").Length == 0, "sleep interruption leaves no installer or partial file");
                handler.Block = false;
                Await((Task)Call(form, "DownloadBuildAsync", Path.Combine(folder, "sleep.msi"), true));
                Assert(controller.State == ApplicationState.Completed && !controller.InterruptedBySleep, "retry after wake completes and clears interruption state");
                ((TextBox)Get(form, "build")).Text = "2026.2.258";
                handler.Block = true;
                var search = (Task)Call(form, "FindBuildsAsync");
                form.Close();
                Assert(((Label)Get(form, "status")).Text.Contains("closing"), "closing an active search gives explicit cancellation status");
                Await(search);
                Pump();
                Assert(form.IsDisposed, "search cancellation finishes before closing");
            }

            foreach (float scale in new[]
            {
                1f,
                1.5f,
                2f
            }

            )
                using (var dialog = new HelpDialog(null, "", "Ready.", "https://sawindowreleasedata.blob.core.windows.net/build.msi", new[] { new DiagnosticEvent(DateTime.Now, "Search started", "", new Uri("https://sawindowreleasedata.blob.core.windows.net/build.msi")) }, "Full diagnostic report"))
                {
                    Assert(!dialog.DiagnosticsCreated, "Diagnostics is lazy at " + scale);
                    if (scale != 1)
                    {
                        UiScale.ApplyTree(dialog, scale);
                        typeof(HelpDialog).GetField("scale", flags).SetValue(dialog, scale);
                    }

                    dialog.Show();
                    dialog.FitToArea(new Rectangle(0, 0, 640, 600));
                    Pump();
                    dialog.SelectDiagnostics();
                    Pump();
                    Assert(dialog.DiagnosticsCreated && dialog.FooterActionsFit, "footer buttons fully contained at " + scale);
                    Assert(dialog.DisplayedUrl == "build.msi", "URL starts with compact installer filename at " + scale);
                    dialog.ToggleUrl();
                    Assert(dialog.DisplayedUrl == "https://sawindowreleasedata.blob.core.windows.net/build.msi", "full URL can be revealed at " + scale);
                    dialog.SelectHelp();
                    Assert(dialog.FooterActionsFit, "Help footer stays visible at " + scale);
                    dialog.FitToArea(new Rectangle(0, 0, 480, 400));
                    dialog.SelectDiagnostics();
                    Pump();
                    Assert(dialog.FooterActionsFit, "very small Help footer fits at " + scale);
                    dialog.Close();
                }

            Assert(HelpDialog.ShortUrl("") == "No download link generated", "clear empty link text");
        }
        finally
        {
            Directory.Delete(folder, true);
        }

        Console.WriteLine(count + " lifecycle/layout checks passed.");
        return 0;
    }
}
