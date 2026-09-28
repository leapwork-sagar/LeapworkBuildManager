using System.Reflection;

[assembly: AssemblyVersion(LeapworkBuildManager.VersionInfo.Number)]
[assembly: AssemblyFileVersion(LeapworkBuildManager.VersionInfo.Number)]
namespace LeapworkBuildManager
{
    public static class VersionInfo
    {
        public const string Number = "1.23.0.0";
        public static readonly string Display = "v" + new System.Version(Number).ToString(2);
    }
}
