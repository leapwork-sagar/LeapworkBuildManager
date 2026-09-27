using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LeapworkBuildManager
{
    public sealed class RedirectPolicyException : HttpRequestException
    {
        public RedirectPolicyException(string message) : base(message)
        {
        }
    }

    public sealed class RestrictedRedirectHandler : DelegatingHandler
    {
        public RestrictedRedirectHandler(HttpMessageHandler inner) : base(inner)
        {
            var transport = inner as HttpClientHandler;
            if (transport != null)
                transport.AllowAutoRedirect = false;
        }

        static void Validate(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !String.Equals(uri.Host, OperationalSettings.DownloadHost, StringComparison.OrdinalIgnoreCase) || uri.Port != 443 || uri.UserInfo.Length != 0)
                throw new RedirectPolicyException("Download address blocked: only the approved HTTPS build host is allowed.");
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage original, CancellationToken token)
        {
            Uri target = original.RequestUri;
            for (int hops = 0;; hops++)
            {
                // Validate before sending each hop; checking the final URL would be too late.
                Validate(target);
                token.ThrowIfCancellationRequested();
                using (var request = new HttpRequestMessage(original.Method, target))
                {
                    foreach (var header in original.Headers)
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    var response = await base.SendAsync(request, token).ConfigureAwait(false);
                    int status = (int)response.StatusCode;
                    if (status != 301 && status != 302 && status != 303 && status != 307 && status != 308)
                        return response;
                    using (response)
                    {
                        if (hops >= OperationalSettings.MaximumRedirects)
                            throw new RedirectPolicyException("The server exceeded the redirect limit.");
                        if (response.Headers.Location == null)
                            throw new RedirectPolicyException("The server returned a redirect without a destination.");
                        target = new Uri(target, response.Headers.Location);
                    }
                }
            }
        }
    }
}
