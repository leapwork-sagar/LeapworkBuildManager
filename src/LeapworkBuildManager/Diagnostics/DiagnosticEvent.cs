using System;
using System.Collections.Generic;
using System.Text;

namespace LeapworkBuildManager
{
    public sealed class DiagnosticEvent
    {
        public string Severity { get; private set; }
        public string Category { get; private set; }
        public string OperationId { get; private set; }
        public string Details { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string Message { get; private set; }
        public string Build { get; private set; }
        public Uri Url { get; private set; }

        public DiagnosticEvent(DateTime timestamp, string message, string build, Uri url)
        {
            Severity = "Information";
            Category = "Application";
            OperationId = "";
            Details = "";
            Timestamp = timestamp;
            Message = message;
            Build = build;
            Url = url;
        }

        public DiagnosticEvent(DateTime timestamp, string message, string build, Uri url, string severity, string category, string operationId, Exception error) : this(timestamp, message, build, url)
        {
            Severity = severity;
            Category = category;
            OperationId = operationId;
            Details = error == null ? "" : error.ToString();
        }

        public static string FormatReport(IEnumerable<DiagnosticEvent> events)
        {
            var text = new StringBuilder();
            foreach (var item in events)
                text.AppendLine(FormatEntry(item));
            return text.ToString();
        }

        static string FormatEntry(DiagnosticEvent item)
        {
            var text = new StringBuilder(item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            text.Append(" | ").Append(item.Severity).Append(" | ").Append(item.Category);
            text.Append(" | Operation: ").Append(item.OperationId);
            text.Append(" | ").Append(item.Message).Append(" | Build: ").Append(item.Build);
            text.Append(" | ").Append(item.Url);
            if (!String.IsNullOrEmpty(item.Details))
                text.AppendLine().Append(item.Details);
            return text.ToString();
        }

        public string DisplayText(string shortUrl)
        {
            var text = new StringBuilder(Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            text.Append(" · ").Append(Severity).Append(" · ").Append(Category);
            if (!String.IsNullOrEmpty(OperationId))
                text.Append(" · ").Append(OperationId.Substring(0, Math.Min(8, OperationId.Length)));
            text.AppendLine().Append(Message);
            if (!String.IsNullOrEmpty(Build))
                text.AppendLine().Append("Build: ").Append(Build);
            if (Url != null)
                text.AppendLine().Append(shortUrl);
            return text.AppendLine().AppendLine().ToString();
        }
    }
}
