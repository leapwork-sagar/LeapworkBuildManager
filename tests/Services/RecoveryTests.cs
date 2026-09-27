using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using LeapworkBuildManager;

static class RecoveryTests
{
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    sealed class Incoming : Stream
    {
        readonly int delay; int reads;
        public Incoming(int wait) { delay = wait; }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken token)
        {
            reads++;
            if (reads > 2) return 0;
            if (reads == 2) await Task.Delay(delay, token);
            buffer[offset] = 7; return 1;
        }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return 2; } }
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] b, int o, int c) { throw new NotSupportedException(); }
        public override long Seek(long o, SeekOrigin s) { throw new NotSupportedException(); }
        public override void SetLength(long l) { throw new NotSupportedException(); }
        public override void Write(byte[] b, int o, int c) { throw new NotSupportedException(); }
    }
    sealed class Handler : HttpMessageHandler
    {
        readonly int delay;
        public Handler(int wait) { delay = wait; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Incoming(delay)) };
            response.Content.Headers.ContentLength = 2;
            return Task.FromResult(response);
        }
    }
    sealed class Updates : IProgress<DownloadProgressInfo>
    {
        public readonly List<DownloadProgressInfo> Values = new List<DownloadProgressInfo>();
        public void Report(DownloadProgressInfo value) { Values.Add(value); }
    }
    static void Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine(count + " recovery checks passed."); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    }
    static async Task Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "Recovery-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "installer.msi"); File.WriteAllText(file, "old");
            Check(await SavedInstallerService.InspectAsync(file, CancellationToken.None) == SavedInstallerState.Available, "existing installer is available");
            Check(await SavedInstallerService.InspectAsync(file + ".missing", CancellationToken.None) == SavedInstallerState.Missing, "missing installer distinguished from access failure");
            Check(SavedInstallerService.Inspect(file, p => { throw new UnauthorizedAccessException(); }) == SavedInstallerState.Inaccessible, "permission failure is inaccessible");
            Check(SavedInstallerService.Inspect(file, p => { throw new DirectoryNotFoundException(); }) == SavedInstallerState.Inaccessible, "unavailable drive is not reported as deleted");
            Check(SavedInstallerService.Inspect(file, p => { throw new IOException(); }) == SavedInstallerState.Inaccessible, "IO failure permits retry");
            Check(await SavedInstallerService.InspectAsync(dir, CancellationToken.None) == SavedInstallerState.Missing, "directory cannot masquerade as installer");
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                try { await SavedInstallerService.InspectAsync(file, stop.Token); throw new Exception("Expected cancellation"); }
                catch (OperationCanceledException) { Check(true, "saved path check respects cancellation"); }
            }
            foreach (int delay in new[] { 0, 90, -1 })
            using (var service = new BuildService(new Handler(delay)))
            {
                service.StallWarningDelay = TimeSpan.FromMilliseconds(30);
                service.InactivityTimeout = TimeSpan.FromMilliseconds(250);
                var updates = new Updates();
                File.WriteAllText(file, "old");
                bool timedOut = false;
                try { await service.DownloadAsync(new Uri("https://" + OperationalSettings.DownloadHost + "/test.msi"), file, updates, CancellationToken.None); }
                catch (OperationCanceledException) { timedOut = true; }
                var warning = updates.Values.Find(x => x.IsStalled);
                Check((warning != null) == (delay != 0), "inactivity warning follows received data at delay " + delay);
                if (warning != null) Check(warning.BytesReceived == 1 && warning.BytesPerSecond == 0 && !warning.TimeRemaining.HasValue, "stall preserves bytes and clears stale estimates");
                if (delay == -1)
                {
                    Check(timedOut && File.ReadAllText(file) == "old", "prolonged stall times out without replacing existing installer");
                    Check(Directory.GetFiles(dir, "*.part").Length == 0, "stall timeout cleans partial file");
                }
                else
                {
                    Check(!timedOut && new FileInfo(file).Length == 2, "transfer completes after incoming data");
                    Check(!updates.Values[updates.Values.Count - 1].IsStalled, "resumed data clears stall state");
                }
            }
            using (var service = new BuildService(new Handler(-1)))
            {
                service.StallWarningDelay = TimeSpan.FromMilliseconds(20);
                service.InactivityTimeout = TimeSpan.FromMilliseconds(200);
                var controller = new BuildController(service);
                controller.Reset();
                await controller.CheckAsync("2026.2.257", BuildKind.Release, CancellationToken.None);
                try { await controller.DownloadAsync(file, new Updates(), CancellationToken.None); }
                catch (OperationCanceledException) { }
                var ui = DownloadUiModel.Create(controller, false, false, true);
                Check(ui.ShowDownload && ui.DownloadText == "Retry download", "inactivity timeout exposes explicit retry action");
            }
            using (var service = new BuildService(new Handler(-1)))
            using (var stop = new CancellationTokenSource())
            {
                service.StallWarningDelay = TimeSpan.FromMilliseconds(20);
                service.InactivityTimeout = TimeSpan.FromSeconds(3);
                stop.CancelAfter(100);
                try { await service.DownloadAsync(new Uri("https://" + OperationalSettings.DownloadHost + "/test.msi"), file, new Updates(), stop.Token); throw new Exception("Expected cancellation"); }
                catch (OperationCanceledException) { Check(stop.IsCancellationRequested, "user cancellation remains available during a stall"); }
                Check(Directory.GetFiles(dir, "*.part").Length == 0, "stalled cancellation removes partial data");
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
