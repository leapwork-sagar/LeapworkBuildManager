using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LeapworkBuildManager
{
    internal enum SavedInstallerState { Available, Missing, Inaccessible }

    internal static class SavedInstallerService
    {
        internal static SavedInstallerState Inspect(string path, Func<string, FileAttributes> attributes)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path)) return SavedInstallerState.Missing;
                return (attributes(path) & FileAttributes.Directory) == 0 ? SavedInstallerState.Available : SavedInstallerState.Missing;
            }
            catch (FileNotFoundException) { return CheckRoot(path, attributes); }
            catch (DirectoryNotFoundException) { return CheckRoot(path, attributes); }
            catch (IOException) { return SavedInstallerState.Inaccessible; }
            catch (UnauthorizedAccessException) { return SavedInstallerState.Inaccessible; }
            catch (System.Security.SecurityException) { return SavedInstallerState.Inaccessible; }
            catch (ArgumentException) { return SavedInstallerState.Inaccessible; }
            catch (NotSupportedException) { return SavedInstallerState.Inaccessible; }
        }

        static SavedInstallerState CheckRoot(string path, Func<string, FileAttributes> attributes)
        {
            try
            {
                attributes(Path.GetPathRoot(Path.GetFullPath(path)));
                return SavedInstallerState.Missing;
            }
            catch { return SavedInstallerState.Inaccessible; }
        }

        internal static Task<SavedInstallerState> InspectAsync(string path, CancellationToken token)
        {
            return AsyncProbe.Run(() => Inspect(path, File.GetAttributes), token);
        }
    }
}
