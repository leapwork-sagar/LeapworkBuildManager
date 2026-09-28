# Changelog

## v1.23

- Launch Open folder through the trusted Windows Explorer path.
- Hide forward-slash network paths when exporting diagnostics with path hiding enabled.
- Apply Windows Attachment Services before committing downloads, preserving existing installers if attachment processing fails.
- Verify internet-origin metadata after replacement on NTFS and cover attachment rejection with regression tests.

## v1.22

- Check saved installers on history selection, distinguish missing from inaccessible locations, and offer Open folder, Retry check or Download again.
- Warn after 30 seconds waiting for download data, clear stale speed/ETA and recover when data resumes. Retain the 60-second inactivity timeout; retry restarts downloads.


## v1.21

- Guard automatic keyboard focus changes against navigation and application switches.
- Shorten destination paths, retain full-path hover text and add Copy folder path.
- Add contextual action tooltips without duplicating existing path/status tooltips.


## v1.20

- Align Diagnostics link actions with responsive spacing.
- Organize form, diagnostics, formatting and test files by responsibility.
- Add a root solution and topic-based documentation.

## Changes in v1.19

- Enforces HTTPS, port 443 and the exact approved Leapwork blob host before every
  request and redirect. At most five redirect hops are followed. Relative redirects
  are supported; other hosts, embedded credentials and HTTP downgrades are rejected.
- Redacts forward-slash Windows paths and file URIs as well as native Windows paths.
- Centralizes disposal so direct disposal also releases services, timers and the
  system-event subscription. Normal shutdown still flushes settings and logs.
- Uses explicit independent settings copies instead of XML serialization for cloning;
  file serialization, revision checks and transactional commits remain in place.
- Refactors form setup, substantial event handlers, button labels and diagnostic
  formatting. Network diagnostics now include captured build/URL/operation context,
  and operation-start events are no longer duplicated.
- Centralizes operational limits, removes unused imports and updates explanatory
  comments. This release does not add log batching or change download finalization.
- Release filenames retain all four version components (v1.19.0.0). Packaging refuses
  existing artifacts unless explicitly invoked with `-Overwrite`.

## Changes in v1.18

- Cancel/Escape confirm after 10 MB or 10 seconds of download activity. Closing an
  active download always confirms; sleep interruption never opens a prompt.
- Progress announcements occur at ten-percent milestones rather than every update.
  Validation errors are announced when leaving the field or submitting.
- Friendly failure guidance is separate from full exception diagnostics.
- Async UI entry points guard setup failures and restore usable controls.
- Logs persist in the settings folder's Logs subfolder: session.log plus three
  rotated backups, approximately 1 MB each. Queue capacity is 256 events; overload
  drops entries instead of blocking. Logging errors/drop counts appear in diagnostics.
- Logs include severity, category, operation IDs, HTTP status, cancellation reasons,
  nested exceptions, progress milestones and cleanup outcomes. Records are capped
  at 16K characters. Normal exit drains logs for up to two seconds; abrupt exit,
  overload or logging IO failures can lose entries.
- Persistent logs redact local Windows paths, URL credentials, query values and
  fragments. Copy diagnostics always sanitizes URLs and asks whether to hide paths.
  Redaction is best-effort; inspect reports before sharing them.
- Git-ready source includes ignore/formatting rules, contributor documentation and
  a Windows GitHub Actions workflow template. No upload or remote setup is performed.

## Changes in v1.17

- Neutral guidance while entering an incomplete build number.
- Specific errors for invalid quarters and incomplete input after leaving the field.
- Automatic-dot placeholder, fixed message space and accessible guidance.
- Shared validation feedback in automatic and manual search; availability still requires a network check.
- Copy-paste handling is unchanged, including silent rejection of unsupported pasted text.

## Previous v1.16 changes

- Fixes download-panel repaint lines with buffered painting and full-card invalidation.
- Redesigns the existing-installer dialog with filename, destination, Open folder,
  clear explanations, and Save another copy as the primary action. Footer actions
  remain accessible on small windows.
- Avoids empty startup animations, batches initial control creation and reduces
  startup to two full layout passes. Resolves the font family once.
- Uses brief 180 ms panel transitions without full layout on every frame. Repeated
  targets do not restart the animation; reversing continues from the current height.
  Completion is immediate and cancellation resets progress immediately.
- Skips unchanged settings writes and combines adjacent edits over 400 ms. Pending
  changes flush on normal exit; completed downloads flush history immediately.
  Abrupt termination during the debounce interval can lose the newest pending edit.
- Reuses destination inspection when the chosen path is unchanged, while retaining
  the final free-space check against the download response before writing.
- Includes automated regression checks and the reproducible source tree.

### Startup measurement

Six paired local process runs with the same 20-entry history fixture showed median
ready time of 718.4 ms in v1.15 and 698.6 ms in v1.16. The ranges overlap, so this is
not evidence of a large startup speedup. Idle animation ticks fell from a median
15.5 to zero. These timings start inside the test process and exclude Windows/CLR
launch and the normal single-instance activation path; they are not cold-boot results.
