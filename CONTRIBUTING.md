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

Open LeapworkBuildManager.sln for the production project. Use tests/Support for shared test helpers; register suites in scripts/build.ps1. See docs/RELEASING.md for packaging and CHANGELOG.md for release notes.
