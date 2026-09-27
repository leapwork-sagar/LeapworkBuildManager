namespace LeapworkBuildManager
{
    public sealed partial class MainForm
    {
        public ApplicationState State
        {
            get
            {
                return appState;
            }
        }

        bool IsBusy
        {
            get
            {
                return appState == ApplicationState.Preparing || appState == ApplicationState.Loading || appState == ApplicationState.Searching || appState == ApplicationState.Checking || appState == ApplicationState.Downloading;
            }
        }
    }
}
