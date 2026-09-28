using System;
using System.Text.RegularExpressions;

namespace LeapworkBuildManager
{
    public static class DiagnosticPrivacy
    {
        public static string Redact(string text, bool hidePaths)
        {
            string value = Regex.Replace(text ?? "", @"https?://[^\s<>""|]+", m =>
            {
                Uri uri;
                if (!Uri.TryCreate(m.Value, UriKind.Absolute, out uri))
                    return "[URL omitted]";
                var clean = new UriBuilder(uri)
                {
                    UserName = "",
                    Password = "",
                    Query = "",
                    Fragment = ""
                };
                return clean.Uri.GetLeftPart(UriPartial.Path) + (uri.Query.Length > 0 ? "?[redacted]" : "");
            }, RegexOptions.IgnoreCase);
            // Cover native/forward-slash Windows paths and file URIs. URL query values
            // are always hidden; path hiding is optional for clipboard exports.
            if (hidePaths)
            {
                value = Regex.Replace(value, @"\bfile:(?://)?[^\r\n|""<>]*", "[local path]", RegexOptions.IgnoreCase);
                value = Regex.Replace(value, @"(?<![A-Za-z0-9])(?:[A-Za-z]:[\\/]|\\\\|(?<![:/])//)[^\r\n|""<>]*", "[local path]", RegexOptions.IgnoreCase);
            }

            return value;
        }
    }
}
