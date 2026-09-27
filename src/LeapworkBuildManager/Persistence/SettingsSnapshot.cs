using System.IO;

namespace LeapworkBuildManager
{
    public sealed class SettingsSnapshot
    {
        internal Preferences Value { get; private set; }

        // Caller supplies an independent copy; this constructor retains that instance.
        internal SettingsSnapshot(Preferences value)
        {
            Value = value;
        }
    }

    public sealed class SettingsConflictException : IOException
    {
        public SettingsConflictException() : base("Settings changed in another operation or app instance. Reopen the app before saving to avoid overwriting newer changes.")
        {
        }
    }
}
