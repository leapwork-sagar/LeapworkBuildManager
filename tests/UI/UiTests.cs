using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using LeapworkBuildManager;

class UiHandler : HttpMessageHandler
{
    public int Mode;
    public int Calls, Active, MaxActive;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Interlocked.Increment(ref Calls);
        int active = Interlocked.Increment(ref Active);
        MaxActive = Math.Max(MaxActive, active);
        try
        {
            await Task.Delay(20, token);
        }
        finally
        {
            Interlocked.Decrement(ref Active);
        }

        if (Mode == 1)
            await Task.Delay(Timeout.Infinite, token);
        if (Mode == 5)
            throw new HttpRequestException("Connection failed");
        return new HttpResponseMessage(Mode == 2 ? HttpStatusCode.InternalServerError : (Mode == 3 || (Mode == 6 && !request.RequestUri.AbsoluteUri.Contains("EarlyAccess"))) ? HttpStatusCode.NotFound : Mode == 4 ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
        };
    }
}

class UiTests
{
    static int count;
    static MainForm form;
    static BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(string name)
    {
        if (name == "type" && !form.AdvancedControlsCreated)
            Call("EnsureAdvancedControls");
        var field = typeof(MainForm).GetField(name, flags);
        return field != null ? field.GetValue(form) : typeof(MainForm).GetProperty(name, flags).GetValue(form, null);
    }

    static object Call(string name, params object[] args)
    {
        if (name == "DownloadBuildAsync")
        {
            var c = (BuildController)Get("controller");
            if (!c.IsBusy)
                c.Select(new BuildMatch { BuildNumber = ((TextBox)Get("build")).Text, Kind = ((ComboBox)Get("type")).Text, Available = true, Url = (Uri)Get("current") });
        }

        return typeof(MainForm).GetMethod(name, flags).Invoke(form, args);
    }

    static void Assert(bool value, string label)
    {
        if (!value)
            throw new Exception(label);
        count++;
        Console.WriteLine("PASS " + label);
    }

