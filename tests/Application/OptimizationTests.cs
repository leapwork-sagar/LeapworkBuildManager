using System;
using System.Collections.Generic;
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

sealed class OptimizationHttp : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) });
    }
}

static class OptimizationTests
{
    static int checks;
    static BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Assert(bool value, string text)
    {
        if (!value)
            throw new Exception(text);
        checks++;
        Console.WriteLine("PASS " + text);
    }

    static object Get(MainForm form, string name)
    {
        return typeof(MainForm).GetField(name, flags).GetValue(form);
    }

    static object Call(MainForm form, string name, params object[] args)
    {
        return typeof(MainForm).GetMethod(name, flags).Invoke(form, args);
    }

    static void Pump(int ms)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < ms)
        {
            Application.DoEvents();
            Thread.Sleep(4);
        }
    }

    static void Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Pump(5);
            if (clock.ElapsedMilliseconds > 8000)
                throw new Exception("UI timeout");
        }

        Application.DoEvents();
    }

    static Preferences Initial()
    {
        return new Preferences
        {
            SchemaVersion = Preferences.CurrentVersion,
            Build = "2026.2.257",
            Type = "Experimental",
            Runtime = 0
        };
    }

    [STAThread]
    static void Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "OptimizationTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Persistence(directory).GetAwaiter().GetResult();
            Ui(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }

        Console.WriteLine(checks + " optimization checks passed.");
    }

    static async Task Persistence(string directory)
    {
        var initial = Initial();
        int writes = 0;
        Preferences last = null;
        var writer = new SettingsWriter(initial, p =>
        {
            writes++;
            last = PreferencesContent.Copy(p);
            return p.Revision + 1;
        });
        Assert(!writer.Request(initial), "unchanged settings do not queue a save");
        await writer.FlushAsync();
        Assert(writes == 0, "unchanged flush does no disk work");
        var changed = PreferencesContent.Copy(initial);
        changed.Build = "2026.2.258";
        writer.Request(changed);
        changed.Build = "2026.2.259";
        writer.Request(changed);
        changed.Build = "2026.2.260";
        writer.Request(changed);
        await writer.FlushAsync();
        Assert(writes == 1 && last.Build == "2026.2.260", "several pending changes write only latest snapshot");
        Assert(!writer.IsDirty && !writer.Request(changed), "successful save clears dirty state");
        changed.History.Add(new RecentBuild { Build = changed.Build, Type = "Release", LastSearchedUtc = DateTime.UtcNow });
        writer.Request(changed);
        await writer.FlushAsync();
        Assert(writes == 2 && last.Revision == 1, "next save uses committed revision");
        changed.History[0].LastSearchedUtc = changed.History[0].LastSearchedUtc.Value.AddSeconds(1);
        Assert(writer.Request(changed), "history timestamp changes count as real changes");
        var frozen = PreferencesContent.Copy(changed);
        changed.History[0].Build = "mutated after request";
        await writer.FlushAsync();
        Assert(last.History[0].Build == frozen.History[0].Build, "pending settings snapshot does not share mutable history");
        bool fail = true;
        var retry = new SettingsWriter(initial, p =>
        {
            if (fail)
                throw new IOException("locked");
            return p.Revision + 1;
        });
        retry.Request(changed);
        try
        {
            await retry.FlushAsync();
            throw new Exception("Expected save failure");
        }
        catch (IOException)
        {
            Assert(retry.IsDirty, "save failure retains dirty changes");
        }

        fail = false;
        await retry.FlushAsync();
        Assert(!retry.IsDirty, "later successful save clears failed state");
        var migration = Initial();
        migration.NeedsSave = true;
        int migrationWrites = 0;
        var migrate = new SettingsWriter(migration, p =>
        {
            migrationWrites++;
            return p.Revision + 1;
        });
        await migrate.FlushAsync();
        await migrate.FlushAsync();
        Assert(migrationWrites == 1, "migration forces exactly one unchanged-content save");
        migration.IsReadOnly = true;
        var readOnly = new SettingsWriter(migration, p =>
        {
            throw new Exception("Must not write");
        });
        await readOnly.FlushAsync();
        Assert(!readOnly.Request(changed) && !readOnly.IsDirty, "read-only initial snapshot cannot be replaced by writable input");
        // A later normal value is not used for real read-only settings; check the actual caller path.
        Assert(!new SettingsWriter(migration, p => 0).Request(migration), "future-schema preferences never queue a save");
        using (var entered = new ManualResetEventSlim())
        using (var release = new ManualResetEventSlim())
        {
            int calls = 0;
            var revisions = new List<long>();
            string finalBuild = null;
            var serial = new SettingsWriter(initial, p =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    entered.Set();
                    release.Wait(3000);
                }

                revisions.Add(p.Revision);
                finalBuild = p.Build;
                return p.Revision + 1;
            });
            var a = PreferencesContent.Copy(initial);
            a.Build = "2026.2.300";
            serial.Request(a);
            var first = serial.FlushAsync();
            Assert(entered.Wait(2000), "background writer starts without blocking caller");
            a.Build = "2026.2.301";
            serial.Request(a);
            var second = serial.FlushAsync();
            release.Set();
            await Task.WhenAll(first, second);
            Assert(calls == 2 && finalBuild == a.Build && revisions[0] == 0 && revisions[1] == 1, "overlapping saves serialize and retain newest revision");
        }

        string path = Path.Combine(directory, "real-settings.xml");
        PreferenceStore.Save(path, initial);
        string before = File.ReadAllText(path);
        var disk = new SettingsWriter(path, PreferenceStore.Load(path));
        await disk.FlushAsync();
        Assert(File.ReadAllText(path) == before, "unchanged real settings file is not rewritten");
    }

    static void Ui(string directory)
    {
        Application.EnableVisualStyles();
        Control.CheckForIllegalCrossThreadCalls = true;
        string path = Path.Combine(directory, "ui.xml");
        PreferenceStore.Save(path, Initial());
        string before = File.ReadAllText(path);
        using (var form = new MainForm(path, new BuildService(new OptimizationHttp())))
        {
            form.Show();
            Until(() => (bool)Get(form, "startupCentered"));
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Assert(form.FullLayoutPasses == 2, "startup uses two full layout passes");
            Pump(250);
            Assert(form.AnimationFrames == 0 && !((System.Windows.Forms.Timer)Get(form, "expandTimer")).Enabled, "already-collapsed startup panel does not animate");
            int layouts = form.FullLayoutPasses;
            Call(form, "SetDownloadPanel", false);
            Assert(form.FullLayoutPasses == layouts, "repeated hidden-panel request does no layout work");
            form.Close();
            Until(() => form.IsDisposed);
        }

        Assert(File.ReadAllText(path) == before, "open and close without edits does not save settings");
        using (var form = new MainForm(path, new BuildService(new OptimizationHttp())))
        {
            form.Show();
            Until(() => (bool)Get(form, "startupCentered"));
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            var build = (TextBox)Get(form, "build");
            build.Text = "2026.2.400";
            build.Text = "2026.2.401";
            build.Text = "2026.2.402";
            Assert(File.ReadAllText(path) == before, "rapid typing does not synchronously write settings");
            Until(() => PreferenceStore.Load(path).Build == "2026.2.402");
            Assert(PreferenceStore.Load(path).Revision == 2, "rapid edits coalesce into one delayed commit");
            build.Text = "2026.2.403";
            form.Close();
            Until(() => form.IsDisposed);
            Assert(PreferenceStore.Load(path).Build == "2026.2.403", "normal exit flushes latest pending edit");
        }

        using (var form = new MainForm(Path.Combine(directory, "animation.xml"), new BuildService(new OptimizationHttp())))
        {
            form.Show();
            Until(() => (bool)Get(form, "startupCentered"));
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ((TextBox)Get(form, "build")).Text = "2026.2.257";
            var controller = (BuildController)Get(form, "controller");
            controller.Select(new BuildMatch { BuildNumber = "2026.2.257", Kind = "Release", Available = true, Url = new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi") });
            controller.Transition(ApplicationState.Downloading);
            Call(form, "SetDownloadPanel", true);
            var timer = (System.Windows.Forms.Timer)Get(form, "expandTimer");
            int passes = form.FullLayoutPasses;
            var bounds = form.Bounds;
            bool stable = true;
            timer.Tick += (s, e) =>
            {
                if (timer.Enabled && form.Bounds != bounds)
                    stable = false;
            };
            Pump(35);
            int partial = (int)Get(form, "panelHeight");
            Call(form, "SetDownloadPanel", true);
            Assert(form.FullLayoutPasses == passes, "same animation target does not restart or reflow");
            Until(() => !timer.Enabled);
            Assert(form.AnimationFrames > 0 && form.FullLayoutPasses <= passes + 1, "animation frames avoid full-window layout");
            Assert(stable, "window bounds remain stable during expansion frames");
            Assert(((Control)Get(form, "progressCard")).Height == 228, "expansion ends at intended panel height");
            Call(form, "SetDownloadPanel", false);
            Pump(35);
            int middle = (int)Get(form, "panelHeight");
            Call(form, "SetDownloadPanel", true);
            Assert((int)Get(form, "expansionStart") == middle, "reversed animation starts from current height");
            controller.Transition(ApplicationState.Completed);
            Call(form, "SetDownloadPanel", true);
            Assert(!timer.Enabled && (int)Get(form, "panelHeight") == 228, "completion switches immediately without replaying expansion");
            var card = (Card)Get(form, "progressCard");
            bool redraw = (bool)typeof(Control).GetMethod("GetStyle", flags).Invoke(card, new object[] { ControlStyles.ResizeRedraw });
            Assert(redraw, "cards repaint their full surface when resized");
            bool invalidated = false;
            card.Invalidated += (s, e) =>
            {
                if (e.InvalidRect.Width >= card.Width && e.InvalidRect.Height >= card.Height)
                    invalidated = true;
            };
            card.Height += 1;
            Assert(invalidated, "resizing invalidates old border across full card");
            form.Close();
            Until(() => form.IsDisposed);
        }

        int probes = 0;
        var preparation = new DownloadPreparationService(p => false, p =>
        {
            Interlocked.Increment(ref probes);
            return long.MaxValue;
        });
        using (var form = new MainForm(Path.Combine(directory, "destination.xml"), new BuildService(new OptimizationHttp(), preparation)))
        {
            form.Show();
            Until(() => (bool)Get(form, "startupCentered"));
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ((TextBox)Get(form, "build")).Text = "2026.2.257";
            ((BuildController)Get(form, "controller")).Select(new BuildMatch { BuildNumber = "2026.2.257", Kind = "Release", Available = true, SizeBytes = 3, Url = new Uri("https://sawindowreleasedata.blob.core.windows.net/a.msi") });
            var download = (Task)Call(form, "PrepareAndDownloadAsync", Path.Combine(directory, "download.msi"));
            Until(() => download.IsCompleted);
            download.GetAwaiter().GetResult();
            Assert(probes == 2, "unchanged destination uses initial and final response-size checks only");
            form.Close();
            Until(() => form.IsDisposed);
        }

        foreach (int width in new[]
        {
            620,
            480
        }

        )
            using (var dialog = new ExistingInstallerDialog(Path.Combine(directory, "Leapwork_NETCore_EarlyAccess_x64_2026.2.257.msi")))
            {
                dialog.Show();
                Pump(30);
                dialog.FitToArea(new Rectangle(0, 0, width, 400));
                Pump(20);
                Assert(dialog.FooterActionsFit, "existing-installer footer fits width " + width);
                Assert(dialog.FormBorderStyle == FormBorderStyle.None, "dialog uses app-style title bar at " + width);
                Assert(((Button)dialog.AcceptButton).Text == "Save another copy" && ((Button)dialog.AcceptButton).BackColor != ((Button)dialog.CancelButton).BackColor, "safe-copy action has distinct primary styling at " + width);
                Assert(dialog.Destination.EndsWith("2026.2.257.msi"), "dialog retains complete installer destination at " + width);
                dialog.Close();
            }
    }
}
