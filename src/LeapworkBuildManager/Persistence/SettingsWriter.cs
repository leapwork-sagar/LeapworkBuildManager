using System;
using System.Threading;
using System.Threading.Tasks;

namespace LeapworkBuildManager
{
    public sealed class SettingsWriter
    {
        readonly object sync = new object ();
        readonly SemaphoreSlim serial = new SemaphoreSlim(1);
        readonly Func<Preferences, long> commit;
        readonly bool readOnly;
        Preferences saved, desired;
        long revision;
        bool force;
        public SettingsWriter(string path, Preferences initial) : this(initial, value => PreferenceStore.Commit(path, new SettingsSnapshot(value)))
        {
        }

        public SettingsWriter(Preferences initial, Func<Preferences, long> commit)
        {
            this.commit = commit;
            readOnly = initial.IsReadOnly;
            saved = PreferencesContent.Copy(initial);
            desired = PreferencesContent.Copy(initial);
            revision = initial.Revision;
            force = initial.NeedsSave || !String.IsNullOrEmpty(initial.LoadNotice);
        }

        public bool IsDirty
        {
            get
            {
                lock (sync)
                    return !readOnly && !desired.IsReadOnly && (force || !PreferencesContent.Equal(saved, desired));
            }
        }

        public bool Request(Preferences value)
        {
            lock (sync)
            {
                if (readOnly || value.IsReadOnly)
                    return false;
                if (!PreferencesContent.Equal(desired, value))
                    desired = PreferencesContent.Copy(value);
                return force || !PreferencesContent.Equal(saved, desired);
            }
        }

        public async Task FlushAsync()
        {
            await serial.WaitAsync().ConfigureAwait(false);
            try
            {
                while (true)
                {
                    Preferences snapshot;
                    lock (sync)
                    {
                        if (readOnly || desired.IsReadOnly || (!force && PreferencesContent.Equal(saved, desired)))
                            return;
                        // Later UI edits must not change the background commit.
                        snapshot = PreferencesContent.Copy(desired);
                        snapshot.Revision = revision;
                    }

                    long written = await Task.Run(() => commit(snapshot)).ConfigureAwait(false);
                    lock (sync)
                    {
                        revision = written;
                        saved = snapshot;
                        force = false;
                    }
                }
            }
            finally
            {
                serial.Release();
            }
        }
    }
}
