namespace LeapworkBuildManager
{
    internal static class FocusPolicy
    {
        public static bool CanAdvance(bool active, bool minimized, bool closing, int startedRevision, int currentRevision)
        {
            return active && !minimized && !closing && startedRevision == currentRevision;
        }
    }
}
