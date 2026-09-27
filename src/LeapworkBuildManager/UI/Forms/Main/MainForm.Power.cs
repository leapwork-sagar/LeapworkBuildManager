using System;
using Microsoft.Win32;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(() => HandlePowerChange(e.Mode)));
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void HandlePowerChange(PowerModes mode)
        {
            if (mode == PowerModes.Suspend && downloading && operation != null)
            {
                controller.MarkSleepInterruption();
                RecordDiagnostic("System sleep interrupted download");
                RequestCancellation(CancellationReason.SystemSleep);
                status.Text = "Download interrupted by sleep. Partial file will be removed.";
            }
            else if (mode == PowerModes.Resume && controller.InterruptedBySleep)
            {
                status.Text = "Download interrupted by sleep. Select Retry download to restart.";
                RecordDiagnostic(status.Text);
                UpdateControls();
            }
        }
    }
}
