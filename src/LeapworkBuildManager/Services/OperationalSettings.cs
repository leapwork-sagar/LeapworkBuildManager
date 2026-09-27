using System;

namespace LeapworkBuildManager
{
    public static class OperationalSettings
    {
        public static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan DownloadStallWarning = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan DownloadIdleTimeout = TimeSpan.FromSeconds(60);
        public const string DownloadHost = "sawindowreleasedata.blob.core.windows.net";
        public const int MaximumRedirects = 5;
        public const int LogQueueCapacity = 256;
        public const long LogFileBytes = 1048576;
        public const int LogBackups = 3;
        public const int LogRecordCharacters = 16384;
        public const int LogShutdownMilliseconds = 2000;
        public const int SettingsDebounceMilliseconds = 400;
        public const int AnimationFrameMilliseconds = 15;
        public const int AnimationDurationMilliseconds = 180;
        public const long CancelConfirmationBytes = 10L * 1024 * 1024;
        public const double CancelConfirmationSeconds = 10;
        public const int ProgressMilestonePercent = 10;
        public const int SearchConcurrency = 3;
        public const int DownloadBufferBytes = 81920;
        public const int ProgressIntervalMilliseconds = 150;
        public const int DiagnosticCapacity = 30;
        public const long DiskSpaceReserveBytes = 16L * 1024 * 1024;
        public const double SpeedSmoothingSeconds = 3.0;
    }
}
