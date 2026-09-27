using System;
using System.Threading;
using System.Threading.Tasks;

namespace LeapworkBuildManager
{
    // Owns workflow state and the verified selection; has no dependency on WinForms.
    public sealed class BuildController
    {
        readonly BuildService service;
        bool running;
        public ApplicationState State { get; private set; }
        public VerifiedBuild Selected { get; private set; }
        public Uri PreparedUrl { get; private set; }

        public Uri CurrentUrl
        {
            get
            {
                return Selected == null ? PreparedUrl : Selected.Url;
            }
        }

        public string CompletedPath
        {
            get
            {
                return CompletedDownload == null ? null : CompletedDownload.Destination;
            }
        }

        public DownloadResult CompletedDownload { get; private set; }
        public bool CopyLocked { get; private set; }
        public bool InterruptedBySleep { get; private set; }

        public void MarkSleepInterruption()
        {
            if (State == ApplicationState.Downloading)
                InterruptedBySleep = true;
        }

        public bool IsVerified
        {
            get
            {
                return Selected != null;
            }
        }

        public void Prepare(string build, string kind)
        {
            Prepare(build, BuildKinds.Parse(kind));
        }

        public void Prepare(string build, BuildKind kind)
        {
            Reset();
            PreparedUrl = BuildService.BuildUrl(build, kind, BuildService.IsModern(build));
        }

        public bool IsBusy
        {
            get
            {
                return State == ApplicationState.Preparing || State == ApplicationState.Loading || State == ApplicationState.Searching || State == ApplicationState.Checking || State == ApplicationState.Downloading;
            }
        }

        public BuildController(BuildService service)
        {
            this.service = service;
            State = ApplicationState.Loading;
        }

        public void Transition(ApplicationState next)
        {
            if (next == State)
                return;
            bool valid = CanTransition(State, next, Selected != null);
            if (!valid)
                throw new InvalidOperationException("Invalid transition: " + State + " to " + next);
            State = next;
        }

        public static bool CanTransition(ApplicationState from, ApplicationState to, bool selected)
        {
            if (from == to)
                return true;
            if (to == ApplicationState.Cancelled || to == ApplicationState.Failed)
                return from != ApplicationState.Loading;
            switch (from)
            {
                case ApplicationState.Loading:
                    return to == ApplicationState.Idle;
                case ApplicationState.Preparing:
                    return selected && (to == ApplicationState.Ready || to == ApplicationState.Completed);
                case ApplicationState.Searching:
                    return to == ApplicationState.Results;
                case ApplicationState.Checking:
                    return to == ApplicationState.Ready && selected;
                case ApplicationState.Downloading:
                    return to == ApplicationState.Completed && selected;
                default:
                    return to == ApplicationState.Idle || to == ApplicationState.Searching || to == ApplicationState.Checking || selected && (to == ApplicationState.Preparing || to == ApplicationState.Ready || to == ApplicationState.Downloading);
            }
        }

        public void Reset()
        {
            if (running)
                throw new InvalidOperationException("An operation is still in progress.");
            Transition(ApplicationState.Idle);
            Selected = null;
            PreparedUrl = null;
            CompletedDownload = null;
            CopyLocked = false;
            InterruptedBySleep = false;
        }

        public void Select(BuildMatch match)
        {
            if (running || IsBusy)
                throw new InvalidOperationException("Wait for the current operation.");
            if (match == null || !match.Available || match.Url == null)
                throw new InvalidOperationException("Select a verified build.");
            Selected = new VerifiedBuild(match);
            PreparedUrl = match.Url;
            CompletedDownload = null;
            CopyLocked = false;
            InterruptedBySleep = false;
            Transition(ApplicationState.Ready);
        }

        void Begin(ApplicationState state)
        {
            if (running)
                throw new InvalidOperationException("An operation is already running.");
            Transition(state);
            running = true;
        }

        public async Task<BuildMatch[]> SearchAsync(string build, IProgress<SearchProgressInfo> progress, CancellationToken token)
        {
            Begin(ApplicationState.Searching);
            Selected = null;
            PreparedUrl = null;
            CompletedDownload = null;
            CopyLocked = false;
            InterruptedBySleep = false;
            try
            {
                var results = await service.FindAsync(build, progress, token);
                token.ThrowIfCancellationRequested();
                Transition(ApplicationState.Results);
                return results;
            }
            catch (OperationCanceledException)
            {
                Transition(ApplicationState.Cancelled);
                throw;
            }
            catch
            {
                Transition(ApplicationState.Failed);
                throw;
            }
            finally
            {
                running = false;
            }
        }

        public Task<AvailabilityResult> CheckAsync(string build, string kind, CancellationToken token)
        {
            return CheckAsync(build, BuildKinds.Parse(kind), token);
        }

        public async Task<AvailabilityResult> CheckAsync(string build, BuildKind kind, CancellationToken token)
        {
            Begin(ApplicationState.Checking);
            Selected = null;
            PreparedUrl = null;
            CompletedDownload = null;
            CopyLocked = false;
            InterruptedBySleep = false;
            try
            {
                bool modern = BuildService.IsModern(build);
                var url = BuildService.BuildUrl(build, kind, modern);
                PreparedUrl = url;
                var result = await service.CheckAsync(url, token);
                token.ThrowIfCancellationRequested();
                if (result.IsAvailable)
                    Selected = new VerifiedBuild(new BuildMatch { BuildNumber = build, BuildType = kind, Modern = modern, Url = url, Available = true, Status = result.Status, SizeBytes = result.SizeBytes });
                Transition(result.IsAvailable ? ApplicationState.Ready : ApplicationState.Failed);
                return result;
            }
            catch (OperationCanceledException)
            {
                Transition(ApplicationState.Cancelled);
                throw;
            }
            catch
            {
                Transition(ApplicationState.Failed);
                throw;
            }
            finally
            {
                running = false;
            }
        }

        public async Task<DownloadResult> DownloadAsync(string destination, IProgress<DownloadProgressInfo> progress, CancellationToken token, bool replaceExisting = true, bool spaceChecked = false)
        {
            if (Selected == null)
                throw new InvalidOperationException("Verify a build before downloading.");
            Begin(ApplicationState.Downloading);
            var selection = Selected;
            CompletedDownload = null;
            CopyLocked = true;
            InterruptedBySleep = false;
            try
            {
                if (!spaceChecked)
                    await service.Preparation.EnsureSpaceAsync(destination, selection.SizeBytes, token);
                var result = await service.DownloadAsync(selection.Url, destination, progress, token, replaceExisting);
                CompletedDownload = result;
                Transition(ApplicationState.Completed);
                return result;
            }
            catch (OperationCanceledException)
            {
                Transition(ApplicationState.Cancelled);
                throw;
            }
            catch
            {
                Transition(ApplicationState.Failed);
                throw;
            }
            finally
            {
                running = false;
            }
        }
    }
}
