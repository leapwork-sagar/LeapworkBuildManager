using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LeapworkBuildManager
{
    public static class WindowsFileActions
    {
        public static ProcessStartInfo FolderStartInfo(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || path.IndexOf('"') >= 0)
                throw new ArgumentException("Invalid installer path.", "path");
            return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                "/select,\"" + Path.GetFullPath(path) + "\"") { UseShellExecute = true };
        }

        public static void OpenFolder(string path)
        {
            Process.Start(FolderStartInfo(path));
        }

        public static void SaveAttachment(string localPath, string destination, Uri source)
        {
            Exception failure = null;
            var worker = new Thread(() =>
            {
                try { SaveAttachmentCore(localPath, destination, source); }
                catch (Exception error) { failure = error; }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            worker.Join();
            if (failure != null)
                throw new IOException("Windows could not approve the downloaded attachment. Any existing installer was kept.", failure);
        }

        static void SaveAttachmentCore(string localPath, string destination, Uri source)
        {
            object instance = null;
            try
            {
                instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("4125DD96-E03A-4103-8F70-E0597D803B9C"), true));
                var attachment = (IAttachmentExecute)instance;
                var clientId = new Guid("785332D0-156E-47E7-8299-876EA96F0E08");
                attachment.SetClientGuid(ref clientId);
                attachment.SetLocalPath(Path.GetFullPath(localPath));
                attachment.SetFileName(Path.GetFileName(destination));
                attachment.SetSource(source.AbsoluteUri);
                // Apply Windows attachment policy before replacing an existing installer.
                // Save may scan or delete a blocked file; never publish it on failure.
                attachment.Save();
            }
            catch (COMException error)
            {
                throw new IOException("Windows could not approve the downloaded attachment. The existing installer was kept.", error);
            }
            finally
            {
                if (instance != null && Marshal.IsComObject(instance))
                    Marshal.FinalReleaseComObject(instance);
            }
        }

        // Preserve native vtable order, including methods preceding Save.
        [ComImport, Guid("73DB1241-1E85-4581-8E4F-A81E1D0F8C57"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAttachmentExecute
        {
            void SetClientTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetClientGuid([In] ref Guid client);
            void SetLocalPath([MarshalAs(UnmanagedType.LPWStr)] string path);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void SetSource([MarshalAs(UnmanagedType.LPWStr)] string source);
            void SetReferrer([MarshalAs(UnmanagedType.LPWStr)] string source);
            void CheckPolicy();
            void Prompt(IntPtr parent, int prompt, out int action);
            void Save();
        }
    }
}
