# Repository preparation

Use this project directory as the repository root. The source-only release ZIP is
also ready to extract into an empty repository. Generated artifacts/releases are
ignored. No workspace screenshots, private preferences, logs or older binary
snapshots belong in the repository. No remote or upload has been configured.

Pre-upload review: this source uses the Leapwork Azure blob endpoint to discover
installers. The endpoint is intentional, but confirm it is appropriate for your
chosen repository visibility. Logo/icon assets were supplied for this application;
no open-source license or permission to publish Leapwork branding is inferred.
Choose private visibility until the owner has approved sharing scope. Synthetic
credentials and paths in privacy tests are test data, not production secrets.

Do not add a public license without an owner decision. Review git diff --cached
before pushing. Publish compiled utilities as release attachments, not source files.
