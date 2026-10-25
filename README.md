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
