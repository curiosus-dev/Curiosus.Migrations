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

var engine = new MigrationEngineBuilder(services)
    .UseScriptMigrations().FromDirectory("./Migrations")
    .UseCodeMigrations().FromAssembly(typeof(Program).Assembly)
    .ConfigureForPostgreSql(connectionString)
    .UseUpgradeMigrationPolicy(MigrationPolicy.ShortRunningAllowed)
    .Build();

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

- [Script migrations](https://curiosity-migrations.readthedocs.io/en/latest/features/script_migration/) come from a directory or from embedded resources. They can be split into [batches](https://curiosity-migrations.readthedocs.io/en/latest/features/script_migration/batches/) with `--BATCH:`. Directives such as `--CURIOSUS:TRANSACTION=OFF`, `--CURIOSUS:LONG-RUNNING=TRUE` and `--CURIOSUS:DEPENDENCIES=1.0, 1.1` set options for a script. The legacy `--CURIOSITY:` prefix still works.
- [Code migrations](https://curiosity-migrations.readthedocs.io/en/latest/features/code_migration/) are C# classes. They support [dependency injection](https://curiosity-migrations.readthedocs.io/en/latest/features/code_migration/di/) and [EF Core](https://curiosity-migrations.readthedocs.io/en/latest/features/code_migration/ef_integration/).
- [Downgrade](https://curiosity-migrations.readthedocs.io/en/latest/features/downgrade/) uses `*.down.sql` scripts or `IDowngradeMigration`. Call `DowngradeDatabaseAsync` with a target version set by `SetUpTargetVersion`.
- [Long-running migrations and policies](https://curiosity-migrations.readthedocs.io/en/latest/basics/#migration-policies): the upgrade and downgrade `MigrationPolicy` values decide which migrations run, so heavy data migrations can run separately from fast schema changes.
- [Pre-migrations](https://curiosity-migrations.readthedocs.io/en/latest/features/pre_migrations/), [variables](https://curiosity-migrations.readthedocs.io/en/latest/features/variables/), [transactions](https://curiosity-migrations.readthedocs.io/en/latest/features/transactions/), [dependencies](https://curiosity-migrations.readthedocs.io/en/latest/features/dependencies/), [journal](https://curiosity-migrations.readthedocs.io/en/latest/features/journal/) and [custom migration providers](https://curiosity-migrations.readthedocs.io/en/latest/features/migration_providers/).

## See also

- [Curiosus.Migrations.PostgreSQL](https://www.nuget.org/packages/Curiosus.Migrations.PostgreSQL): PostgreSQL provider
- [Curiosus.Migrations.SqlServer](https://www.nuget.org/packages/Curiosus.Migrations.SqlServer): SQL Server provider
- [Curiosus.Migrations.Utils](https://www.nuget.org/packages/Curiosus.Migrations.Utils): helpers for batched mass data updates
- [Documentation](https://curiosity-migrations.readthedocs.io/) and [Quick Start](https://curiosity-migrations.readthedocs.io/en/latest/quickstart/)
- [Curiosus.Migrations](https://github.com/curiosus-dev/Curiosus.Migrations): repository and all packages
