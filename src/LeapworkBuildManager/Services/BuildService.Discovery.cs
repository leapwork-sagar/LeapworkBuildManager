using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LeapworkBuildManager
{
    public sealed partial class BuildService
    {
        public async Task<BuildMatch[]> FindAsync(string build, IProgress<SearchProgressInfo> progress, CancellationToken token)
        {
            bool modern = IsModern(build);
            var kinds = BuildKinds.Candidates(modern);
            int completed = 0;
            using (var limit = new SemaphoreSlim(OperationalSettings.SearchConcurrency))
            {
                var tasks = new List<Task<BuildMatch>>();
                foreach (BuildKind kind in kinds)
                {
                    BuildKind selected = kind;
                    tasks.Add(FindOneAsync(build, selected, modern, limit, token, result =>
                    {
                        int count = Interlocked.Increment(ref completed);
                        if (progress != null)
                            progress.Report(new SearchProgressInfo(count, kinds.Length, result));
                    }));
                }

                return await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        async Task<BuildMatch> FindOneAsync(string build, BuildKind kind, bool modern, SemaphoreSlim limit, CancellationToken token, Action<BuildMatch> finished)
        {
            await limit.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var result = new BuildMatch
                {
                    BuildNumber = build,
                    BuildType = kind,
                    Modern = modern,
                    Url = BuildUrl(build, kind, modern)
                };
                var availability = await CheckAsync(result.Url, token).ConfigureAwait(false);
                result.Status = availability.Status;
                result.Available = availability.IsAvailable;
                result.Missing = availability.Status == AvailabilityStatus.NotFound;
                result.SizeBytes = availability.SizeBytes;
                result.Detail = availability.Detail;
                finished(result);
                return result;
            }
            finally
            {
                limit.Release();
            }
        }

        public async Task<AvailabilityResult> CheckAsync(Uri url, CancellationToken token)
        {
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(OperationalSettings.AvailabilityTimeout);
                    using (var head = new HttpRequestMessage(HttpMethod.Head, url))
                    {
                        var response = await client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                        bool ranged = false;
                        if (response.StatusCode == HttpStatusCode.MethodNotAllowed || response.StatusCode == HttpStatusCode.NotImplemented)
                        {
                            response.Dispose();
                            using (var get = new HttpRequestMessage(HttpMethod.Get, url))
                            {
                                get.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                                response = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                                ranged = response.StatusCode == HttpStatusCode.PartialContent;
                            }
                        }

                        using (response)
                        {
                            token.ThrowIfCancellationRequested();
                            Trace("Availability HTTP " + (int)response.StatusCode + " " + url, null, url);
                            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone)
                                return new AvailabilityResult(AvailabilityStatus.NotFound, null, "Build not found.");
                            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
                                return new AvailabilityResult(AvailabilityStatus.AccessDenied, null, "Access denied. Availability could not be confirmed.");
                            if (!response.IsSuccessStatusCode)
                                return new AvailabilityResult(AvailabilityStatus.ServerError, null, "Server returned HTTP " + (int)response.StatusCode + ".");
                            long? size = null;
                            if (response.Content != null)
                            {
                                // A ranged response's Content-Length is the fragment size, not installer size.
                                var headers = response.Content.Headers;
                                size = ranged ? (headers.ContentRange == null ? null : headers.ContentRange.Length) : headers.ContentLength;
                            }

                            return new AvailabilityResult(AvailabilityStatus.Available, size, "Build available for download.");
                        }
                    }
                }
            }
            catch (OperationCanceledException error)
            {
                Trace(token.IsCancellationRequested ? "Availability cancelled" : "Availability timeout", error, url);
                token.ThrowIfCancellationRequested();
                return new AvailabilityResult(AvailabilityStatus.TimedOut, null, "The server timed out. Please retry.");
            }
            catch (RedirectPolicyException error)
            {
                Trace("Address policy blocked request", error, url);
                return new AvailabilityResult(AvailabilityStatus.ServerError, null, error.Message);
            }
            catch (HttpRequestException error)
            {
                Trace("Availability connection failure", error, url);
                return new AvailabilityResult(AvailabilityStatus.ConnectionFailed, null, "Connection failed. Check your connection and retry.");
            }
        }
    }
}
