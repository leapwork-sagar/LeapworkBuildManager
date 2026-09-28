# Architecture

## Source ownership

`Application` owns workflows/state; `Models` contains build and transfer data.
`Services` handles discovery/downloads; `Persistence` owns transactional settings.
`Diagnostics` owns events, context, report formatting, privacy and log storage.
`UI/Forms/Main` contains the main form partials; `UI/Forms/Dialogs` contains dialogs.
`UI/Formatting` owns user-facing progress and history text. Tests are grouped by
responsibility, with reusable UI pumping under `tests/Support`.

## Layout and lifecycle

WindowLayoutPlan computes work-area-constrained dimensions before MainForm applies
layout in one suspended batch. Canvas width excludes only a required vertical
scrollbar. Narrow input rows stack, eliminating horizontal scrolling. UiScale owns
pixel conversion and one-time control scaling; point-size fonts keep device scaling.
Startup centering happens after preferences load and does not run on later resizing.

HelpDialog uses header/tab/content/footer layout rows. Footer actions never wrap.
Diagnostics controls are lazy and format a view of the original session log; Copy
diagnostics keeps full log data and URLs. Advanced controls are also lazy and bind
to the selected-build-type value without requiring a combo box at startup.

SingleInstance holds a per-user/session mutex and listens for activation events.
The second process grants foreground permission to the matching process, signals
activation and exits. Main restores/activates the primary window on its UI thread.

SystemEvents power notifications dispatch to the UI. Sleep cancels an active download;
the service's existing cancellation path cleans partial files. The controller keeps
an interruption marker for an explicit Retry download action and clears it on retry.
Closing during search cancels and waits for the operation before disposing the form.

The controller, immutable verified selections, structured progress, typed
availability and revision-checked settings transactions remain. Tests exercise
public model/controller behavior plus UI, layout and lifecycle integration.

Update VersionInfo.Number and run release.ps1. The main executable embeds the icon
and a system-DPI-aware, asInvoker manifest. Packaging excludes generated outputs.

## Download preparation and discovery

DownloadPreparationService has injectable existence/free-space probes. It checks
space including the temporary copy and reserve, and finds numbered copy paths.
MainForm.Transfer owns dialogs and separate CheckBuildAsync/DownloadBuildAsync
workflows. User-approved replacement is passed through the controller to the
service; nonreplacement uses File.Move so a late collision cannot overwrite.
Controller checks the verified size; service checks the GET response size before
creating the temporary file. Failed cleanup reports a warning without changing
the primary exception. Disk checks do not reserve disk capacity.

BuildKind is the internal build-type value. BuildKinds owns display/token mapping
and runtime-specific candidates. Legacy string properties are adapters for saved
preferences and compatibility. SearchProgressInfo reports each completed check;
MainForm marshals updates to its UI thread, ignores obsolete callbacks, deduplicates
and sorts available rows, and enables selection only after the search completes.

DiagnosticEvent stores immutable event fields. Help formats those fields directly,
while full reports retain URLs. OperationalSettings owns operational constants.
PreparationTests uses deterministic probes and HTTP responses plus a pumped UI
context to verify safety and incremental results. Existing regression suites stay
part of the release gate. No additional packages or network dependencies are used.


The src/LeapworkBuildManager project is the only working production source.
Tests are outside production code, grouped by Application, Services and UI.
Forms, Controls and Styling have distinct UI directories. ApplicationState is
in Application; the form's public state view is in UI/Forms/Main/MainForm.State.cs.
scripts/build.ps1 writes artifacts/bin; release.ps1 tests then publishes ZIPs
under releases. Source snapshots include src/tests/scripts/docs without generated
outputs. The production project and executable are both LeapworkBuildManager.

DownloadResult is returned only after a successful final file commit and carries
absolute destination, actual bytes and monotonic elapsed time. BuildController
retains that result; CompletedPath is derived, not duplicated. The form renders
completion and destination directly from the result.

OperationCoordinator owns the active CancellationTokenSource. Search, preparation,
availability and download share its Begin/Cancel/Complete lifecycle. Accepts checks
identity before cancellation state so disposed, stale sources are safe to reject.
AsyncProbe moves read-only filesystem checks off the UI and supports cancellation
of waiting even when the native probe cannot stop. Its late failures are observed;
it never mutates UI. Preparation blocks conflicting actions, restores selection on
cancellation and cannot start a transfer after the window begins closing.

The runtime ComboBox and unused availability Label were removed. Saved runtime
integers are legacy serialization values; runtime inference depends on build data.
The historical settings folder and activation key intentionally remain stable.


## Animation and persistence

Panel animation reserves its maximum height once. Frames resize only the progress
card and move history; a final full layout releases the reservation. BufferedPanel
and Card repaint resized areas, including transparent children. Identical animation
targets are ignored, while completion snaps immediately to its final presentation.

