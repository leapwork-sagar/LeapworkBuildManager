using System;
using System.IO;

namespace LeapworkBuildManager
{
    public sealed class DownloadResult
    {
        public string Destination { get; private set; }
        public long BytesWritten { get; private set; }
        public TimeSpan Elapsed { get; private set; }

        public DownloadResult(string destination, long bytesWritten, TimeSpan elapsed)
        {
            Destination = Path.GetFullPath(destination);
            BytesWritten = Math.Max(0, bytesWritten);
            Elapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }

        public string Summary
        {
            get
            {
                long seconds = Math.Max(1, (long)Math.Ceiling(Elapsed.TotalSeconds));
                string duration = seconds >= 3600 ? String.Format("{0}h {1}m {2}s", seconds / 3600, seconds / 60 % 60, seconds % 60) : seconds >= 60 ? String.Format("{0}m {1}s", seconds / 60, seconds % 60) : seconds + "s";
                return String.Format("Downloaded {0:F1} MB in {1}.", BytesWritten / 1048576.0, duration);
            }
        }
    }
}
