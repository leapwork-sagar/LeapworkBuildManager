using System;
using System.Threading;

namespace LeapworkBuildManager
{
    // Owned by the UI thread. Workers receive only a captured token.
    public sealed class OperationCoordinator : IDisposable
    {
        public CancellationTokenSource Current { get; private set; }

        bool disposed;
        public CancellationTokenSource Begin()
        {
            if (disposed)
                throw new ObjectDisposedException("OperationCoordinator");
            if (Current != null)
                throw new InvalidOperationException("An operation is already running.");
            return Current = new CancellationTokenSource();
        }

        public bool Accepts(CancellationTokenSource source)
        {
            return !disposed && source != null && ReferenceEquals(Current, source) && !source.IsCancellationRequested;
        }

        public void Cancel()
        {
            if (Current != null)
                Current.Cancel();
        }

        public void Complete(CancellationTokenSource source)
        {
            if (!ReferenceEquals(Current, source))
                return;
            Current = null;
            source.Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Cancel();
            if (Current != null)
            {
                Current.Dispose();
                Current = null;
            }
        }
    }
}
