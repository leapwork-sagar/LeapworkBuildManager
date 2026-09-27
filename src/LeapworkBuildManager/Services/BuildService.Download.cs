using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LeapworkBuildManager
{
    public sealed partial class BuildService
    {
        internal TimeSpan StallWarningDelay = OperationalSettings.DownloadStallWarning;
        internal TimeSpan InactivityTimeout = OperationalSettings.DownloadIdleTimeout;

        async Task<T> AwaitIncomingAsync<T>(Task<T> work, Action stalled, CancellationToken token)
        {
            if (work.IsCompleted)
            {
                token.ThrowIfCancellationRequested();
                return await work.ConfigureAwait(false);
            }
            using (var warning = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var delay = Task.Delay(StallWarningDelay, warning.Token);
                if (await Task.WhenAny(work, delay).ConfigureAwait(false) != work && !token.IsCancellationRequested)
                    stalled();
                warning.Cancel();
                try { return await work.ConfigureAwait(false); }
                catch { token.ThrowIfCancellationRequested(); throw; }
            }
        }

        public async Task<DownloadResult> DownloadAsync(Uri url, string destination, IProgress<DownloadProgressInfo> progress, CancellationToken token, bool replaceExisting = true)
        {
            var transferClock = System.Diagnostics.Stopwatch.StartNew();
            string temp = destination + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(InactivityTimeout);
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    using (var response = await AwaitIncomingAsync(client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token),
                        () => { if (progress != null) progress.Report(new DownloadProgressInfo(0, null, 0, 0, true)); }, timeout.Token).ConfigureAwait(false))
                    {
                        Trace("Download HTTP " + (int)response.StatusCode + " " + url, null, url);
                        if (!response.IsSuccessStatusCode)
                            throw new DownloadHttpException((int)response.StatusCode);
                        long? total = response.Content.Headers.ContentLength;
                        await Preparation.EnsureSpaceAsync(destination, total, timeout.Token).ConfigureAwait(false);
                        long done = 0;
                        var updates = System.Diagnostics.Stopwatch.StartNew();
                        var elapsed = System.Diagnostics.Stopwatch.StartNew();
                        var speed = new RecentSpeed();
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (timeout.Token.Register(() => { try { input.Dispose(); } catch { } }))
                        using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, OperationalSettings.DownloadBufferBytes, true))
                        {
                            byte[] buffer = new byte[OperationalSettings.DownloadBufferBytes];
                            while (true)
                            {
                                timeout.CancelAfter(InactivityTimeout);
                                bool wasStalled = false;
                                int count = await AwaitIncomingAsync(input.ReadAsync(buffer, 0, buffer.Length, timeout.Token), () =>
                                {
                                    wasStalled = true;
                                    Trace("Download stalled: waiting for data", null, url);
                                    if (progress != null) progress.Report(new DownloadProgressInfo(done, total, 0, elapsed.Elapsed.TotalSeconds, true));
                                }, timeout.Token).ConfigureAwait(false);
                                if (wasStalled)
                                {
                                    Trace("Download data resumed", null, url);
                                    speed.ResetAt(done, elapsed.Elapsed.TotalSeconds);
                                }
                                if (count == 0)
                                    break;
                                await output.WriteAsync(buffer, 0, count, timeout.Token).ConfigureAwait(false);
                                done += count;
                                if (progress != null && (wasStalled || updates.ElapsedMilliseconds >= OperationalSettings.ProgressIntervalMilliseconds || (total.HasValue && done == total.Value)))
                                {
                                    progress.Report(new DownloadProgressInfo(done, total, speed.Update(done, elapsed.Elapsed.TotalSeconds), elapsed.Elapsed.TotalSeconds));
                                    updates.Restart();
                                }
                            }

                            if (total.HasValue && total.Value != done)
                                throw new IOException("The download was incomplete. Please retry.");
                            await output.FlushAsync(timeout.Token).ConfigureAwait(false);
                        }

                        token.ThrowIfCancellationRequested();
                        // Keep the previous installer until transfer and length validation succeed.
                        if (replaceExisting && File.Exists(destination))
                            File.Replace(temp, destination, null);
                        else
                            File.Move(temp, destination);
                        transferClock.Stop();
                        return new DownloadResult(destination, done, transferClock.Elapsed);
                    }
                }
            }
            finally
            {
                CleanupPartial(temp);
            }
        }

        public void CleanupPartial(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    DeletePartialFile(path);
                    Trace("Partial file removed");
                }
            }
            catch (Exception error)
            {
                Trace("Partial file cleanup failed", error);
                // Cleanup must never replace the transfer's original exception or cancellation.
                try
                {
                    if (CleanupWarning != null)
                        CleanupWarning("Could not remove partial file: " + path + ". " + error.Message);
                }
                catch
                { /* Diagnostic failures must not mask the transfer failure either. */
                }
            }
        }
    }
}
