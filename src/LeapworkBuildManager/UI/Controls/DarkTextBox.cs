using System;
using System.Drawing;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public class DarkTextBox : TextBox
    {
        public string Placeholder { get; set; }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg == 0xF || m.Msg == 0x318 || m.Msg == 0x317) && Text.Length == 0 && !String.IsNullOrEmpty(Placeholder))
            {
                if ((m.Msg == 0x318 || m.Msg == 0x317) && m.WParam != IntPtr.Zero)
                {
                    using (var g = Graphics.FromHdc(m.WParam))
                        DrawPlaceholder(g);
                }
                else
                    using (var g = Graphics.FromHwnd(Handle))
                        DrawPlaceholder(g);
            }
        }

        void DrawPlaceholder(Graphics g)
        {
            using (var background = new SolidBrush(BackColor))
                g.FillRectangle(background, ClientRectangle);
            TextRenderer.DrawText(g, Placeholder, Font, ClientRectangle, Theme.Placeholder, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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
