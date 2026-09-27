using System;
using System.IO;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;

namespace LeapworkBuildManager
{
    // Bounded queue; background file-write failures do not fail application operations.
    public sealed class DiagnosticLog : IDisposable
    {
        readonly BlockingCollection<string> queue = new BlockingCollection<string>(OperationalSettings.LogQueueCapacity);
        readonly Task worker;
        readonly string directory;
        readonly long limit;
        readonly Action<string> sink;
        public int Dropped
        {
            get
            {
                return dropped;
            }
        }

        int dropped;
        public string LastError { get; private set; }

        public DiagnosticLog(string directory, long limit = OperationalSettings.LogFileBytes, Action<string> sink = null)
        {
            this.directory = directory;
            this.limit = limit;
            this.sink = sink;
            worker = Task.Factory.StartNew(WriteLoop, System.Threading.CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Write(string text)
        {
            try
            {
                string safe = DiagnosticPrivacy.Redact((text ?? "").Length > OperationalSettings.LogRecordCharacters ? text.Substring(0, OperationalSettings.LogRecordCharacters) + " [truncated]" : text, true);
                if (safe.Length > OperationalSettings.LogRecordCharacters)
                    safe = safe.Substring(0, OperationalSettings.LogRecordCharacters) + " [truncated]";
                if (!queue.TryAdd(safe))
                    System.Threading.Interlocked.Increment(ref dropped);
            }
            catch (InvalidOperationException)
            {
            }
        }

        void WriteLoop()
        {
            try
            {
                foreach (string line in queue.GetConsumingEnumerable())
                {
                    try
                    {
                        if (sink != null)
                        {
                            sink(line);
                            continue;
                        }

                        Directory.CreateDirectory(directory);
                        string path = Path.Combine(directory, "session.log");
                        if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(line) > limit)
                        {
                            for (int i = OperationalSettings.LogBackups; i >= 1; i--)
                            {
                                string target = path + "." + i;
                                string source = i == 1 ? path : path + "." + (i - 1);
                                if (File.Exists(target))
                                    File.Delete(target);
                                if (File.Exists(source))
                                    File.Move(source, target);
                            }
                        }

                        File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                    }
                    catch (Exception e)
                    {
                        LastError = e.GetType().Name;
                    }
                }
            }
            finally
            {
                queue.Dispose();
            }
        }

        public Task CompleteAsync()
        {
            try
            {
                queue.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
            }

            return worker;
        }

        public void Dispose()
        {
            CompleteAsync();
        }
    }
}
