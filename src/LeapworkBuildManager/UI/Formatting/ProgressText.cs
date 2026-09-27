using System;

namespace LeapworkBuildManager
{
    public static class ProgressText
    {
        public static string Remaining(DownloadProgressInfo progress)
        {
            if (!progress.TotalBytes.HasValue)
                return "Time remaining unavailable (unknown file size)";
            if (!progress.TimeRemaining.HasValue)
                return "Estimating time remaining…";
            var remaining = TimeSpan.FromSeconds(Math.Max(1, Math.Ceiling(progress.TimeRemaining.Value.TotalSeconds)));
            if (remaining.TotalHours >= 1)
                return String.Format("About {0}h {1}m remaining", (int)remaining.TotalHours, remaining.Minutes);
            if (remaining.TotalMinutes >= 1)
                return String.Format("About {0}m {1}s remaining", (int)remaining.TotalMinutes, remaining.Seconds);
            return String.Format("About {0}s remaining", remaining.Seconds);
        }
    }
}