PreferencesContent compares persisted user data separately from revision bookkeeping.
SettingsWriter owns serialized commits, immutable snapshots and the latest revision.
A 400 ms UI timer coalesces ordinary edits; completion and normal close flush them.
Failures retain dirty data and closing offers an explicit unsaved-changes choice.
Read-only settings stay protected, and migration/recovery forces one initial save.

Prepared transfers reuse the initial destination check only for the same path.
Changing to a numbered copy triggers another inspection. The download service always
retains its final response-size disk check and atomic non-overwrite commit behavior.


## Cancellation and diagnostics

CancellationPolicy separates confirmation from cancellation mechanics. Cancel/Escape
confirm after 10 MB or 10 seconds; close always confirms downloads, sleep never does.
Closing is marked before signalling cancellation to handle synchronous completion.
RunUiAsync guards async UI entry points, including setup before operation try blocks.
FailureInfo maps technical exceptions to recovery messages; original exceptions are
retained in DiagnosticEvent. Availability tracing preserves swallowed transport errors.

DiagnosticLog uses a bounded 256-record queue and a background worker. It rotates
session.log plus three backups at roughly 1 MB each; records are capped at 16K chars.
Completion drains asynchronously on close with a two-second ceiling. Storage failure
is contained; report diagnostics expose dropped events and the last logger error.
Network tracing captures operation identity before posting updates to the UI.
Persistent logs redact Windows paths and URL secrets. Clipboard exports always
sanitize URLs and offer path hiding. Redaction is best-effort, not a security boundary.

The EXE embeds assets and requires only the installed .NET Framework 4.7.2+ runtime.
App.config contains only a runtime declaration and is not needed for EXE-only use.
smoke-portable.ps1 hosts the production form from an isolated directory containing
only the EXE, with temporary preferences, to check assets, startup and shutdown.
It does not emulate a machine without the prerequisite framework or Windows SmartScreen.
Release packaging produces a standalone EXE, full release ZIP and clean source ZIP.


## Network policy and resource ownership

RestrictedRedirectHandler disables native automatic redirects and validates the
initial URI and each resolved Location before transport. Only HTTPS on port 443
at OperationalSettings.DownloadHost is accepted, without URI credentials. GET/HEAD
and Range headers are preserved through a maximum of five redirects. Mock transports
exercise the same policy; test addresses use the approved hostname without networking.

Form initialization is decomposed into ordered setup methods; substantial UI handlers
live in MainForm.Events. Disposal is idempotent and owns service/timer/event cleanup.
Normal asynchronous shutdown and direct resource disposal remain separate concerns.
DiagnosticContext is captured before network work; both network and UI events use
PublishDiagnostic. Formatting/redaction remain on the caller thread, as before.
Settings snapshots use PreferencesContent.Copy, preserving nested-record isolation
and metadata without changing revision conflicts or atomic file commits.

## Focus, destinations and contextual help

FocusPolicy allows asynchronous focus movement only while the window is active,
not minimized or closing, and no navigation has occurred since the operation began.
Keyboard commands, pointer actions and deactivation invalidate that revision.
Search completion focuses available results; Enter in results focuses Download.
Successful transfers focus Open folder only when the same guard permits it.
Reset and invalid submissions return to the build field; Help restores its opener.

DestinationText abbreviates long paths to the available width. The original folder
is retained for accessibility, the existing hover tooltip and Copy folder path.
Action tooltips use the existing ToolTip instances, with text refreshed for changing
link/download states. Existing status, validation, URL and diagnostic-log tips remain.

## Saved installers and download inactivity

Selecting a downloaded history entry opens SavedInstallerDialog and checks only that
saved path through AsyncProbe. File attributes distinguish missing files from access
or IO failures; a missing root is treated as inaccessible. No startup scan or disk
search is performed. Open folder rechecks before opening Explorer. Download again
returns to online verification; closing or a missing installer never removes history.

Download reads and initial response headers emit a stalled progress record after 30
seconds waiting for incoming data. That record retains byte counts but clears speed
and ETA. New data clears the warning and resets the speed baseline. The existing
60-second inactivity timeout remains, with cancellation disposing the input stream to
interrupt a pending read. Timeout/cancellation cleans the partial file and preserves
an existing installer. Retry restarts rather than resuming. Tests use short injected
intervals and simulated streams; physical network loss is not reproduced end to end.
## Windows download security

`WindowsFileActions` centralizes Explorer launches using the absolute Windows
directory path. Download completion passes the temporary installer and its source
URL to Windows Attachment Services on a dedicated STA thread before publishing
the file. Attachment-policy failures fail the download and preserve an existing
installer. Cancellation is checked again after attachment processing; Windows
attachment processing itself cannot be interrupted by the cancellation token.

Origin metadata follows Windows policy and filesystem capabilities. This is not
publisher-signature verification and does not guarantee a particular Windows
warning. The hardening tests exercise NTFS origin metadata after replacement,
policy-failure cleanup, and diagnostic network-path redaction.
