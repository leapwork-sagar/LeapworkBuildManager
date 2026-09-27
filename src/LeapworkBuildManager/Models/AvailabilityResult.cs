using System;

namespace LeapworkBuildManager
{
    public enum AvailabilityStatus
    {
        Available,
        NotFound,
        AccessDenied,
        TimedOut,
        ConnectionFailed,
        ServerError
    }

    public sealed class AvailabilityResult
    {
        public AvailabilityStatus Status { get; private set; }
        public long? SizeBytes { get; private set; }
        public string Detail { get; private set; }

        public bool IsAvailable
        {
            get
            {
                return Status == AvailabilityStatus.Available;
            }
        }

        public AvailabilityResult(AvailabilityStatus status, long? sizeBytes, string detail)
        {
            Status = status;
            SizeBytes = sizeBytes.HasValue && sizeBytes.Value >= 0 ? sizeBytes : null;
            Detail = detail;
        }

        public static string FormatSize(long? bytes)
        {
            if (!bytes.HasValue)
                return "Size unavailable";
            if (bytes.Value < 1048576)
                return String.Format("{0:N0} KB", bytes.Value / 1024.0);
            return String.Format("{0:N1} MB", bytes.Value / 1048576.0);
        }
    }
}
