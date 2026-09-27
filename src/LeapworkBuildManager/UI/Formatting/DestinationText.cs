using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    internal static class DestinationText
    {
        public static string Fit(string path, Font font, int width)
        {
            if (String.IsNullOrEmpty(path)) return "";
            if (TextRenderer.MeasureText(path, font).Width <= width) return path;
            string root = Path.GetPathRoot(path);
            string tail = path.Substring(root.Length).TrimEnd('\\', '/');
            while (tail.Length > 0)
            {
                string candidate = root + "…\\" + tail;
                if (TextRenderer.MeasureText(candidate, font).Width <= width) return candidate;
                int separator = tail.IndexOfAny(new[] { '\\', '/' });
                tail = separator >= 0 ? tail.Substring(separator + 1) : tail.Substring(1);
            }
            return "…";
        }
    }
}
