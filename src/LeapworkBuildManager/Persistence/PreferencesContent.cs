namespace LeapworkBuildManager
{
    public static class PreferencesContent
    {
        // Revision is persistence bookkeeping, not a change to user preferences.
        public static bool Equal(Preferences a, Preferences b)
        {
            if (a == null || b == null)
                return a == b;
            if (a.Build != b.Build || a.Type != b.Type || a.Runtime != b.Runtime || a.DownloadFolder != b.DownloadFolder || a.SchemaVersion != b.SchemaVersion || a.History.Count != b.History.Count)
                return false;
            for (int i = 0; i < a.History.Count; i++)
            {
                var x = a.History[i];
                var y = b.History[i];
                if (x.Build != y.Build || x.Type != y.Type || x.Runtime != y.Runtime || x.DownloadPath != y.DownloadPath || x.LastSearchedUtc != y.LastSearchedUtc || x.DownloadedUtc != y.DownloadedUtc || x.Outcome != y.Outcome)
                    return false;
            }

            return true;
        }

        public static Preferences Copy(Preferences value)
        {
            var copy = new Preferences
            {
                Build = value.Build,
                Type = value.Type,
                Runtime = value.Runtime,
                DownloadFolder = value.DownloadFolder,
                Revision = value.Revision,
                SchemaVersion = value.SchemaVersion,
                IsReadOnly = value.IsReadOnly,
                NeedsSave = value.NeedsSave,
                LoadNotice = value.LoadNotice
            };
            foreach (var item in value.History)
                copy.History.Add(new RecentBuild { Build = item.Build, Type = item.Type, Runtime = item.Runtime, DownloadPath = item.DownloadPath, LastSearchedUtc = item.LastSearchedUtc, DownloadedUtc = item.DownloadedUtc, Outcome = item.Outcome });
            return copy;
        }
    }
}
