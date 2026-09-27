using System;
using System.Threading;
using System.Threading.Tasks;

namespace LeapworkBuildManager
{
    public static class AsyncProbe
    {
        // Native filesystem probes may not be interruptible. Cancellation stops waiting;
        // the read-only worker is allowed to finish without touching the UI.
        public static async Task<T> Run<T>(Func<T> probe, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var work = Task.Run(probe, token);
            Observe(work);
            var cancelled = new TaskCompletionSource<bool>();
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(work, cancelled.Task).ConfigureAwait(false) != work)
                {
                    token.ThrowIfCancellationRequested();
                }

                token.ThrowIfCancellationRequested();
                return await work.ConfigureAwait(false);
            }
        }

        // Observe faults from probes that finish after the caller stops waiting.
        static void Observe(Task work)
        {
            work.ContinueWith(failed =>
            {
                var ignored = failed.Exception;
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
