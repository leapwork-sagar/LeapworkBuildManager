using System;
using System.Drawing;

namespace LeapworkBuildManager
{
    public sealed class WindowLayoutPlan
    {
        public Size WindowSize { get; private set; }
        public int CanvasWidth { get; private set; }
        public int ContentHeight { get; private set; }
        public bool VerticalScroll { get; private set; }

        public static WindowLayoutPlan Calculate(Rectangle area, float scale, int logicalHeight, int scrollbarWidth)
        {
            int title = UiScale.Pixels(34, scale);
            int height = UiScale.Pixels(logicalHeight, scale);
            bool scroll = height > Math.Max(1, area.Height - title);
            int width = Math.Min(area.Width, UiScale.Pixels(620, scale) + (scroll ? scrollbarWidth : 0));
            return new WindowLayoutPlan
            {
                WindowSize = new Size(width, Math.Min(area.Height, height + title)),
                CanvasWidth = Math.Max(1, width - (scroll ? scrollbarWidth : 0)),
                ContentHeight = height,
                VerticalScroll = scroll
            };
        }

        public static Point Center(Rectangle area, Size window)
        {
            return new Point(area.Left + Math.Max(0, (area.Width - window.Width) / 2), area.Top + Math.Max(0, (area.Height - window.Height) / 2));
        }
    }
}
