using System;
using System.IO;
using System.Net.Http;

namespace LeapworkBuildManager
{
    public sealed class InsufficientDiskSpaceException : IOException
    {
        public InsufficientDiskSpaceException(string message) : base(message)
        {
        }
    }

    public sealed class DownloadHttpException : HttpRequestException
    {
        public int StatusCode { get; private set; }

        public DownloadHttpException(int status) : base("HTTP " + status)
        {
            StatusCode = status;
        }
    }

    public sealed class FailureInfo
    {
        public string Category { get; private set; }
        public string Message { get; private set; }

        public static FailureInfo From(Exception e)
        {
            string category = "Unexpected", message = "The operation could not finish. Retry, or copy diagnostics from About / Help.";
            var http = e as DownloadHttpException;
            if (e is RedirectPolicyException)
            {
                category = "AddressPolicy";
                message = "The server returned a blocked download address. Contact support.";
            }
            else if (e is InsufficientDiskSpaceException || e is IOException && ((e.HResult & 65535) == 112 || (e.HResult & 65535) == 39))
            {
                category = "DiskSpace";
                message = "There is not enough free space. Free up space or choose another drive.";
            }
            else if (http != null && (http.StatusCode == 401 || http.StatusCode == 403))
            {
                category = "AccessDenied";
                message = "The server denied access to this build. Check availability again or contact support.";
            }
            else if (http != null && (http.StatusCode == 404 || http.StatusCode == 410))
            {
                category = "NotFound";
                message = "This download is no longer available. Search for the build again.";
            }
            else if (http != null)
            {
                category = "Server";
                message = "The server could not complete the download. Try again shortly.";
            }
            else if (e is UnauthorizedAccessException)
            {
                category = "Permissions";
                message = "Cannot write to this location. Choose another folder.";
            }
            else if (e is OperationCanceledException)
            {
                category = "Timeout";
                message = "The operation timed out. Check your connection and retry.";
            }
            else if (e is HttpRequestException)
            {
                category = "Connection";
                message = "Cannot reach the download server. Check your connection and retry.";
            }
            else if (e is IOException)
            {
                category = "FileSystem";
                message = "The file operation could not finish. Check free space and folder access, then retry.";
            }

            return new FailureInfo
            {
                Category = category,
                Message = message
            };
        }
    }

    public enum CancellationReason
    {
        CancelButton,
        Escape,
        WindowClosing,
        SystemSleep
    }

    public static class CancellationPolicy
    {
        public static bool NeedsConfirmation(bool downloading, long bytes, double seconds, CancellationReason reason)
        {
            return downloading && reason != CancellationReason.SystemSleep && (reason == CancellationReason.WindowClosing || bytes >= OperationalSettings.CancelConfirmationBytes || seconds >= OperationalSettings.CancelConfirmationSeconds);
        }
    }
}