    static void WaitUi(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    static void Pump(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Application.DoEvents();
            Thread.Sleep(5);
            if (watch.ElapsedMilliseconds > 5000)
                throw new Exception("UI task timeout");
        }

        task.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        var path = Path.Combine(Path.GetTempPath(), "BuildUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var handler = new UiHandler();
        try
        {
            form = new MainForm(Path.Combine(path, "prefs.xml"), new BuildService(handler));
            Assert(!(bool)Get("preferencesLoaded"), "preferences deferred until shown");
            form.Show();
            var loadWatch = Stopwatch.StartNew();
            while (!(bool)Get("preferencesLoaded"))
            {
                Application.DoEvents();
                Thread.Sleep(5);
                if (loadWatch.ElapsedMilliseconds > 5000)
                    throw new Exception("Preferences startup timeout");
            }

            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Assert(!form.MaximizeBox, "maximize disabled");
            Assert(((Label)Get("validation")).Text == "", "no empty-input error");
            Application.DoEvents();
            var build = (TextBox)Get("build");
            var recent = (ComboBox)Get("recent");
            var copy = (Button)Get("copy");
            var reset = (Button)Get("reset");
            var bar = (DownloadProgress)Get("bar");
            Assert(((TextBox)Get("url")).ReadOnly, "URL read-only");
            Assert(copy.Text == "Copy link", "Copy link label");
            Assert(recent.Text == "No recent builds yet", "empty history placeholder");
            Assert(((System.Windows.Forms.PictureBox)Get("brandLogo")).Image != null, "logo embedded");
            Assert(form.Text.StartsWith("Leapwork Build Manager"), "app renamed");
            Assert(!((Control)Get("progressCard")).Visible, "download panel hidden while idle");
            Assert(form.State == ApplicationState.Idle, "explicit idle state after startup");
            Assert(!((Control)Get("linkCard")).Visible && !((Control)Get("download")).Visible && !((Control)Get("historyCard")).Visible, "unused initial sections hidden");
            Assert(((Control)Get("resultsCard")).Height == 72 && !((Control)Get("matches")).Visible, "compact instructional empty state");
            Assert(((Control)Get("titleCaption")).Right < ((Control)Get("help")).Left && ((Control)Get("help")).Right < ((Control)Get("minimize")).Left && ((Control)Get("minimize")).Right < ((Control)Get("close")).Left, "title bar controls never overlap");
            var number = (BuildNumberBox)build;
            number.Clear();
            foreach (char digit in "20252200")
                number.InsertNumericText(digit.ToString());
            Assert(number.Text == "2025.2.200", "digits autoformat while typing");
            number.SelectAll();
            Assert(number.InsertNumericText("2026.2.257") && number.Text == "2026.2.257", "formatted paste accepted");
            Assert(!number.InsertNumericText("bad12") && number.Text == "2026.2.257", "letters rejected on paste");
            Assert(!number.InsertNumericText("2025.10.1"), "malformed dotted paste rejected");
            number.Select(5, 1);
            number.InsertNumericText("3");
            Assert(number.Text == "2026.3.257", "middle digit replacement preserves separators");
            number.Text = "2026.2.257";
            number.Select(5, 0);
            typeof(BuildNumberBox).GetMethod("OnKeyDown", flags).Invoke(number, new object[] { new KeyEventArgs(Keys.Back) });
            Assert(number.Text == "2022.2.57", "backspace skips generated separator");
            number.Text = "2026.2.257";
            number.Select(4, 0);
            typeof(BuildNumberBox).GetMethod("OnKeyDown", flags).Invoke(number, new object[] { new KeyEventArgs(Keys.Delete) });
            Assert(number.Text == "2026.2.57", "delete skips generated separator");
            number.Select(5, 1);
            number.InsertNumericText("5");
            Assert(!((Button)Get("check")).Enabled, "invalid quarter remains blocked");
            number.SelectAll();
            number.InsertNumericText("2026123");
            Assert(number.Text == "2026.1.23", "replace full selection");
            number.Clear();
            WaitUi(230);
            build.Text = "2026.1.329";
            Assert(!((Button)Get("download")).Enabled, "search required before download");
            ((ComboBox)Get("type")).SelectedItem = "Pre-release";
            Call("Generate");
            Call("UpdateControls");
            Assert(copy.Enabled, "copy enabled before download");
            handler.Mode = 1;
            var task = (Task)Call("DownloadBuildAsync", Path.Combine(path, "test.msi"), true);
            Application.DoEvents();
            var source = (CancellationTokenSource)Get("operation");
            Assert(!copy.Enabled && !reset.Enabled && !((Control)Get("historyCard")).Visible, "downloading controls and history");
            Assert(form.State == ApplicationState.Downloading, "explicit downloading state");
            Assert(((Label)Get("downloadIdentity")).Text.Contains("2026.1.329") && ((Label)Get("downloadIdentity")).Text.Contains("Pre-release"), "download identifies build and type");
            Assert((bool)Get("panelExpanded") && ((System.Windows.Forms.Timer)Get("expandTimer")).Enabled, "download starts expansion animation");
            WaitUi(250);
            Assert(!((Control)Get("resultsCard")).Visible && !((Control)Get("linkCard")).Visible, "download hides unused search and link sections");
            Assert(((Control)Get("progressCard")).Visible && ((Control)Get("progressCard")).Height == 228, "download panel expands fully");
            Call("ReportProgress", source, new DownloadProgressInfo(288L, 1470L, 0, 0));
            Assert(((Label)Get("percentage")).Text == "19.6%", "numeric download percentage");
            Call("CancelOperation");
            Assert(bar.Value == 0, "cancel immediately clears progress");
            Call("ReportProgress", source, new DownloadProgressInfo(800L, 1470L, 0, 0));
            Assert(bar.Value == 0, "queued progress ignored after cancellation");
            Pump(task);
            Console.WriteLine("reset=" + reset.Enabled + " history=" + ((Control)Get("historyCard")).Visible + " status=" + ((Label)Get("status")).Text);
            Assert(reset.Enabled && ((Control)Get("historyCard")).Visible && ((Label)Get("status")).Text == "Cancelled.", "cancel restores controls");
            handler.Mode = 0;
            Call("Generate");
            task = (Task)Call("DownloadBuildAsync", Path.Combine(path, "test.msi"), true);
            Pump(task);
            Assert(((Button)Get("download")).Text == "Download again", "completed action says Download again");
            Assert(!((Control)Get("copy")).Visible && !((Control)Get("eta")).Visible, "completed copy and repeated ETA message hidden");
            Assert(!((Control)Get("linkCard")).Visible, "URL starts collapsed");
            ((Button)Get("viewLink")).PerformClick();
            Assert(((Control)Get("linkCard")).Visible && typeof(MainForm).GetField("availability", flags) == null, "expanded URL has no duplicate availability");
            Assert(((Control)Get("folder")).Top == ((Control)Get("downloadIdentity")).Top && ((Control)Get("folder")).Bottom + 12 <= ((Control)Get("bar")).Top && ((Control)Get("folder")).Right == ((Control)Get("bar")).Right, "completion action aligns with build and clears progress bar");
            Assert(((Control)Get("historyHeading")).Visible, "recent history has permanent heading");
            ((Button)Get("viewLink")).PerformClick();
            Assert(bar.Value == 100 && ((Label)Get("percentage")).Text == "100%" && !copy.Enabled && ((Button)Get("folder")).Enabled, "completed download controls");
            Call("ReportProgress", source, new DownloadProgressInfo(20L, 100L, 0, 0));
            Assert(bar.Value == 100, "previous operation cannot overwrite completion");
            Assert(((Label)Get("validation")).Text.IndexOf("net", StringComparison.OrdinalIgnoreCase) < 0, "runtime hidden in build hint");
            Assert(recent.Items[1].ToString().IndexOf("net", StringComparison.OrdinalIgnoreCase) < 0, "runtime hidden in history");
            Assert(recent.Text == "Select a recent build…", "history placeholder");
            int historyCount = recent.Items.Count;
            Call("ResetState");
            Assert(build.Text == "" && ((TextBox)Get("url")).Text == "" && bar.Value == 0 && !((Button)Get("folder")).Enabled && recent.Items.Count == historyCount && File.Exists(Path.Combine(path, "test.msi")), "Reset preserves history and files, clears state");
            using (var historyChoice = new System.Windows.Forms.Timer { Interval = 20 })
            {
                historyChoice.Tick += delegate
                {
                    foreach (Form dialog in Application.OpenForms)
                        if (dialog is SavedInstallerDialog) { dialog.DialogResult = DialogResult.Retry; break; }
                };
                historyChoice.Start();
                recent.SelectedIndex = 1;
            }
            while (Get("operation") != null)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }

            ((ListBox)Get("matches")).SelectedIndex = 0;
            Assert(build.Text == "2026.1.329" && copy.Enabled, "history recall rechecks and selects link");
            handler.Mode = 2;
            task = (Task)Call("DownloadBuildAsync", Path.Combine(path, "failure.msi"), true);
            Pump(task);
            Assert(reset.Enabled && ((Control)Get("historyCard")).Visible && bar.Value == 0 && !((Button)Get("folder")).Enabled, "failure restores idle controls");
            handler.Mode = 0;
            task = (Task)Call("CheckBuildAsync");
            Pump(task);
            Assert(copy.Enabled, "new generated check unlocks copy");
            Assert(((Label)Get("status")).Text.Contains("available"), "available status message");
            foreach (var outcome in new[]
            {
                Tuple.Create(3, "Build not found"),
                Tuple.Create(4, "Access denied"),
                Tuple.Create(5, "Connection failed")
            }

            )
            {
                handler.Mode = outcome.Item1;
                Pump((Task)Call("CheckBuildAsync"));
                Assert(((Label)Get("status")).Text.Contains(outcome.Item2), "visible outcome " + outcome.Item2);
            }

            Call("ResetState");
            Assert(((Label)Get("status")).Text == "Ready.", "reset clears availability");
            Assert(MainForm.EstimateRemaining(100, 1000, TimeSpan.FromSeconds(10)) == "About 1m 30s remaining", "ETA calculation");
            Assert(MainForm.EstimateRemaining(100, null, TimeSpan.FromSeconds(10)).Contains("unavailable"), "unknown-size ETA");
            Assert(MainForm.EstimateRemaining(0, 1000, TimeSpan.FromSeconds(10)).Contains("Estimating"), "zero-speed ETA");
            Assert(MainForm.EstimateRemaining(1, 1000, TimeSpan.FromSeconds(1)).Contains("Estimating"), "ETA warmup");
            Assert(MainForm.EstimateRemaining(1000, 1000, TimeSpan.FromSeconds(10)).Contains("Finishing"), "completed ETA");
            handler.Mode = 0;
            handler.Calls = handler.MaxActive = 0;
            build.Text = "2026.2.257";
            Pump((Task)Call("FindBuildsAsync"));
            Assert(((ListBox)Get("matches")).Items.Count == 5 && handler.Calls == 5, "all five modern build types searched once");
            Assert(handler.MaxActive <= 3 && handler.MaxActive > 1, "bounded parallel search");
            Assert(((ListBox)Get("matches")).SelectedIndex == -1 && !((Button)Get("download")).Enabled, "multiple matches require selection");
            ((ListBox)Get("matches")).SelectedIndex = 1;
            Assert(((TextBox)Get("url")).Text.Contains("EarlyAccess") && ((Button)Get("download")).Enabled, "selected result drives download URL");
            Assert(((ListBox)Get("matches")).Items[0].ToString().IndexOf("net", StringComparison.OrdinalIgnoreCase) < 0, "runtime hidden in search results");
            handler.Mode = 6;
            Pump((Task)Call("FindBuildsAsync"));
            Assert(((ListBox)Get("matches")).Items.Count == 1 && ((ListBox)Get("matches")).SelectedIndex == 0 && ((Button)Get("download")).Enabled, "single match auto-selected");
            handler.Mode = 3;
            Pump((Task)Call("FindBuildsAsync"));
            Assert(((ListBox)Get("matches")).Items.Count == 0 && ((Label)Get("status")).Text.Contains("No matching") && !((Button)Get("download")).Enabled, "no matching downloads");
            handler.Mode = 5;
            Pump((Task)Call("FindBuildsAsync"));
            Console.WriteLine("SEARCH SUMMARY=" + ((Label)Get("resultSummary")).Text + " STATUS=" + ((Label)Get("status")).Text);
            Assert(((Label)Get("resultSummary")).Text.Contains("could not be verified"), "network failures not misreported as missing");
            handler.Mode = 0;
            handler.Calls = 0;
            build.Text = "2025.2.999";
            Pump((Task)Call("FindBuildsAsync"));
            Assert(handler.Calls == 4 && (int)Get("inferredRuntime") == 1, "older build searches legacy runtime only");
            foreach (BuildMatch match in ((ListBox)Get("matches")).Items)
                Assert(!match.Url.AbsoluteUri.Contains("NETCore"), "legacy URL pattern");
            build.Text = "2025.3.0";
            Assert((int)Get("inferredRuntime") == 0 && !((Button)Get("download")).Enabled, "cutoff inferred and stale result cleared");
            handler.Mode = 1;
            var search = (Task)Call("FindBuildsAsync");
            Call("CancelOperation");
            Pump(search);
            Assert(((Label)Get("resultSummary")).Text.Contains("cancelled") && !((Button)Get("download")).Enabled, "search cancellation safe");
            ((Button)Get("advanced")).PerformClick();
            Assert((bool)Get("advancedMode") && typeof(MainForm).GetField("runtime", flags) == null, "advanced mode with inferred runtime");
            ((Button)Get("advanced")).PerformClick();
            handler.Mode = 0;
            build.Text = "2026.2.257";
            Call("Generate");
            Call("UpdateControls");
            Assert(!((Button)Get("download")).Enabled, "manual generated URL is not verified");
            handler.Mode = 3;
            Pump((Task)Call("CheckBuildAsync"));
            Assert(!((Button)Get("download")).Enabled, "manual not-found blocks download");
            handler.Mode = 5;
            Pump((Task)Call("CheckBuildAsync"));
            Assert(!((Button)Get("download")).Enabled, "manual connection failure blocks download");
            handler.Mode = 0;
            Pump((Task)Call("CheckBuildAsync"));
            Assert(((Button)Get("download")).Enabled, "manual success enables download");
            Assert(((Button)Get("download")).Text.Contains("KB"), "manual verified download shows size");
            build.Text = "2026.2.258";
            Assert(!((Button)Get("download")).Enabled, "edit invalidates manual verification");
            var estimator = new RecentSpeed();
            double first = estimator.Update(1000, 1);
            double slower = estimator.Update(1100, 2);
            Assert(first == 1000 && slower < first && slower > 100, "recent speed smooths slowdown");
            double faster = estimator.Update(5100, 3);
            Assert(faster > slower && faster < 4000, "recent speed smooths recovery");
            estimator.Reset();
            Assert(estimator.Update(0, 0) == 0, "speed reset");
            Assert(build.AccessibleName == "Build number" && ((ListBox)Get("matches")).AccessibleName == "Available downloads", "screen reader names");
            form.WorkingAreaProvider = () => new System.Drawing.Rectangle(0, 0, 480, 400);
            Call("ReflowDownloadPanel");
            WaitUi(230);
            Assert(((Control)Get("titleCaption")).Right < ((Control)Get("help")).Left && ((Control)Get("close")).Right <= form.ClientSize.Width, "small-screen titlebar remains separated");
            Assert(form.Width <= 480 && form.Height <= 400, "small-screen window bounds");
            Assert(((Panel)Get("viewport")).AutoScroll && ((Panel)Get("canvas")).Width <= ((Panel)Get("viewport")).ClientSize.Width, "small viewport fits content without horizontal scrolling");
            form.WorkingAreaProvider = () => Screen.FromControl(form).WorkingArea;
            Call("ReflowDownloadPanel");
            Assert(form.Height <= Screen.FromControl(form).WorkingArea.Height, "window fits working area");
            Assert(form.DiagnosticText().Contains(VersionInfo.Display) && form.DiagnosticText().Contains("Connection"), "diagnostics include version and failed-check details");
            int notices = 0;
            form.CompletionNotice = message =>
            {
                notices++;
            };
            Call("Generate");
            handler.Mode = 0;
            Pump((Task)Call("CheckBuildAsync"));
            form.WindowState = FormWindowState.Minimized;
            Pump((Task)Call("DownloadBuildAsync", Path.Combine(path, "notice.msi"), true));
            Assert(notices == 1, "minimized successful download sends completion notification");
            form.WindowState = FormWindowState.Normal;
            handler.Mode = 2;
            Pump((Task)Call("DownloadBuildAsync", Path.Combine(path, "notice-failure.msi"), true));
            Assert(notices == 1, "no completion notification on failure");
            ((Button)Get("clearHistory")).PerformClick();
            Assert(((Preferences)Get("preferences")).History.Count == 0 && !((Control)Get("historyCard")).Visible, "clear history removes records and hides empty section");
            build.Text = "2026.1.329";
            Call("Generate");
            handler.Mode = 1;
            task = (Task)Call("DownloadBuildAsync", Path.Combine(path, "close-test.msi"), true);
            var active = (CancellationTokenSource)Get("operation");
            int prompts = 0;
            form.ConfirmCloseDownload = () =>
            {
                prompts++;
                return false;
            };
            form.Close();
            Assert(prompts == 1 && !form.IsDisposed && !active.IsCancellationRequested, "reject close keeps download running");
            form.ConfirmCloseDownload = () =>
            {
                prompts++;
                return true;
            };
            form.Close();
            Pump(task);
            var closeWait = Stopwatch.StartNew();
            while (!form.IsDisposed && closeWait.ElapsedMilliseconds < 5000)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }

            Assert(prompts == 2 && form.IsDisposed && !File.Exists(Path.Combine(path, "close-test.msi")), "confirm close cancels and closes safely");
            Console.WriteLine(count + " UI checks passed.");
        }
        finally
        {
            if (form != null)
                form.Dispose();
            Directory.Delete(path, true);
        }
    }
}
