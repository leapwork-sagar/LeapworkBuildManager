using System;
using System.Drawing;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed class DownloadProgress : Control
    {
        int value;
        ProgressBarStyle style;
        public DownloadProgress()
        {
            DoubleBuffered = true;
            BackColor = Theme.ProgressTrack;
        }

        public int Value
        {
            get
            {
                return value;
            }

            set
            {
                this.value = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public ProgressBarStyle Style
        {
            get
            {
                return style;
            }

            set
            {
                style = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var brush = new SolidBrush(Theme.Accent))
                e.Graphics.FillRectangle(brush, 0, 0, style == ProgressBarStyle.Marquee ? Width / 3 : Width * value / 100, Height);
        }
    }
}
