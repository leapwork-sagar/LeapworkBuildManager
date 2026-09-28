using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LeapworkBuildManager;

static class DisplayScaleTests
{
    sealed class Probe : DpiAwareForm
    {
        public Probe() { AutoScaleMode = AutoScaleMode.None; }
        public void Change(int dpi)
        {
            IntPtr rect = Marshal.AllocHGlobal(16);
            try
            {
                Marshal.Copy(new int[] { 10, 10, 610, 510 }, 0, rect, 4);
                var message = Message.Create(Handle, 0x02E0, (IntPtr)(dpi | dpi << 16), rect);
                base.WndProc(ref message);
            }
            finally { Marshal.FreeHGlobal(rect); }
        }
    }
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using (var probe = new Probe())
        {
            var button = new SoftButton { AutoSize = false, Bounds = new Rectangle(11, 13, 101, 33) };
            probe.Controls.Add(button);
            probe.Show();
            probe.Change(96);
            var original = button.Bounds;
            for (int i = 0; i < 8; i++)
            {
                probe.Change(144); probe.Change(192); probe.Change(120); probe.Change(96);
                if (button.Bounds != original) throw new Exception("Repeated DPI changes accumulated rounding drift");
            }
            var lazy = new Label { Bounds = new Rectangle(20, 20, 100, 24) };
            probe.Change(144); probe.Controls.Add(lazy); probe.Change(192); probe.Change(144);
            if (lazy.Bounds != new Rectangle(20, 20, 100, 24)) throw new Exception("Lazy control scale drift");
        }
        string folder = Path.Combine(Path.GetTempPath(), "DpiTests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            using (var form = new MainForm(Path.Combine(folder, "preferences.xml")))
            {
                form.Show();
                var ready = typeof(MainForm).GetField("preferencesLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
                UiTestPump.Until(() => (bool)ready.GetValue(form));
                foreach (float scale in new[] { 1.5f, 2f, 1f })
                {
                    form.ApplyDisplayScale(scale, new Rectangle(10, 10, 620, 600));
                    Application.DoEvents();
                    foreach (string name in new[] { "help", "minimize", "close" })
                    {
                        var action = (Control)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                        if (!action.Parent.ClientRectangle.Contains(action.Bounds)) throw new Exception("DPI change clipped title action: " + name);
                    }
                    var viewport = (Panel)typeof(MainForm).GetField("viewport", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    if (viewport.HorizontalScroll.Visible) throw new Exception("DPI change introduced horizontal scroll");
                }
                form.Close();
            }
            using (var help = new HelpDialog(null, "2026.2.257", "Ready", "https://example.invalid/build.msi", new DiagnosticEvent[0], "Synthetic report"))
            {
                help.Show(); help.SelectDiagnostics();
                foreach (float scale in new[] { 1.5f, 2f, 1f })
                {
                    help.ApplyDisplayScale(scale, new Rectangle(10, 10, 640, 660));
                    Application.DoEvents();
                    if (!help.FooterActionsFit) throw new Exception("DPI change clipped Help footer");
                }
            }
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("PASS live DPI message, repeated transitions, lazy controls and open dialogs");
    }
}
