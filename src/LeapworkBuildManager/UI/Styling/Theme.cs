using System.Drawing;

namespace LeapworkBuildManager
{
    internal static class Theme
    {
        static readonly string FontFamilyName = ResolveFontFamily();
        static string ResolveFontFamily()
        {
            using (var font = new Font("Segoe UI Variable", 10, FontStyle.Regular))
                return font.Name == "Segoe UI Variable" ? "Segoe UI Variable" : "Segoe UI";
        }

        internal static Font CreateFont(float size, FontStyle style)
        {
            return new Font(FontFamilyName, size, style);
        }

        internal static readonly Color Background = Color.FromArgb(5, 35, 37);
        internal static readonly Color Text = Color.FromArgb(238, 244, 241);
        internal static readonly Color Subtitle = Color.FromArgb(167, 195, 192);
        internal static readonly Color Surface = Color.FromArgb(15, 48, 51);
        internal static readonly Color Accent = Color.FromArgb(211, 182, 105);
        internal static readonly Color AccentText = Color.FromArgb(13, 40, 39);
        internal static readonly Color NeutralStatus = Color.FromArgb(161, 189, 187);
        internal static readonly Color SecondaryText = Color.FromArgb(170, 200, 196);
        internal static readonly Color Input = Color.FromArgb(22, 58, 61);
        internal static readonly Color Success = Color.FromArgb(111, 221, 156);
        internal static readonly Color Error = Color.FromArgb(255, 160, 160);
        internal static readonly Color Focus = Color.FromArgb(224, 195, 122);
        internal static readonly Color Warning = Color.FromArgb(246, 200, 115);
        internal static readonly Color Unavailable = Color.FromArgb(255, 151, 151);
        internal static readonly Color Caption = Color.FromArgb(179, 204, 200);
        internal static readonly Color Selection = Color.FromArgb(45, 91, 91);
        internal static readonly Color Border = Color.FromArgb(44, 80, 82);
        internal static readonly Color InputBorder = Color.FromArgb(64, 105, 107);
        internal static readonly Color DisabledText = Color.FromArgb(118, 148, 147);
        internal static readonly Color Placeholder = Color.FromArgb(169, 195, 192);
        internal static readonly Color ProgressTrack = Color.FromArgb(41, 77, 79);
        internal static readonly Color Button = Color.FromArgb(32, 70, 73);
        internal static readonly Color DisabledSurface = Color.FromArgb(23, 51, 54);
    }
}
