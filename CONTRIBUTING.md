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

The included workflow is a GitHub Actions template. For another Git host, run the
same scripts on a Windows runner. Hosted UI tests require an interactive Windows
session; validate runner support before relying on CI as the only release gate.
No repository or remote has been created or configured automatically.

Open LeapworkBuildManager.sln for the production project. Use tests/Support for shared test helpers; register suites in scripts/build.ps1. See docs/RELEASING.md for packaging and CHANGELOG.md for release notes.
