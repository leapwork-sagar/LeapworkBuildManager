using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public static class UiScale
    {
        public static int Pixels(int value, float scale)
        {
            return (int)Math.Round(value * scale);
        }

        public static void ApplyTree(Control root, float factor)
        {
            var fonts = new Dictionary<Control, Font>();
            Capture(root, fonts);
            root.Scale(new SizeF(factor, factor));
            // Point-size fonts already scale with device DPI. Scale geometry once only.
            foreach (var entry in fonts)
                entry.Key.Font = entry.Value;
        }

        static void Capture(Control root, Dictionary<Control, Font> fonts)
        {
            fonts[root] = root.Font;
            foreach (Control child in root.Controls)
                Capture(child, fonts);
        }
    }
}
