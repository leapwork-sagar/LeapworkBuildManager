using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Test-only entry point: capture application windows, never the desktop.
internal static class TestRunner
{
    static readonly List<Bitmap> snapshots = new List<Bitmap>();
    static readonly Stopwatch captureClock = Stopwatch.StartNew();
    static string directory;

    [STAThread]
    static int Main(string[] args)
    {
        directory = Environment.GetEnvironmentVariable("TEST_ARTIFACT_DIR");
        string suite = Environment.GetEnvironmentVariable("TEST_SUITE");
        if (String.IsNullOrEmpty(suite)) throw new InvalidOperationException("TEST_SUITE is required");
        var captureTimer = new Timer { Interval = 500 };
        captureTimer.Tick += CaptureWindows;
        captureTimer.Start();
        Application.Idle += CaptureWindows;
        try
        {
            var type = Assembly.GetExecutingAssembly().GetType(suite, true);
            var main = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object result = main.Invoke(null, main.GetParameters().Length == 0 ? null : new object[] { args });
            int code = result is int ? (int)result : Environment.ExitCode;
            if (code != 0) SaveFailure();
            return code;
        }
        catch (Exception error)
        {
            var invocation = error as TargetInvocationException;
            Console.Error.WriteLine(invocation != null ? invocation.InnerException : error);
            SaveFailure();
            return 1;
        }
        finally
        {
            captureTimer.Stop();
            captureTimer.Dispose();
            Application.Idle -= CaptureWindows;
            ClearSnapshots();
        }
    }

    static void CaptureWindows(object sender, EventArgs args)
    {
        if (String.IsNullOrEmpty(directory) || snapshots.Count > 0 && captureClock.ElapsedMilliseconds < 500) return;
        captureClock.Restart();
        var next = new List<Bitmap>();
        foreach (Form form in Application.OpenForms)
        {
            if (!form.Visible || form.Width < 1 || form.Height < 1 || form.IsDisposed) continue;
            try
            {
                var bitmap = new Bitmap(form.Width, form.Height);
                try { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); }
                catch { bitmap.Dispose(); throw; }
                next.Add(bitmap);
                if (next.Count == 3) break;
            }
            catch (Exception error) { Console.WriteLine("Screenshot unavailable: " + error.GetType().Name); }
        }
        // Keep the last visible test windows if failure occurs after their disposal.
        if (next.Count == 0) return;
        ClearSnapshots();
        snapshots.AddRange(next);
    }

    static void SaveFailure()
    {
        if (String.IsNullOrEmpty(directory)) return;
        try
        {
            CaptureWindows(null, EventArgs.Empty);
            Directory.CreateDirectory(directory);
            for (int i = 0; i < snapshots.Count; i++)
                snapshots[i].Save(Path.Combine(directory, "failure-window-" + (i + 1) + ".png"), ImageFormat.Png);
            File.WriteAllText(Path.Combine(directory, "capture-info.txt"),
                "Last available test-window captures: " + snapshots.Count + Environment.NewLine +
                "OS: " + Environment.OSVersion + Environment.NewLine + "CLR: " + Environment.Version);
        }
        catch (Exception error) { Console.Error.WriteLine("Could not save failure screenshots: " + error.Message); }
    }

    static void ClearSnapshots()
    {
        foreach (var snapshot in snapshots) snapshot.Dispose();
        snapshots.Clear();
    }
}
