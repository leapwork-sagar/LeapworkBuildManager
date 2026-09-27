using System;

namespace LeapworkBuildManager
{
    public enum BuildKind
    {
        Experimental,
        EarlyAccess,
        Custom,
        Release,
        PreRelease
    }

    public static class BuildKinds
    {
        public static string Display(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.EarlyAccess:
                    return "Early Access";
                case BuildKind.PreRelease:
                    return "Pre-release";
                default:
                    return Token(kind);
            }
        }

        public static string Token(BuildKind kind)
        {
            if (!Enum.IsDefined(typeof(BuildKind), kind))
                throw new ArgumentException("Unknown build type.");
            return kind.ToString();
        }

        public static BuildKind Parse(string value)
        {
            foreach (BuildKind kind in Enum.GetValues(typeof(BuildKind)))
                if (value == Token(kind) || value == Display(kind))
                    return kind;
            throw new ArgumentException("Unknown build type.");
        }

        public static BuildKind[] Candidates(bool modern)
        {
            return modern ? new[]
            {
                BuildKind.Release,
                BuildKind.EarlyAccess,
                BuildKind.Experimental,
                BuildKind.Custom,
                BuildKind.PreRelease
            }

            : new[]
            {
                BuildKind.Release,
                BuildKind.EarlyAccess,
                BuildKind.Experimental,
                BuildKind.Custom
            };
        }
    }
}
