using System;
using System.Text.RegularExpressions;

namespace LeapworkBuildManager
{
    public sealed partial class BuildService
    {
        public static bool Validate(string input, out string error)
        {
            string value = (input ?? "").Trim();
            error = value.Length == 0 ? "Enter a build number, for example 2025.2.990." : !Regex.IsMatch(value, @"\A[0-9]{4}\.[1-4]\.[0-9]+\z") ? "Use YYYY.Q.BuildNumber, with a four-digit release year and quarter 1–4 (e.g. 2026.2.257)." : "";
            return error.Length == 0;
        }

        public static Uri BuildUrl(string input, string type, bool core)
        {
            return BuildUrl(input, BuildKinds.Parse(type), core);
        }

        public static Uri BuildUrl(string input, BuildKind kind, bool core)
        {
            string type = BuildKinds.Token(kind);
            string error;
            if (!Validate(input, out error))
                throw new ArgumentException(error);
            if (kind == BuildKind.PreRelease && !core)
                throw new ArgumentException("Pre-release is available only for .net8 or higher.");
            return new Uri("https://sawindowreleasedata.blob.core.windows.net/" + (kind == BuildKind.Release ? "leaptest-release-builds/" : "leaptest-unstable-builds/") + "Leapwork_" + (core && kind != BuildKind.Release ? "NETCore_" : "") + type + "_x64_" + input.Trim() + ".msi");
        }

        public static bool IsModern(string build)
        {
            string error;
            if (!Validate(build, out error))
                throw new ArgumentException(error);
            var parts = build.Trim().Split('.');
            int year = Int32.Parse(parts[0]), quarter = Int32.Parse(parts[1]);
            // Builds from 2025.3.0 use the modern runtime naming convention.
            return year > 2025 || (year == 2025 && quarter >= 3);
        }
    }
}
