using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        void ShowCompletionNotice(string description)
        {
            try
            {
                notification.Visible = true;
                notification.BalloonTipTitle = "Download complete";
                notification.BalloonTipText = description + ". Click to open the app.";
                notification.ShowBalloonTip(8000);
            }
            catch (Exception e)
            {
                RecordDiagnostic("Notification unavailable", "Warning", "Notification", e);
            }
        }

        void RecordDiagnostic(string message)
        {
            RecordDiagnostic(message, "Information", diagnosticCategory, null);
        }

        void RecordDiagnostic(string message, string severity, string category, Exception error)
        {
            var item = new DiagnosticEvent(DateTime.Now, message, build.Text, current, severity, category, category == "Settings" ? "" : diagnosticOperation, error);
            PublishDiagnostic(item);
        }

        public string DiagnosticText()
        {
            return DiagnosticReport.Format(build.Text, status.Text, current, diagnostics, persistentLog);
        }

        void ShowHelp()
        {
            Control previousFocus = ActiveControl;
            using (var dialog = new HelpDialog(brandLogo.Image, build.Text, status.Text, current == null ? "" : current.AbsoluteUri, diagnostics.ToArray(), DiagnosticText()))
                dialog.ShowDialog(this);
            if (!IsDisposed && previousFocus != null && previousFocus.CanFocus) previousFocus.Focus();
        }

        static void Announce(Control control)
        {
            if (control.Visible && control.Parent != null && control.IsHandleCreated)
                NotifyWinEvent(0x800C, control.Handle, -4, 0);
        }

        [DllImport("user32.dll")]
        static extern void NotifyWinEvent(uint ev, IntPtr handle, int objectId, int childId);
    }
}
