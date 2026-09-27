using System;
using System.Collections.Generic;
using System.Text;

namespace LeapworkBuildManager
{
    public static class DiagnosticReport
    {
        public static string Format(string build, string status, Uri url, IEnumerable<DiagnosticEvent> events, DiagnosticLog log)
        {
            var text = new StringBuilder();
            text.AppendLine("Windows: " + Environment.OSVersion + " | CLR: " + Environment.Version + " | Process: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit"));
            text.AppendLine("Logging: " + (log == null ? "Starting" : "dropped=" + log.Dropped + "; last error=" + (log.LastError ?? "none")));
            text.AppendLine("Leapwork Build Manager " + VersionInfo.Display);
            text.AppendLine("Build: " + build);
            text.AppendLine("Status: " + status);
            text.AppendLine("URL: " + url);
            text.Append(DiagnosticEvent.FormatReport(events));
            return text.ToString();
        }
    }
}
