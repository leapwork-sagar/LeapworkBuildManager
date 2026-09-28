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

The draft workflow checks the tag against `VersionInfo.Number` and the latest
changelog heading before building. Before upload it verifies required EXE/checksum
assets, EXE file and assembly versions, and the SHA-256 manifest contents.
Historical headings such as `v1.22` mean `1.22.0.0`; nonzero patch/revision values
must be included. Run `scripts/check-release.ps1` with `-Tag` and optionally both
`-ExecutablePath` and `-ChecksumPath` to perform the same checks locally.
Windows CI tests rejection of invalid release metadata and assets.

Drafts start with [the release template](RELEASE_TEMPLATE.md): Improvements,
Fixes, Requirements and Known limitations, followed by generated PR/commit notes.
Replace placeholder bullets and review all sections before publishing.

## Release workflow permissions

The build job has read-only repository access. It builds and tests the app, validates
release metadata and transfers only the EXE, checksum and notes as a workflow artifact.
The publish job downloads that exact artifact by ID from the same run and checks its
digest and SHA-256 manifest before creating a draft. Only this job has `contents: write`;
it does not check out or execute repository code. All checkouts disable persisted
credentials and use the Node.js 24-based checkout action.

Release immutability is enabled in repository settings. New releases become immutable
when published: prepare all assets in the draft and verify them before publishing.
Correct a published binary with a new version rather than replacing its asset or tag.
This setting does not retroactively lock releases published before it was enabled.

## Provenance and release source

Use **Prepare draft release** for future public releases. Review and publish its draft;
do not replace its tested EXE with a local build. The publishing job verifies the
transferred checksum, creates GitHub build provenance, verifies the signed bundle,
and attaches `LeapworkBuildManager.exe.sigstore.json` alongside the EXE and checksum.
A failed attestation or verification prevents draft creation. The build job retains
read-only repository permissions; signing permissions are restricted to the publish job.

Verify a future attested download using the GitHub CLI:

```text
gh attestation verify LeapworkBuildManager.exe --repo leapwork-sagar/LeapworkBuildManager --bundle LeapworkBuildManager.exe.sigstore.json
```

The record identifies the release workflow and source commit. It is not an Authenticode
signature and does not remove Windows reputation warnings. Older releases are not
retroactively attested. Update the four-part version and changelog before running the
workflow; published versions remain immutable.
