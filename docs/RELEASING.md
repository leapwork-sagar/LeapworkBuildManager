# Building and releasing

Published binaries, checksums and notes are on [GitHub Releases](https://github.com/leapwork-sagar/LeapworkBuildManager/releases).
The [latest release](https://github.com/leapwork-sagar/LeapworkBuildManager/releases/latest) is the recommended download.

Use Windows, PowerShell and .NET Framework 4.7.2+. The scripts use the installed
Framework compiler and require no NuGet packages. Paths resolve from the script location.

1. Run `./scripts/build.ps1` to compile production code and all regression suites.
2. Build `LeapworkBuildManager.sln` with MSBuild to verify explicit project includes.
3. Update `src/LeapworkBuildManager/VersionInfo.cs`, README and CHANGELOG for a release.
4. Run `./scripts/release.ps1`. It reruns tests and the portable smoke check, then
   creates the EXE under `releases/v<version>/`, a full ZIP and a source-only ZIP.

Release names retain all four version components. Existing outputs are refused;
`-Overwrite` is only for an intentional replacement. Older releases stay intact.
Source archives include the solution, documentation, scripts and tests, excluding
generated bin/obj/artifacts directories. Commit source, not binaries or user settings.

`./scripts/smoke-portable.ps1` loads the production form with isolated preferences
from an EXE-only directory. It checks assets, startup and shutdown, not SmartScreen
or a machine missing the required runtime. Before distribution, manually review
the UI and download flow. The output is portable but unsigned and not self-contained.

After source changes pass the required PR checks and merge to `main`, create a
version tag and GitHub Release for that commit. Attach the tested EXE and
`SHA256SUMS.txt`, verify the uploaded digest, and include requirements and release
notes. Publication remains a manual maintainer action.

## Prepare a draft on GitHub

After merging a new four-part version in `VersionInfo.cs` and updating the changelog,
open **Actions > Prepare draft release > Run workflow** and select `main`.
The workflow refuses an existing release version, runs regression tests and the
portable smoke check, verifies MSBuild, and attaches the EXE and `SHA256SUMS.txt`
to a draft with generated release notes. It uses the exact workflow commit.
It never publishes automatically. Review the assets, notes and manual checks above
before publishing. If upload fails, inspect the draft before retrying; existing
drafts are intentionally not overwritten. Running on another branch is skipped.
