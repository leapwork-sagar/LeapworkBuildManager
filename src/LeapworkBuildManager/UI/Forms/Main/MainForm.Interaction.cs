using System;
using System.IO;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        readonly SoftButton copyFolderPath = new SoftButton { Text = "Copy folder path" };
        string displayedDestination = "";
        string destinationPrefix = "";
        int interactionRevision;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Tab is processed as a dialog key and may never raise KeyDown.
            interactionRevision++;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void ConfigureInteraction()
        {
            KeyPreview = true;
            KeyDown += delegate { interactionRevision++; };
            Deactivate += delegate { interactionRevision++; };
            TrackPointerNavigation(this);
            detailsTip.SetToolTip(advanced, "Check only the build type you select.");
            detailsTip.SetToolTip(reset, "Clear the current build, results and progress. Keep saved history and download folder.");
            detailsTip.SetToolTip(copy, "Copy the complete download address.");
            copyFolderPath.Click += delegate
            {
                if (String.IsNullOrEmpty(displayedDestination)) return;
                try { Clipboard.SetText(displayedDestination); }
                catch (System.Runtime.InteropServices.ExternalException) { status.Text = "Could not copy the folder path. Please try again."; }
            };
            destinationLabel.SizeChanged += delegate { RenderDestination(); };
        }

        void TrackPointerNavigation(Control parent)
        {
            parent.MouseDown += delegate { interactionRevision++; };
            foreach (Control child in parent.Controls) TrackPointerNavigation(child);
        }

        void FocusAfterOperation(Control target, int revision)
        {
            // A completion must not steal focus after navigation or an application switch.
            if (!IsDisposed && FocusPolicy.CanAdvance(ActiveForm == this, WindowState == FormWindowState.Minimized,
                closing, revision, interactionRevision) && target.CanFocus)
                target.Focus();
        }

        void SetDestination(string file, bool completed)
        {
            displayedDestination = String.IsNullOrEmpty(file) ? "" : Path.GetDirectoryName(file);
            destinationPrefix = completed ? "Saved to: " : "Saving to: ";
            detailsTip.SetToolTip(destinationLabel, displayedDestination);
            destinationLabel.AccessibleDescription = displayedDestination;
            copyFolderPath.Visible = !String.IsNullOrEmpty(displayedDestination);
            RenderDestination();
        }

        void RenderDestination()
        {
            destinationLabel.Text = String.IsNullOrEmpty(displayedDestination) ? "" : destinationPrefix +
                DestinationText.Fit(displayedDestination, destinationLabel.Font,
                    Math.Max(0, destinationLabel.Width - TextRenderer.MeasureText(destinationPrefix, destinationLabel.Font).Width));
        }

        void UpdateActionTooltips()
        {
            detailsTip.SetToolTip(download, appState == ApplicationState.Completed ? "Download another copy of this build." : "");
            detailsTip.SetToolTip(folder, String.IsNullOrEmpty(completedPath) ? "" : Path.GetDirectoryName(completedPath));
            detailsTip.SetToolTip(viewLink, linkExpanded ? "Hide the download address." : "Show the complete download address.");
            var entry = recent.SelectedItem as RecentBuild;
            detailsTip.SetToolTip(recent, entry == null ? "" : HistoryFormatter.Format(entry));
        }
    }
}
