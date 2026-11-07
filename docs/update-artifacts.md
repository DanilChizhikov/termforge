# Update artifacts

Official stable releases are published at `https://github.com/DanilChizhikov/termforge/releases`. Packages cover Linux, macOS and Windows, each for x64 and arm64. The release workflow publishes only after all package builds and the configured platform smoke tests succeed.

Each release includes `release-manifest.json` with `schemaVersion: 1`, the exact stable `version`, and a `packages` array. Each entry contains `rid`, `fileName`, byte `size` and hexadecimal `sha256`. Archive names remain `termforge-<version>-<rid>.zip` for Windows and `.tar.gz` otherwise. Native libraries extract at runtime; the interface and configuration stay next to the executable.

The package downloader accepts only published stable releases from the official repository, validates the release metadata, requires exactly one matching package for the requested supported RID and version, and verifies its length and SHA-256 while streaming it to a temporary file. It returns a path only after verification. Failure or cancellation removes the incomplete file. It does not install or execute packages. Older releases without a compatible manifest remain available for manual updates but are rejected by this downloader.

Checksums detect damaged or substituted downloads when the manifest remains trustworthy. The manifest and packages share the GitHub account and HTTPS trust boundary; this is not an independent signature and does not protect against compromise of the publisher's account or release workflow.

Manual updates remain supported: download the correct archive from the official release page, compare its size and SHA-256 with the manifest, stop TermForge, preserve user data and configuration, then replace the application files. Do not replace application files while transfers or unsaved editing sessions are active. Automatic installation and rollback are implemented separately.
