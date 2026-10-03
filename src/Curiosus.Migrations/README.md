# Curiosus.Migrations

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Migrations)](https://www.nuget.org/packages/Curiosus.Migrations) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Migrations)](https://www.nuget.org/packages/Curiosus.Migrations) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/Curiosus.Migrations.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml)

Core engine of Curiosus.Migrations, a database migration framework for .NET that combines raw SQL scripts and C# code migrations.
Install it together with a database provider:
[PostgreSQL](https://www.nuget.org/packages/Curiosus.Migrations.PostgreSQL) or [SQL Server](https://www.nuget.org/packages/Curiosus.Migrations.SqlServer).

## Installation

```bash
dotnet add package Curiosus.Migrations
dotnet add package Curiosus.Migrations.PostgreSQL # or Curiosus.Migrations.SqlServer
```

## Usage

Put SQL scripts named by version (`1.0.sql`, `1.1-add_users.sql`, `2.0.down.sql`) into a directory. Then configure and run the engine:

```csharp
using Curiosus.Migrations;
using Curiosus.Migrations.PostgreSQL;

var builder = new MigrationEngineBuilder(services);
builder.UseScriptMigrations().FromDirectory("./Migrations"); // returns the provider, not the builder
builder.UseCodeMigrations().FromAssembly(typeof(Program).Assembly);
builder.ConfigureForPostgreSql(connectionString);
builder.UseUpgradeMigrationPolicy(MigrationPolicy.ShortRunningAllowed);
var engine = builder.Build();

var result = await engine.UpgradeDatabaseAsync(cancellationToken);
if (!result.IsSuccessfully)
    throw new InvalidOperationException($"{result.ErrorCode}: {result.ErrorMessage}");
```

The engine creates the database and the migration history table if they do not exist. To register
`IMigrationEngine` as a singleton, call `services.AddMigrations(builder => ...)` instead.

A code migration is a class derived from `CodeMigration`:

```csharp
public class SeedRolesMigration : CodeMigration
{
    public override MigrationVersion Version => new(1, 2);
    public override string? Comment => "Seed default roles";

    public override Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        MigrationConnection.ExecuteNonQuerySqlAsync("INSERT INTO roles (name) VALUES ('admin');", null, cancellationToken);
}
```

## Key concepts

- [Script migrations](https://curiosus-dev.github.io/Curiosus.Migrations/features/script_migration) come from a directory or from embedded resources. They can be split into [batches](https://curiosus-dev.github.io/Curiosus.Migrations/features/script_migration/batches) with `--BATCH:`. Directives such as `-- CURIOSUS: TRANSACTION = OFF`, `-- CURIOSUS: LONG-RUNNING = TRUE` and `-- CURIOSUS: DEPENDENCIES = 1.0, 1.1` set options for a script (the upgrade script's directives apply to its downgrade script too). The legacy `--CURIOSITY:` prefix still works.
- [Code migrations](https://curiosus-dev.github.io/Curiosus.Migrations/features/code_migration) are C# classes. They support [dependency injection](https://curiosus-dev.github.io/Curiosus.Migrations/features/code_migration/di) and [EF Core](https://curiosus-dev.github.io/Curiosus.Migrations/features/code_migration/ef_integration).
- [Downgrade](https://curiosus-dev.github.io/Curiosus.Migrations/features/downgrade) uses `*.down.sql` scripts or `IDowngradeMigration`. Call `DowngradeDatabaseAsync` with a target version set by `SetUpTargetVersion`.
- [Long-running migrations](https://curiosus-dev.github.io/Curiosus.Migrations/basics#migration-types-short-running-vs-long-running) and [policies](https://curiosus-dev.github.io/Curiosus.Migrations/basics#migration-policies): the upgrade and downgrade `MigrationPolicy` values decide which migrations run, so heavy data migrations can run separately from fast schema changes.
- [Pre-migrations](https://curiosus-dev.github.io/Curiosus.Migrations/features/pre_migrations), [variables](https://curiosus-dev.github.io/Curiosus.Migrations/features/variables), [transactions](https://curiosus-dev.github.io/Curiosus.Migrations/features/transactions), [dependencies](https://curiosus-dev.github.io/Curiosus.Migrations/features/dependencies), [journal](https://curiosus-dev.github.io/Curiosus.Migrations/features/journal) and [custom migration providers](https://curiosus-dev.github.io/Curiosus.Migrations/features/migration_providers).

## See also

- [Curiosus.Migrations.PostgreSQL](https://www.nuget.org/packages/Curiosus.Migrations.PostgreSQL): PostgreSQL provider
- [Curiosus.Migrations.SqlServer](https://www.nuget.org/packages/Curiosus.Migrations.SqlServer): SQL Server provider
- [Curiosus.Migrations.Utils](https://www.nuget.org/packages/Curiosus.Migrations.Utils): helpers for batched mass data updates
- [Documentation](https://curiosus-dev.github.io/Curiosus.Migrations/) and [Quick Start](https://curiosus-dev.github.io/Curiosus.Migrations/quickstart)
- [Curiosus.Migrations](https://github.com/curiosus-dev/Curiosus.Migrations): repository and all packages
