namespace LeapworkBuildManager
{
    // Derives download-related labels and visibility from controller state.
    public sealed class DownloadUiModel
    {
        public bool Busy, Completed, ShowDownload, ShowLinkToggle, ShowLink, ShowCopy, CanCopy, CanOpenFolder, ShowHistory, ShowResults;
        public string DownloadText, LinkText, CheckText;
        public static DownloadUiModel Create(BuildController controller, bool linkExpanded, bool advanced, bool hasHistory)
        {
            bool downloading = controller.State == ApplicationState.Downloading;
            bool completed = controller.State == ApplicationState.Completed;
            bool hasLink = controller.CurrentUrl != null;
            return new DownloadUiModel
            {
                Busy = controller.IsBusy,
                Completed = completed,
                ShowDownload = controller.IsVerified && !downloading,
                ShowLinkToggle = hasLink && !downloading,
                ShowLink = hasLink && linkExpanded && !downloading,
                ShowCopy = !completed,
                CanCopy = hasLink && !controller.IsBusy && !controller.CopyLocked,
                CanOpenFolder = completed && controller.CompletedPath != null,
                ShowHistory = hasHistory && !downloading,
                ShowResults = !downloading,
                DownloadText = GetDownloadText(controller),
                LinkText = linkExpanded ? "Hide download link" : "View download link",
                CheckText = GetCheckText(controller, advanced)
            };
        }

        static string GetDownloadText(BuildController controller)
        {
            if (controller.InterruptedBySleep || (controller.State == ApplicationState.Failed || controller.State == ApplicationState.Cancelled) && controller.CopyLocked && controller.IsVerified)
                return "Retry download";
            if (controller.State == ApplicationState.Completed)
                return "Download again";
            if (controller.Selected == null)
                return "Download selected build";
            return "Download · " + AvailabilityResult.FormatSize(controller.Selected.SizeBytes);
        }

        static string GetCheckText(BuildController controller, bool advanced)
        {
            if (controller.IsBusy && controller.State != ApplicationState.Downloading && controller.State != ApplicationState.Loading)
            {
                if (controller.State == ApplicationState.Preparing)
                    return "Cancel check";
                return "Cancel search";
            }

            return advanced ? "Check selected type" : "Find available builds";
        }
    }
}
