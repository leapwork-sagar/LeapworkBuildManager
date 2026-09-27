using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        void SetupKeyboard()
        {
            KeyPreview = true;
            KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.F1)
                {
                    ShowHelp();
                    e.Handled = true;
                }

                if (e.KeyCode == Keys.Escape && operation != null)
                {
                    RequestCancellation(CancellationReason.Escape);
                    e.Handled = true;
                }
            };
            SetAccessible(build, "Build number", "Digits only. Dots are inserted automatically.");
            SetAccessible(check, "Find or check available builds", "Checks the entered build number.");
            SetAccessible(matches, "Available downloads", "Use Up and Down to select a build, then Enter to focus Download.");
            SetAccessible(download, "Download selected build", "Available only after verification.");
            SetAccessible(recent, "Recent builds", "Select a build to search again.");
            SetAccessible(url, "Selected download URL", "Read-only link.");
            int tab = 0;
            foreach (Control c in new Control[]
            {
                build,
                check,
                advanced,
                type,
                reset,
                matches,
                download,
                viewLink,
                url,
                copy,
                open,
                cancel,
                folder,
                copyFolderPath,
                recent,
                clearHistory,
                help
            }

            )
            {
                if (c == null)
                    continue;
                c.TabStop = true;
                c.TabIndex = tab++;
                if (c.AccessibleName == null)
                    c.AccessibleName = c.Text.Replace("&", "");
            }

            status.AccessibleRole = AccessibleRole.StatusBar;
            status.TextChanged += delegate
            {
                if (!downloading)
                    Announce(status);
            };
            matches.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && download.Enabled)
                {
                    download.Focus();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
        }

        static void SetAccessible(Control c, string name, string description)
        {
            c.AccessibleName = name;
            c.AccessibleDescription = description;
        }
    }
}
