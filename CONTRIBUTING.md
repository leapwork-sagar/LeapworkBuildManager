# Contributing

Use Windows with .NET Framework 4.7.2 or later and PowerShell. No NuGet dependencies
or network access are needed for the current build/test scripts. Run:

```powershell
./scripts/build.ps1
./scripts/release.ps1
```

Production code belongs in src/LeapworkBuildManager. Tests belong in tests; keep
network tests deterministic with HTTP handlers rather than downloading installers.
Update explicit Compile entries in the csproj when adding production files.
Increment VersionInfo.Number before publishing a release. Artifact names use all
four version components. Existing outputs are refused by default; use
`./scripts/release.ps1 -Overwrite` only for an intentional local replacement. Never commit generated
binaries, logs, user settings, or release ZIPs. Follow .editorconfig/.gitattributes.

The source lives at [leapwork-sagar/LeapworkBuildManager](https://github.com/leapwork-sagar/LeapworkBuildManager).
Create a feature branch and open a pull request to `main`. The `test` check from
[Windows build and tests](https://github.com/leapwork-sagar/LeapworkBuildManager/actions/workflows/windows.yml)
must pass before merging, and the branch must be up to date with `main`.
Additional reviewer approval is not mandatory for this single-maintainer repository.
Administrator bypass, force pushes and deletion of `main` are disabled.

The workflow passed on GitHub-hosted Windows for v1.22.0.0, including regression tests
and MSBuild: [verified run](https://github.com/leapwork-sagar/LeapworkBuildManager/actions/runs/36314477433).
Keep manual release checks for real downloads, sleep/wake and display scaling.
Publish binaries through [GitHub Releases](https://github.com/leapwork-sagar/LeapworkBuildManager/releases).

## Tracking and automation

Use the bug-report or feature-request form under **Issues > New issue**. Keep one
accepted improvement per issue, with clear acceptance criteria. Apply `bug` for
defects, `enhancement` for features, `ui` for interface work and `maintenance` for
repository upkeep; `documentation` can be combined with these. Assign accepted
work to a milestone when its scope is agreed, without implying a release date.
Link the issue in the PR and use `Closes #number` when the change completes it.

The PR template records changes, validation and UI screenshots. Windows CI runs
on PRs and pushes to `main`, cancelling superseded runs. The Documentation links
workflow checks Markdown links and referenced images on PRs, `main`, weekly and
on manual request; external outages may require a rerun. Dependabot proposes
weekly GitHub Actions updates for review; updates are not automatically merged.
See the release guide for the manually triggered draft-release workflow.

Formatting CI checks changed text files for whitespace, conflict markers and
EditorConfig compliance, and changed Markdown with markdownlint. It reports
problems without rewriting files. Existing untouched files are not reformatted.
Git keeps C# and PowerShell working files as CRLF; documentation and YAML use LF.
The labeler adds `documentation`, `ui` and `maintenance` from changed paths using
trusted base-branch configuration. It preserves manually assigned labels and
never checks out or executes pull-request code.

Open LeapworkBuildManager.sln for the production project. Use tests/Support for shared test helpers; register suites in scripts/build.ps1. See docs/RELEASING.md for packaging and CHANGELOG.md for release notes.
