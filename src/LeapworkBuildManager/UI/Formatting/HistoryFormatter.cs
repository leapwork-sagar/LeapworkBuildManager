namespace LeapworkBuildManager
{
    public static class HistoryFormatter
    {
        public static string Format(RecentBuild entry)
        {
            bool downloaded = entry.Outcome == HistoryOutcome.Downloaded;
            var date = downloaded ? entry.DownloadedUtc : entry.LastSearchedUtc;
            return entry.Build + " — " + entry.Type + " — " + (downloaded ? "Downloaded" : "Searched") + (date.HasValue ? " · " + date.Value.ToLocalTime().ToString("dd MMM HH:mm") : "");
        }
    }
}
