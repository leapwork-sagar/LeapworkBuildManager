using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        public static string EstimateRemaining(long done, long? total, TimeSpan elapsed)
        {
            if (!total.HasValue || total.Value <= 0)
                return "Time remaining unavailable (unknown file size)";
            if (done >= total.Value)
                return "Finishing download…";
            if (done <= 0 || elapsed.TotalSeconds < 2)
                return "Estimating time remaining…";
            double seconds = (total.Value - done) / (done / elapsed.TotalSeconds);
            if (Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds > TimeSpan.MaxValue.TotalSeconds)
                return "Estimating time remaining…";
            if (seconds < 1)
                return "About 1 second remaining";
            var remaining = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            return remaining.TotalHours >= 1 ? String.Format("About {0}h {1}m remaining", (long)remaining.TotalHours, remaining.Minutes) : remaining.TotalMinutes >= 1 ? String.Format("About {0}m {1}s remaining", (int)remaining.TotalMinutes, remaining.Seconds) : String.Format("About {0}s remaining", remaining.Seconds);
        }

        bool ShowCloseWarning()
        {
            using (var dialog = new DpiAwareForm
            {
                Text = "Download in progress",
                AutoScaleMode = AutoScaleMode.None,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(480, 180),
                BackColor = BackColor,
                ForeColor = ForeColor,
                Font = Font
            }

            )
            {
                dialog.HandleCreated += delegate
                {
                    try
                    {
                        int dark = 1;
                        DwmSetWindowAttribute(dialog.Handle, 20, ref dark, 4);
                    }
                    catch (DllNotFoundException)
                    {
                    }
                    catch (EntryPointNotFoundException)
                    {
                    }
                };
                var title = new Label
                {
                    Text = "A download is still in progress",
                    Font = LauncherFont(12, FontStyle.Bold)
                };
                Place(dialog, title, 16, 16, 448, 30);
                var detail = new Label
                {
                    Text = "Closing will cancel the download and remove the partial file. Do you want to close the launcher?"
                };
                Place(dialog, detail, 16, 58, 448, 57);
                var keep = new SoftButton
                {
                    Text = "Keep downloading",
                    DialogResult = DialogResult.Cancel
                };
                var stop = new SoftButton
                {
                    Text = "Cancel download && close",
                    DialogResult = DialogResult.OK
                };
                Place(dialog, keep, 16, 132, 200, 32);
                Place(dialog, stop, 228, 132, 236, 32);
                dialog.AcceptButton = keep;
                dialog.CancelButton = keep;
                return dialog.ShowDialog(this) == DialogResult.OK;
            }
        }

        static Font LauncherFont(float size, FontStyle style)
        {
            return Theme.CreateFont(size, style);
        }

        static void Place(Control parent, Control child, int x, int y, int w, int h)
        {
            child.AutoSize = false;
            child.SetBounds(x, y, w, h);
            child.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            parent.Controls.Add(child);
        }

        static void DrawCombo(object sender, DrawItemEventArgs e)
        {
            var combo = (ComboBox)sender;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? Theme.Selection : combo.BackColor))
                e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 ? (combo.Items[e.Index] is RecentBuild ? HistoryFormatter.Format((RecentBuild)combo.Items[e.Index]) : combo.Items[e.Index].ToString()) : combo.Text;
            TextRenderer.DrawText(e.Graphics, text, combo.Font, Rectangle.Inflate(e.Bounds, -5, 0), combo.Enabled ? combo.ForeColor : Color.Gray, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, string text);
        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, 4);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }
}
