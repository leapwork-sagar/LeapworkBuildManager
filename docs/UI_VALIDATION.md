# UI and CI diagnostics

`./scripts/build.ps1` runs regression suites and synthetic visual scenarios. Results
are written below `artifacts/test-results/<run-id>/`: a Markdown summary, JSON suite
outcomes, compilation and test logs, visual-review PNGs, and available failure captures.
The script returns failure if any suite fails, while still running remaining suites.
GitHub Actions uploads these reports even after a failed test step, with 14-day retention.

## Visual review

The VisualChecks suite captures idle, Advanced, results, downloading, completion,
Help/Diagnostics, installer dialogs and 100/150/200% layouts using synthetic data and
an in-memory HTTP handler. No real downloads or user preferences are used. Captures
contain app windows only, never the desktop. Destination fields are replaced with
`Downloads` in the visual scenarios. Failure captures come only from test-process forms;
there may be no image for a compiler failure or an error before a window is displayed.

For a UI pull request, download its test-results artifact and compare matching PNGs
against the last approved run on the same runner, font and DPI configuration. Review
text clipping, symbol rendering, focus indicators, contrast, alignment and footer space.
This first stage provides visual review artifacts, not an automatic pixel-diff gate.
Do not bless reference images automatically. Native Windows rendering varies by OS/font;
introduce approved pixel baselines only once the capture environment is stable.

## Live display scaling

The portable EXE embeds per-monitor DPI awareness. A shared window handler owns scaling
for manually laid-out forms and handles Windows' suggested monitor rectangle. Geometry
is retained in logical units so repeated scale changes do not accumulate rounding error;
controls created lazily are captured at their current scale. Main and Help layouts reflow
after geometry changes. Built-in message boxes remain managed by Windows.

The regression suite sends DPI-change messages and checks repeated transitions, lazy
controls, horizontal overflow and Help footers. Before releasing a DPI change, also move
an open app and each dialog between real 100%, 150% and 200% monitors, including during
a download. Synthetic message tests cannot validate every display driver or font setup.
