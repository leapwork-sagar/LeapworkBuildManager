using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace LeapworkBuildManager
{
    public enum HistoryOutcome
    {
        Searched,
        Downloaded
    }

    public class RecentBuild
    {
        public string Build { get; set; }
        public string Type { get; set; }
        public int Runtime { get; set; }
        public string DownloadPath { get; set; }
        public DateTime? LastSearchedUtc { get; set; }
        public DateTime? DownloadedUtc { get; set; }
        public HistoryOutcome Outcome { get; set; }
    }

    public class Preferences
    {
        public const int CurrentVersion = 2;
        public int SchemaVersion { get; set; }
        public long Revision { get; set; }
        public string Build { get; set; }
        public string Type { get; set; }
        public int Runtime { get; set; }
        public string DownloadFolder { get; set; }
        public List<RecentBuild> History { get; set; }

        [XmlIgnore]
        public bool IsReadOnly { get; set; }

        [XmlIgnore]
        public string LoadNotice { get; set; }

        [XmlIgnore]
        public bool NeedsSave { get; set; }

        public Preferences()
        {
            Build = "";
            Type = "Experimental";
            History = new List<RecentBuild>();
        }

        public void Remember(RecentBuild entry)
        {
            var existing = History.Find(x => x.Build == entry.Build && x.Type == entry.Type && x.Runtime == entry.Runtime);
            entry.LastSearchedUtc = DateTime.UtcNow;
            if (!String.IsNullOrEmpty(entry.DownloadPath))
            {
                entry.Outcome = HistoryOutcome.Downloaded;
                entry.DownloadedUtc = DateTime.UtcNow;
            }
            else if (existing != null)
            {
                entry.DownloadPath = existing.DownloadPath;
                entry.DownloadedUtc = existing.DownloadedUtc;
                entry.Outcome = existing.Outcome;
            }

            History.RemoveAll(x => x.Build == entry.Build && x.Type == entry.Type && x.Runtime == entry.Runtime);
            History.Insert(0, entry);
            if (History.Count > 20)
                History.RemoveRange(20, History.Count - 20);
        }

        public void Migrate()
        {
            if (SchemaVersion > CurrentVersion)
                throw new NotSupportedException("Settings were created by a newer app version.");
            if (History == null)
                History = new List<RecentBuild>();
            History.RemoveAll(x => x == null || String.IsNullOrWhiteSpace(x.Build));
            foreach (var entry in History)
                if (!String.IsNullOrEmpty(entry.DownloadPath))
                    entry.Outcome = HistoryOutcome.Downloaded;
            if (History.Count > 20)
                History.RemoveRange(20, History.Count - 20);
            SchemaVersion = CurrentVersion;
        }
    }

    public static class PreferenceStore
    {
        // Preserve settings/history after the application rename.
        public static string DefaultPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeapworkBuildUrlGenerator", "preferences.xml");
            }
        }

        static Preferences Read(string path)
        {
            using (var input = File.OpenRead(path))
            {
                var value = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(input);
                value.NeedsSave = value.SchemaVersion < Preferences.CurrentVersion;
                value.Migrate();
                return value;
            }
        }

        public static Preferences Load(string path)
        {
            try
            {
                return Read(path);
            }
            catch (NotSupportedException)
            {
                return new Preferences
                {
                    IsReadOnly = true,
                    LoadNotice = "Newer settings detected. This session will not overwrite them."
                };
            }
            catch (Exception error)
            {
                if (!(error is IOException || error is InvalidOperationException || error is UnauthorizedAccessException))
                    throw;
                try
                {
                    var backup = Read(path + ".bak");
                    backup.LoadNotice = "Settings recovered from backup.";
                    return backup;
                }
                catch (Exception backupError)
                {
                    if (!(backupError is NotSupportedException || backupError is IOException || backupError is InvalidOperationException || backupError is UnauthorizedAccessException))
                        throw;
                    return new Preferences
                    {
                        SchemaVersion = Preferences.CurrentVersion,
                        LoadNotice = File.Exists(path) ? "Settings could not be read. Defaults loaded; existing settings preserved." : null
                    };
                }
            }
        }

        public static SettingsSnapshot Capture(Preferences value)
        {
            lock (value)
            {
                // Preserve independent history records without an XML round trip.
                return new SettingsSnapshot(PreferencesContent.Copy(value));
            }
        }

        public static void Save(string path, Preferences value)
        {
            var snapshot = Capture(value);
            long revision = Commit(path, snapshot);
            lock (value)
                value.Revision = revision;
        }

        public static long Commit(string path, SettingsSnapshot snapshot)
        {
            var value = Capture(snapshot.Value).Value;
            string canonical = Path.GetFullPath(path).ToUpperInvariant();
            string identity;
            using (var hash = System.Security.Cryptography.SHA256.Create())
                identity = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical))).Replace("-", "");
            using (var mutex = new System.Threading.Mutex(false, "Local\\LeapworkSettings-" + identity))
            {
                bool acquired = false;
                try
                {
                    try
                    {
                        acquired = mutex.WaitOne(TimeSpan.FromSeconds(10));
                    }
                    catch (System.Threading.AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (!acquired)
                        throw new IOException("Settings are busy. Please retry.");
                    return CommitCore(path, value);
                }
                finally
                {
                    if (acquired)
                        mutex.ReleaseMutex();
                }
            }
        }

        static long CommitCore(string path, Preferences value)
        {
            if (value.IsReadOnly)
                return value.Revision;
            value.Migrate();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            bool replaceBackup = true;
            if (File.Exists(path))
            {
                // Do not silently overwrite an unknown schema or damaged original.
                try
                {
                    var current = Read(path);
                    if (current.Revision != value.Revision)
                        throw new SettingsConflictException();
                }
                catch (InvalidOperationException)
                {
                    File.Copy(path, path + ".preserved-" + Guid.NewGuid().ToString("N"));
                    replaceBackup = false;
                }
            }

            value.Revision++;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = File.Create(temporary))
                {
                    new XmlSerializer(typeof(Preferences)).Serialize(output, value);
                    output.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temporary, path, replaceBackup ? path + ".bak" : null);
                else
                    File.Move(temporary, path);
                return value.Revision;
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
