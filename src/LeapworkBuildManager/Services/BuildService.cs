using System;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace LeapworkBuildManager
{
    public sealed partial class BuildService : IDisposable
    {
        readonly HttpClient client;
        public DownloadPreparationService Preparation { get; private set; }
        public Action<string, Exception> Diagnostic { get; set; }
        internal Action<string, Exception, Uri> DiagnosticWithUrl { get; set; }

        void Trace(string message, Exception error = null, Uri address = null)
        {
            try
            {
                if (DiagnosticWithUrl != null)
                    DiagnosticWithUrl(message, error, address);
                if (Diagnostic != null)
                    Diagnostic(message, error);
            }
            catch
            { /* Logging cannot affect network work. */
            }
        }

        public Action<string> CleanupWarning { get; set; }
        public Action<string> DeletePartialFile { get; set; }

        public BuildService(HttpMessageHandler handler = null, DownloadPreparationService preparation = null)
        {
            client = new HttpClient(new RestrictedRedirectHandler(handler ?? new HttpClientHandler { AllowAutoRedirect = false }));
            client.Timeout = Timeout.InfiniteTimeSpan;
            Preparation = preparation ?? new DownloadPreparationService();
            DeletePartialFile = File.Delete;
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
