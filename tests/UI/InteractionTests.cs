using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using LeapworkBuildManager;

static class InteractionTests
{
    static int count;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
    [STAThread] static void Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Interaction-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        using (var form = new MainForm(Path.Combine(directory, "settings.xml"), new BuildService()))
        {
            Func<string, object> get = name => typeof(MainForm).GetField(name, flags).GetValue(form);
            Action<string, object[]> call = (name, args) => typeof(MainForm).GetMethod(name, flags).Invoke(form, args);
            form.Show();
            UiTestPump.Until(() => (bool)get("preferencesLoaded"));
            var build = (TextBox)get("build");
            var reset = (Button)get("reset");
            var tips = (ToolTip)get("detailsTip");
            string folder = @"C:\Users\VeryLongUserName\OneDrive - Example Organisation\Downloads";
            string shortPath = DestinationText.Fit(folder, form.Font, 210);
            Check(shortPath.Contains("…") && shortPath.EndsWith("Downloads"), "path shortening retains destination folder");
            Check(DestinationText.Fit(@"C:\Temp", form.Font, 800) == @"C:\Temp", "short paths remain unchanged");
            call("SetDestination", new object[] { folder + @"\setup.msi", false });
            var path = (Label)get("destinationLabel");
            Check(tips.GetToolTip(path) == folder && path.AccessibleDescription == folder, "full destination retained for hover and accessibility");
            Check(tips.GetToolTip(reset).Contains("Keep saved history"), "reset tooltip explains retained preferences");
            Check(tips.GetToolTip((Control)get("advanced")).Contains("only"), "manual search tooltip explains scope");
            Check(tips.GetToolTip((Control)get("copy")).Contains("complete"), "copy link tooltip explains complete address");
            call("ResetState", new object[0]);
            Check(build.Focused && path.Text == "", "reset focuses build field and clears destination");
            form.Activate(); Application.DoEvents();
            int revision = (int)get("interactionRevision");
            reset.Focus();
            call("FocusAfterOperation", new object[] { build, revision - 1 });
            Check(reset.Focused, "stale completion cannot steal keyboard focus");
            call("FocusAfterOperation", new object[] { build, revision });
            Check(!build.Focused || Form.ActiveForm == form, "automatic focus is restricted to the active window");
            Check(FocusPolicy.CanAdvance(true, false, false, 2, 2), "active unchanged interaction permits focus");
            Check(!FocusPolicy.CanAdvance(false, false, false, 2, 2), "background window cannot advance focus");
            Check(!FocusPolicy.CanAdvance(true, true, false, 2, 2), "minimized window cannot advance focus");
            Check(!FocusPolicy.CanAdvance(true, false, true, 2, 2), "closing window cannot advance focus");
            Check(!FocusPolicy.CanAdvance(true, false, false, 2, 3), "user navigation prevents completion focus");
            build.Text = "2";
            var validation = (System.Threading.Tasks.Task)typeof(MainForm).GetMethod("FindBuildsAsync", flags).Invoke(form, null);
            validation.GetAwaiter().GetResult();
            Check(build.Focused, "invalid submission returns focus to input");
            using (var other = new Form())
            {
                other.Show(); other.Activate(); Application.DoEvents();
                call("FocusAfterOperation", new object[] { build, (int)get("interactionRevision") });
                Check(Form.ActiveForm != form, "completion does not activate a background app");
                other.Close();
            }
            form.Close(); UiTestPump.Until(() => form.IsDisposed);
        }
        Directory.Delete(directory, true);
        Console.WriteLine(count + " interaction checks passed.");
    }
}
