using System;

namespace LeapworkBuildManager
{
    // A verified selection is a snapshot, never the mutable discovery row.
    public sealed class VerifiedBuild
    {
        public string BuildNumber { get; private set; }
        public BuildKind BuildType { get; private set; }

        public string Kind
        {
            get
            {
                return BuildKinds.Token(BuildType);
            }
        }

        public bool Modern { get; private set; }
        public Uri Url { get; private set; }
        public long? SizeBytes { get; private set; }

        public string DisplayKind
        {
            get
            {
                return BuildKinds.Display(BuildType);
            }
        }

        public VerifiedBuild(BuildMatch match)
        {
            if (match == null || !match.Available || match.Url == null)
                throw new ArgumentException("A verified result is required.");
            BuildNumber = match.BuildNumber;
            BuildType = match.BuildType;
            Modern = match.Modern;
            Url = match.Url;
            SizeBytes = match.SizeBytes;
        }
    }
}
