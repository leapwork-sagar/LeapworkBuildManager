using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace LeapworkBuildManager
{
    public enum ExistingInstallerChoice
    {
        Cancel,
        OpenFolder,
        SaveCopy,
        Replace
    }

    public sealed class DownloadPreparation
    {
        public string Destination { get; private set; }
        public bool Exists { get; private set; }
        public long? AvailableBytes { get; private set; }
        public long? RequiredBytes { get; private set; }

        public bool HasEnoughSpace
        {
            get
            {
                return !RequiredBytes.HasValue || !AvailableBytes.HasValue || AvailableBytes.Value >= RequiredBytes.Value;
            }
        }

        public bool SpaceVerified
        {
            get
            {
                return RequiredBytes.HasValue && AvailableBytes.HasValue;
            }
        }

        public string Message { get; private set; }

        public DownloadPreparation(string path, bool exists, long? available, long? size)
        {
            Destination = path;
            Exists = exists;
            AvailableBytes = available;
            RequiredBytes = size.HasValue && size >= 0 ? (size.Value > long.MaxValue - OperationalSettings.DiskSpaceReserveBytes ? long.MaxValue : size.Value + OperationalSettings.DiskSpaceReserveBytes) : (long? )null;
            Message = !HasEnoughSpace ? String.Format("Not enough free space. Need {0:N1} MB including a safety margin; {1:N1} MB is available. Choose another location or free some space.", RequiredBytes.Value / 1048576.0, available.Value / 1048576.0) : !size.HasValue ? "The installer size is unknown, so available disk space cannot be verified." : !available.HasValue ? "Free space at this location could not be checked." : "Enough free space is available.";
        }
    }

    // No WinForms dependency. File-system probes are injectable for deterministic tests.
    public sealed class DownloadPreparationService
    {
        readonly Func<string, bool> exists;
        readonly Func<string, long?> freeSpace;
        public DownloadPreparationService() : this(File.Exists, ReadFreeSpace)
        {
        }

        public DownloadPreparationService(Func<string, bool> exists, Func<string, long?> freeSpace)
        {
            this.exists = exists;
            this.freeSpace = freeSpace;
        }

        public Task<DownloadPreparation> InspectAsync(string destination, long? size, CancellationToken token)
        {
            return AsyncProbe.Run(() => Inspect(destination, size), token);
        }

        public Task<string> CopyDestinationAsync(string destination, CancellationToken token)
        {
            return AsyncProbe.Run(() => CopyDestination(destination), token);
        }

        public async Task EnsureSpaceAsync(string destination, long? size, CancellationToken token)
        {
            var result = await InspectAsync(destination, size, token).ConfigureAwait(false);
            if (!result.HasEnoughSpace)
                throw new InsufficientDiskSpaceException(result.Message);
        }

        public DownloadPreparation Inspect(string destination, long? size)
        {
            string path = Path.GetFullPath(destination);
            return new DownloadPreparation(path, exists(path), freeSpace(Path.GetDirectoryName(path)), size);
        }

        public string CopyDestination(string destination)
        {
            string directory = Path.GetDirectoryName(destination), name = Path.GetFileNameWithoutExtension(destination), extension = Path.GetExtension(destination);
            for (int suffix = 1; suffix < 10000; suffix++)
            {
                string candidate = Path.Combine(directory, name + " (" + suffix + ")" + extension);
                if (!exists(candidate))
                    return candidate;
            }

            throw new IOException("Cannot find an unused copy filename. Choose another location.");
        }

        public void EnsureSpace(string destination, long? size)
        {
            var result = Inspect(destination, size);
            if (!result.HasEnoughSpace)
                throw new InsufficientDiskSpaceException(result.Message);
        }

        static long? ReadFreeSpace(string directory)
        {
            ulong available, total, free;
            if (!GetDiskFreeSpaceEx(directory, out available, out total, out free))
                return null;
            return available > long.MaxValue ? long.MaxValue : (long)available;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetDiskFreeSpaceEx(string directory, out ulong available, out ulong total, out ulong free);
    }
}
