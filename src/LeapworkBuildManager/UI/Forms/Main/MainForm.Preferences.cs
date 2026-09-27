using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        async Task LoadPreferencesAsync()
        {
            await Task.Yield();
            var loaded = await Task.Run(() => PreferenceStore.Load(preferencesPath));
            if (IsDisposed || Disposing)
                return;
            preferences = loaded;
            settingsWriter = new SettingsWriter(preferencesPath, loaded);
            restoring = true;
            build.Text = preferences.Build;
            foreach (BuildKind kind in Enum.GetValues(typeof(BuildKind)))
                if (preferences.Type == BuildKinds.Display(kind) || preferences.Type == BuildKinds.Token(kind))
                    SelectedBuildType = BuildKinds.Display(kind);
            restoring = false;
            preferencesLoaded = true;
            RefreshHistory();
            Changed();
            if (!String.IsNullOrEmpty(loaded.LoadNotice))
            {
                RecordDiagnostic(loaded.LoadNotice, "Warning", "Settings", null);
                status.Text = loaded.LoadNotice;
            }

            CenterStartup();
            if (Visible)
                ActiveControl = build;
        }

        void RefreshHistory()
        {
            restoring = true;
            recent.Items.Clear();
            recent.Items.Add(preferences.History.Count == 0 ? "No recent builds yet" : "Select a recent build…");
            foreach (var entry in preferences.History)
                recent.Items.Add(entry);
            recent.SelectedIndex = 0;
            restoring = false;
        }

        void RememberBuild(string path)
        {
            preferences.Remember(new RecentBuild { Build = build.Text.Trim(), Type = SelectedBuildType, Runtime = inferredRuntime, DownloadPath = path });
            RefreshHistory();
        }

        void QueuePreferences()
        {
            if (!preferencesLoaded || settingsWriter == null)
                return;
            preferences.Build = build.Text;
            preferences.Type = SelectedBuildType;
            preferences.Runtime = inferredRuntime;
            if (settingsWriter.Request(preferences))
            {
                settingsSaveTimer.Stop();
                settingsSaveTimer.Start();
            }
        }

        void SavePreferences()
        {
            QueuePreferences();
        }

        async Task<bool> TryFlushPreferencesAsync()
        {
            settingsSaveTimer.Stop();
            if (settingsWriter == null)
                return true;
            try
            {
                await settingsWriter.FlushAsync();
                return true;
            }
            catch (Exception error)
            {
                if (!IsDisposed)
                {
                    RecordDiagnostic("Settings save failed", "Error", "Settings", error);
                    status.Text = "Settings could not be saved. Changes will be retried on exit.";
                }

                return false;
            }
        }

        void ConfigureSettingsSaves()
        {
            settingsSaveTimer.Tick += async delegate
            {
                await TryFlushPreferencesAsync();
            };
            FormClosing += async delegate (object sender, FormClosingEventArgs e)
            {
                if (e.Cancel || allowSettingsClose || !preferencesLoaded)
                    return;
                e.Cancel = true;
                if (settingsClosePending)
                    return;
                settingsClosePending = true;
                QueuePreferences();
                settingsSaveTimer.Stop();
                Enabled = false;
                try
                {
                    await settingsWriter.FlushAsync();
                    allowSettingsClose = true;
                }
                catch (Exception error)
                {
                    Enabled = true;
                    RecordDiagnostic("Settings save failed on exit", "Error", "Settings", error);
                    allowSettingsClose = MessageBox.Show(this, "Your latest settings could not be saved.\n" + error.Message + "\n\nClose without saving them?", "Settings could not be saved", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                }

                settingsClosePending = false;
                if (allowSettingsClose && !IsDisposed)
                {
                    RecordDiagnostic("Application closing");
                    // Bound shutdown: queued logs can be lost if the writer cannot finish.
                    await Task.WhenAny(persistentLog.CompleteAsync(), Task.Delay(OperationalSettings.LogShutdownMilliseconds));
                    if (!IsDisposed)
                        BeginInvoke(new Action(Close));
                }
                else
                    Enabled = true;
            };
        }
    }
}
