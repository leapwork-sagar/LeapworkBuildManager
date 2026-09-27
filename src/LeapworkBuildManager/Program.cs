using System;
using System.Net;
using System.Windows.Forms;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Leapwork Build Manager")]
[assembly: AssemblyProduct("Leapwork Build Manager")]
[assembly: AssemblyDescription("Find and download Leapwork installer builds")]
namespace LeapworkBuildManager
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            // Retain the activation key so v1.13/v1.14 and renamed releases share one instance.
            using (var instance = new SingleInstance("LeapworkBuildDownloader-" + user))
            {
                if (!instance.IsPrimary)
                {
                    var current = System.Diagnostics.Process.GetCurrentProcess();
                    foreach (var name in new[]
                    {
                        current.ProcessName,
                        "BuildDownloadPathRetrieval"
                    }

                    )
                        foreach (var process in System.Diagnostics.Process.GetProcessesByName(name))
                            using (process)
                            {
                                if (process.Id != current.Id)
                                    AllowSetForegroundWindow(process.Id);
                            }

                    instance.ActivateExisting();
                    return;
                }

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new MainForm())
                {
                    var handle = form.Handle;
                    instance.Listen(delegate
                    {
                        if (form.IsDisposed || !form.IsHandleCreated)
                            return;
                        try
                        {
                            form.BeginInvoke(new Action(delegate
                            {
                                if (form.IsDisposed)
                                    return;
                                form.Show();
                                if (form.WindowState == FormWindowState.Minimized)
                                    form.WindowState = FormWindowState.Normal;
                                form.BringToFront();
                                form.Activate();
                                SetForegroundWindow(form.Handle);
                            }));
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    });
                    Application.Run(form);
                }
            }
        }

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int processId);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr window);
    }
}
