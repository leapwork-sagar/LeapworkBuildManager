using System;
using System.Drawing;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed class DarkComboBox : ComboBox
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0xF || m.Msg == 0x318 || m.Msg == 0x317)
            {
                if ((m.Msg == 0x318 || m.Msg == 0x317) && m.WParam != IntPtr.Zero)
                {
                    using (var g = Graphics.FromHdc(m.WParam))
                        PaintChrome(g);
                }
                else
                    using (var g = Graphics.FromHwnd(Handle))
                        PaintChrome(g);
            }
        }

        void PaintChrome(Graphics g)
        {
            int arrowWidth = SystemInformation.VerticalScrollBarWidth + 4;
            using (var b = new SolidBrush(BackColor))
                g.FillRectangle(b, Width - arrowWidth, 0, arrowWidth, Height);
            using (var p = new Pen(Focused ? Theme.Focus : Theme.InputBorder))
            {
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
            }

            int x = Width - arrowWidth / 2, y = Height / 2;
            using (var p = new Pen(Enabled ? ForeColor : Theme.DisabledText, 1.5f))
                g.DrawLines(p, new[] { new Point(x - 4, y - 2), new Point(x, y + 2), new Point(x + 4, y - 2) });
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }
    }
}
