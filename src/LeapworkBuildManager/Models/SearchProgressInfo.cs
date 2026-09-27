namespace LeapworkBuildManager
{
    public sealed class SearchProgressInfo
    {
        public int Completed { get; private set; }
        public int Total { get; private set; }
        public BuildMatch Result { get; private set; }

        public SearchProgressInfo(int completed, int total, BuildMatch result)
        {
            Completed = completed;
            Total = total;
            Result = result;
        }
    }
}
