# termforge

Local cross-platform web console for managing personal VPS and home servers over SSH: terminals, file manager, remote editing, Docker and basic monitoring.

Specification: `.codex/docs/PROJECT_SPEC.md`.

## Requirements

- .NET SDK 10.0.110 or newer feature band (see `global.json`)

## Layout

```
src/
  Api/
    Entrypoint/                 ASP.NET Core host, application lifecycle, HTTP and WebSocket endpoints
    Core/                       domain and application logic, one folder per feature
  Infrastructure/
    Ssh/                        SSH transport for terminal sessions and SFTP
    Persistence/                SQLite storage
    Persistence.Migrations/     database migrations
    Credentials/                OS credential store and encrypted fallback vault
  Tests/
    Core.Tests/
    Entrypoint.Tests/
    Persistence.Tests/
    Ssh.IntegrationTests/
scripts/                        helper scripts, described in scripts/README.md
```

## Build and test

```sh
dotnet build termforge.slnx
dotnet test termforge.slnx
```

## Run

```sh
dotnet run --project src/Api/Entrypoint
```

The backend listens on `http://127.0.0.1:5023` only.

## CI and releases

- `ci` builds and tests the solution on Linux, macOS and Windows for every push and PR to `develop` and `master`.
- `release-start` (manual run) bumps `<Version>` in `Directory.Build.props`, pushes `release/X.Y.Z` (from `develop`) or `hotfix/X.Y.Z` (from `master`) and opens a PR to `master`. Needs the `RELEASE_TOKEN` secret (PAT with `contents` and `pull-requests` write access).
- `release` runs when that PR is merged: publishes self-contained builds for `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, creates the GitHub release `X.Y.Z` with the archives and merges `master` back into `develop`.
- `pr-labeler` labels PRs by branch prefix (`feature/`, `bugfix/`, `hotfix/`, `release/`), labels group the release notes.
