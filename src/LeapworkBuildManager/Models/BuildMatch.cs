using System;

namespace LeapworkBuildManager
{
    public sealed class BuildMatch
    {
        public string BuildNumber;
        public long? SizeBytes;
        public AvailabilityStatus Status;
        public BuildKind BuildType;
        public string Kind
        {
            get
            {
                return BuildKinds.Token(BuildType);
            }

            set
            {
                BuildType = BuildKinds.Parse(value);
            }
        }

        public bool Modern, Available, Missing;
        public string Detail;
        public Uri Url;
        public string DisplayKind
        {
            get
            {
                return BuildKinds.Display(BuildType);
            }
        }

        public override string ToString()
        {
            return DisplayKind + "  ·  Available";
        }
    }
}
