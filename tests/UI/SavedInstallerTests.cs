using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using LeapworkBuildManager;

static class SavedInstallerTests
{
    static int count;
    static void Check(bool value, string text) { if (!value) throw new Exception(text); count++; Console.WriteLine("PASS " + text); }
    [STAThread] static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "SavedInstaller-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "build.msi"); File.WriteAllText(file, "installer");
            var entry = new RecentBuild { Build = "2026.2.257", Type = "Early Access", DownloadPath = file, Outcome = HistoryOutcome.Downloaded };
            using (var dialog = new SavedInstallerDialog(entry))
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Func<string, object> get = name => typeof(SavedInstallerDialog).GetField(name, flags).GetValue(dialog);
                dialog.Show();
                UiTestPump.Until(() => !(bool)get("checking"));
                Check(((Button)get("open")).Enabled, "saved installer dialog enables Open folder when present");
                Check(!((Button)get("retry")).Visible, "successful local check hides Retry check");
                File.Delete(file);
                var task = (Task)typeof(SavedInstallerDialog).GetMethod("RefreshAsync", flags).Invoke(dialog, new object[] { true });
                UiTestPump.Until(() => task.IsCompleted); task.GetAwaiter().GetResult();
                Check(!((Button)get("open")).Enabled && ((Label)get("status")).Text.Contains("not found"), "Open folder rechecks a deleted file before opening Explorer");
                Check(entry.DownloadPath == file && entry.Outcome == HistoryOutcome.Downloaded, "missing installer leaves history data unchanged");
                dialog.Close();
            }
            using (var dialog = new SavedInstallerDialog(new RecentBuild { Build = "2026.2.257", Type = "Release", DownloadPath = "?:\\invalid\\build.msi" }))
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Func<string, object> get = name => typeof(SavedInstallerDialog).GetField(name, flags).GetValue(dialog);
                dialog.Show(); UiTestPump.Until(() => !(bool)get("checking"));
                Check(((Button)get("retry")).Visible && !((Button)get("open")).Enabled, "inaccessible location offers Retry check");
                Check(((Label)get("status")).Text.Contains("Couldn’t check"), "inaccessible message does not claim deletion");
                dialog.Close();
            }
            Console.WriteLine(count + " saved installer UI checks passed.");
        }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
        finally { Directory.Delete(dir, true); }
    }
}
