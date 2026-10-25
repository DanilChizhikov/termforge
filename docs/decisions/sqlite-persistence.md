# SQLite persistence

## Decision

- Storage is a single SQLite file accessed through EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`).
- `DbContext` and Fluent API configurations live in `src/Infrastructure/Persistence`. Entities have no data annotations.
- Migrations live only in `src/Infrastructure/Persistence.Migrations`. `Persistence` selects that assembly by name (`MigrationsAssembly("Persistence.Migrations")`) because it cannot reference the migrations project. `Entrypoint` references `Persistence.Migrations` so the assembly is deployed and loadable at runtime.
- The design-time factory is in `Persistence.Migrations` and builds the context through the same `UseTermForgeSqlite` extension as the host, so the tooling sees the same naming convention and converters.
- `dotnet-ef` is a local tool pinned in `.config/dotnet-tools.json`.

## Naming

`EFCore.NamingConventions` (`UseSnakeCaseNamingConvention`) produces snake_case tables, columns, keys and indexes. Tables are named explicitly in configurations. The EF history table keeps its default name `__EFMigrationsHistory` with snake_case columns.

## UTC

A model-wide convention applies `UtcDateTimeConverter` (and a nullable variant) to every `DateTime` property. Values are written as UTC (`Unspecified` is treated as UTC) and read back with `DateTimeKind.Utc`. New entities get this without extra configuration.

## Database location

Resolved by `DatabasePathResolver`:

1. `Persistence:DatabasePath` from configuration (appsettings, environment variable `Persistence__DatabasePath`, command line) if set. Tests use this.
2. Otherwise `Environment.SpecialFolder.LocalApplicationData` + `termforge/termforge.db`.

`LocalApplicationData` per platform: Windows `%LOCALAPPDATA%`, Linux `$XDG_DATA_HOME` or `~/.local/share`, macOS `~/.local/share`. The directory is created on startup.

## Migrations on startup

`Program.cs` calls `await app.MigrateDatabaseAsync()` between `Build()` and `Run()`. Kestrel starts listening inside `Run()`, so the host accepts no request before the schema is up to date. A hosted service was rejected: hosted-service ordering relative to the server is an implicit contract, an explicit awaited call is not. A migration failure aborts startup.

## First entity

`app_settings` (`key`, `value`, `updated_at`) is the only table. It exists to prove the pipeline; tables of later features add their own migrations.

## Limitations

- Generated migration files are edited after generation to remove comments (code style rule: no comments). Do this for every new migration.
- The default location is shared by all instances of the application of one OS user; concurrent instances share one file.
- Tests that boot the full host (for example `WebApplicationFactory`) must set `Persistence:DatabasePath` to a temporary file, otherwise they migrate the real user database.
