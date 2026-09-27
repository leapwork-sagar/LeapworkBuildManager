using System;
using System.Threading;

namespace LeapworkBuildManager
{
    public sealed class SingleInstance : IDisposable
    {
        readonly Mutex mutex;
        readonly EventWaitHandle activation;
        RegisteredWaitHandle listener;
        public bool IsPrimary { get; private set; }

        public SingleInstance(string name)
        {
            bool created;
            mutex = new Mutex(true, "Local\\" + name, out created);
            IsPrimary = created;
            activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\" + name + "-Activate");
        }

        public void Listen(Action activate)
        {
            if (!IsPrimary)
                throw new InvalidOperationException();
            listener = ThreadPool.RegisterWaitForSingleObject(activation, delegate
            {
                activate();
            }, null, Timeout.Infinite, false);
        }

        public void ActivateExisting()
        {
            activation.Set();
        }

        public void Dispose()
        {
            if (listener != null)
                listener.Unregister(null);
            activation.Dispose();
            if (IsPrimary)
                mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
