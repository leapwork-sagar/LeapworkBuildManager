using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using LeapworkBuildManager;

static partial class HardeningTests
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    static string ReadOrigin(string path)
    {
        // Framework File.ReadAllText rejects alternate-stream syntax before opening it.
        using (var handle = CreateFile(path + ":Zone.Identifier", 0x80000000, 7, IntPtr.Zero, 3, 0, IntPtr.Zero))
        {
            if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            using (var stream = new FileStream(handle, FileAccess.Read))
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }
    }
    static async Task AttachmentChecks()
    {
        var start = WindowsFileActions.FolderStartInfo(Path.Combine(Path.GetTempPath(), "installer.msi"));
        Assert(start.FileName == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "Explorer uses trusted absolute path");
        Assert(DiagnosticPrivacy.Redact(Host + "/installer.msi", true) == Host + "/installer.msi", "path redaction preserves HTTPS URL");
        string directory = Path.Combine(Path.GetTempPath(), "attachment-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "installer.msi");
        try
        {
            File.WriteAllText(destination, "original");
            var handler = new Handler { Reply = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("replacement") } };
            using (var service = new BuildService(handler))
            {
                service.SaveAttachment = (temporary, final, source) =>
                {
                    Assert(File.ReadAllText(temporary) == "replacement", "attachment check receives completed temporary file");
                    Assert(File.ReadAllText(final) == "original", "old installer survives until attachment approval");
                    throw new IOException("blocked by test policy");
                };
                bool blocked = false;
                try { await service.DownloadAsync(new Uri(Host + "/installer.msi"), destination, null, CancellationToken.None); }
                catch (IOException) { blocked = true; }
                Assert(blocked && File.ReadAllText(destination) == "original", "attachment failure preserves original installer");
                Assert(Directory.GetFiles(directory, "*.part").Length == 0, "blocked attachment partial is cleaned up");
                service.SaveAttachment = WindowsFileActions.SaveAttachment;
                await service.DownloadAsync(new Uri(Host + "/installer.msi"), destination, null, CancellationToken.None);
                Assert(File.ReadAllText(destination) == "replacement", "approved attachment replaces old installer");
                Assert(ReadOrigin(destination).Contains("ZoneId=3"), "internet origin survives replacement on NTFS");
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
