# Repository maintenance

The public source repository is [leapwork-sagar/LeapworkBuildManager](https://github.com/leapwork-sagar/LeapworkBuildManager).
Use this project directory as the repository root. Portable binaries and checksums
are published through [GitHub Releases](https://github.com/leapwork-sagar/LeapworkBuildManager/releases/latest).
Generated artifacts/releases are ignored. Private preferences, logs and older binary
snapshots do not belong in Git. Reviewed documentation screenshots belong in `docs/images`;
temporary workspace screenshots remain outside the repository.

The application intentionally uses the Leapwork Azure blob endpoint to discover
installers. Do not add credentials or signed access URLs to source or documentation.
Synthetic credentials and paths in privacy tests are test data. Public visibility
does not grant a software or branding license; licensing remains an owner decision.

Do not add a public license without an owner decision. Review git diff --cached
before pushing. Publish compiled utilities as release attachments, not source files.

Use a feature branch and pull request for protected `main`. The `test` status check
must pass and the PR must be up to date. Administrator bypass is disabled.
