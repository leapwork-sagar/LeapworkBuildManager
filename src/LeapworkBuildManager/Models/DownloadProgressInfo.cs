using System;

namespace LeapworkBuildManager
{
    public sealed class DownloadProgressInfo
    {
        public bool IsStalled { get; private set; }
        public long BytesReceived { get; private set; }
        public long? TotalBytes { get; private set; }
        public double BytesPerSecond { get; private set; }
        public TimeSpan? TimeRemaining { get; private set; }

        public double? Percentage
        {
            get
            {
                return TotalBytes.HasValue && TotalBytes.Value > 0 ? (double? )Math.Min(100, BytesReceived * 100.0 / TotalBytes.Value) : null;
            }
        }

        public DownloadProgressInfo(long received, long? total, double rate, double elapsedSeconds, bool stalled = false)
        {
            IsStalled = stalled;
            if (stalled) rate = 0;
            BytesReceived = Math.Max(0, received);
            TotalBytes = total.HasValue && total.Value >= 0 ? total : null;
            BytesPerSecond = Double.IsNaN(rate) || Double.IsInfinity(rate) ? 0 : Math.Max(0, rate);
            if (TotalBytes.HasValue && BytesPerSecond > 0 && elapsedSeconds >= 2)
            {
                double seconds = Math.Max(0, TotalBytes.Value - BytesReceived) / BytesPerSecond;
                if (seconds < TimeSpan.MaxValue.TotalSeconds)
                    TimeRemaining = TimeSpan.FromSeconds(seconds);
            }
        }
    }
}
