# Curiosus.Migrations

Database migration framework for .NET: raw SQL and C# code migrations, downgrades, long-running data migrations and per-environment policies for PostgreSQL and SQL Server.

[![Build](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml/badge.svg?branch=main)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml)
[![License](https://img.shields.io/github/license/curiosus-dev/Curiosus.Migrations)](https://github.com/curiosus-dev/Curiosus.Migrations/blob/main/LICENSE)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Curiosus.Migrations)](https://www.nuget.org/packages/Curiosus.Migrations)
[![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/coverage.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml)
[![Docs](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/docs.yml/badge.svg?branch=main)](https://curiosus-dev.github.io/Curiosus.Migrations/)

> **Renamed:** formerly `Curiosity.Migrations*` by SIIS Ltd. Since 5.0.0 the packages are published as `Curiosus.Migrations*`
> by [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev). To migrate, replace `Curiosity` with `Curiosus` in package references and code.
> `-- CURIOSITY:` directives in SQL scripts keep working.

## Why use it

Curiosus.Migrations is a database migration framework for .NET (`net9.0` and `net10.0`; stay on 5.x for older runtimes) that gives you precise control over how your database evolves. It keeps raw SQL scripts and C# code migrations in one ordered history, so a schema change and the data migration that goes with it are versioned, applied and rolled back together.

Unlike ORM-specific migration tools, Curiosus.Migrations is database-focused and designed for scenarios where you need fine-grained control over migration execution, especially for large production databases where heavy data migrations must not block a deployment.

<table>
  <tr>
    <td width="50%" valign="top">
      <h3>🔧 Precise Control</h3>
      <p>Write raw SQL when you need optimal performance, or use C# code when you need complex logic. You control exactly what runs against your database.</p>
    </td>
    <td width="50%" valign="top">
      <h3>🚀 Production-Ready</h3>
      <p>Long-running migration support and policies that decide what runs in each environment: quick schema changes on deployment, heavy data migrations separately.</p>
    </td>
  </tr>
  <tr>
    <td width="50%" valign="top">
      <h3>🔄 Bidirectional</h3>
      <p>Downgrade scripts and code migrations roll the database back to a target version when a deployment doesn't go as planned.</p>
    </td>
    <td width="50%" valign="top">
      <h3>📊 Progressive Migrations</h3>
      <p>Separate long-running data migrations from quick schema changes to keep your application responsive during upgrades.</p>
    </td>
  </tr>
  <tr>
    <td width="50%" valign="top">
      <h3>🧪 Testability</h3>
      <p>Create and initialize test databases with specific migration states for reliable integration testing.</p>
    </td>
    <td width="50%" valign="top">
      <h3>🧩 Extensibility</h3>
      <p>Customize where migrations come from, how they're logged, and how they're applied to fit your workflow.</p>
    </td>
  </tr>
</table>

## Features

### Migration Types

- **[Script Migrations](https://curiosus-dev.github.io/Curiosus.Migrations/features/script_migration)**: Write raw SQL for direct database access
    - [Batched Execution](https://curiosus-dev.github.io/Curiosus.Migrations/features/script_migration/batches): Split large scripts into manageable chunks
    - Full support for database-specific SQL features and optimizations

- **[Code Migrations](https://curiosus-dev.github.io/Curiosus.Migrations/features/code_migration)**: Implement migrations in C# for complex scenarios
    - [Dependency Injection](https://curiosus-dev.github.io/Curiosus.Migrations/features/code_migration/di): Use your application's services in migrations
    - [Entity Framework Integration](https://curiosus-dev.github.io/Curiosus.Migrations/features/code_migration/ef_integration): Leverage EF Core when needed
    - Any C# logic: data transformations, calls to your services, batched updates

### Safety and Control

- **[Policies](https://curiosus-dev.github.io/Curiosus.Migrations/basics#migration-policies)**: Control which migrations run in different environments
- **[Dependencies](https://curiosus-dev.github.io/Curiosus.Migrations/features/dependencies)**: Specify explicit requirements between migrations
- **[Downgrade Migrations](https://curiosus-dev.github.io/Curiosus.Migrations/features/downgrade)**: Safely roll back changes when needed
- **[Transactions](https://curiosus-dev.github.io/Curiosus.Migrations/features/transactions)**: Configure transaction behavior per migration
- **[Long-running vs Short-running](https://curiosus-dev.github.io/Curiosus.Migrations/basics#migration-types-short-running-vs-long-running)**: Separate quick schema changes from data-intensive operations

### Extensibility

- **[Migration Providers](https://curiosus-dev.github.io/Curiosus.Migrations/features/migration_providers)**: Source migrations from files, embedded resources, etc.
- **[Variables](https://curiosus-dev.github.io/Curiosus.Migrations/features/variables)**: Dynamic value substitution in migrations
- **[Pre-migrations](https://curiosus-dev.github.io/Curiosus.Migrations/features/pre_migrations)**: Run setup scripts before main migrations
- **[Custom Journal](https://curiosus-dev.github.io/Curiosus.Migrations/features/journal)**: Configure how applied migrations are tracked

## Quick start

### Installation

```bash
# Install core package
dotnet add package Curiosus.Migrations

# Install database provider (PostgreSQL or SQL Server)
dotnet add package Curiosus.Migrations.PostgreSQL
# or
dotnet add package Curiosus.Migrations.SqlServer
```

### Basic Setup

Put SQL scripts named by version into a directory: `1.0-create_users.sql`, `1.1.up.sql` with its `1.1.down.sql`, and so on. Then configure and run the engine:

```csharp
using System.Reflection;
using Curiosus.Migrations;
using Curiosus.Migrations.PostgreSQL;

var builder = new MigrationEngineBuilder();

// UseScriptMigrations() and UseCodeMigrations() return the providers, not the builder: configure them separately
builder.UseScriptMigrations().FromDirectory("./Migrations");
builder.UseCodeMigrations().FromAssembly(Assembly.GetExecutingAssembly());
builder.ConfigureForPostgreSql("Host=localhost;Database=myapp;Username=postgres;Password=secret");
builder.UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed);

var migrationEngine = builder.Build();
var result = await migrationEngine.UpgradeDatabaseAsync();

if (!result.IsSuccessfully)
{
    throw new InvalidOperationException(
        $"Migration {result.FailedMigration?.Version} failed: {result.ErrorCode} {result.ErrorMessage}",
        result.Exception);
}

Console.WriteLine($"Applied {result.AppliedMigrations.Count} migrations");
```

The engine creates the database and the migration history table when they are missing. A complete runnable example with script, code and downgrade migrations is in [samples/Curiosus.Migrations.Sample](https://github.com/curiosus-dev/Curiosus.Migrations/tree/main/samples/Curiosus.Migrations.Sample).

Get started quickly with the [**Quick Start Guide**](https://curiosus-dev.github.io/Curiosus.Migrations/quickstart) or dive into [**Core Concepts**](https://curiosus-dev.github.io/Curiosus.Migrations/basics).

### Running in production

> **No concurrency lock yet.** The engine doesn't lock the database while it migrates, so two instances starting at
> the same time can apply the same migration twice or fail on the journal. Until
> [#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32) lands, run migrations from one place: a Kubernetes
> Job or init container, a deployment pipeline step, or the startup of a single replica.

## Supported Databases

<table>
  <tbody>
    <tr>
      <td align="center" valign="middle">
        <img src="https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/main/docs/images/postgresql.png" width="200" />
        <br />
        <b>PostgreSQL</b>
      </td>
      <td align="center" valign="middle">
        <img src="https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/main/docs/images/sqlserver.svg" width="200" />
        <br />
        <b>SQL Server</b>
      </td>
    </tr>
  </tbody>
</table>

MySQL/MariaDB ([#37](https://github.com/curiosus-dev/Curiosus.Migrations/issues/37)) and SQLite ([#38](https://github.com/curiosus-dev/Curiosus.Migrations/issues/38)) are planned for v7, after the engine rework that makes a new database a small dialect ([#36](https://github.com/curiosus-dev/Curiosus.Migrations/issues/36)). A custom `IMigrationConnection` can add any other database.

## Comparing with Alternatives

As of October 2026. ✅ supported, ⚠️ partly or with caveats, ❌ not supported, 💰 paid editions only.

| | Curiosus.Migrations | EF Core Migrations | FluentMigrator | DbUp | grate | Flyway | Liquibase |
|---|---|---|---|---|---|---|---|
| Migrations written as | SQL + C# code | C# generated from the EF model (+ raw SQL) | Fluent C# API (+ raw SQL) | SQL (+ `IScript` code) | SQL | SQL (+ Java) | XML/YAML/JSON/SQL changelogs |
| Databases | PostgreSQL, SQL Server | Any EF Core relational provider | 6 | 7 | 5 | 20+ | 50+ |
| Downgrade / rollback | ✅ Hand-written, free | ✅ Generated `Down()` | ✅ `Down()`, auto-reversing | ❌ | ❌ | 💰 Undo | ✅ (targeted rollback 💰) |
| Long-running vs short-running policies | ✅ | ❌ | ⚠️ Tags, profiles | ⚠️ Script filters | ⚠️ Environment scripts | ⚠️ Cherry-pick 💰 | ⚠️ Contexts, labels |
| Dependencies between migrations | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ Changelog order |
| C# migrations with DI | ✅ | ⚠️ No DI in migrations | ✅ | ⚠️ `IScript` | ❌ | ❌ | ❌ |
| Concurrency lock | ❌ [#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32) | ✅ Since EF Core 9 | ❌ | ❌ | Not documented | ✅ | ✅ |
| Checksums, drift detection | ❌ [#33](https://github.com/curiosus-dev/Curiosus.Migrations/issues/33) | ⚠️ Pending model changes check | ❌ | ❌ | ✅ | ✅ (drift report 💰) | ✅ (drift 💰) |
| Repeatable migrations | ❌ [#39](https://github.com/curiosus-dev/Curiosus.Migrations/issues/39) | ❌ | ⚠️ Maintenance stages | ✅ | ✅ | ✅ | ✅ |
| CLI, dry-run, SQL preview | ❌ [#40](https://github.com/curiosus-dev/Curiosus.Migrations/issues/40), [#42](https://github.com/curiosus-dev/Curiosus.Migrations/issues/42) | ✅ `dotnet ef`, bundles, scripts | ✅ `dotnet-fm` | ⚠️ Library | ✅ | ✅ | ✅ |
| License | MIT | MIT | Apache-2.0 | MIT | MIT | Apache-2.0 core, paid editions | FSL core, paid editions |

The gaps are planned for v7, see the [roadmap](https://github.com/curiosus-dev/Curiosus.Migrations/issues/44). Evolve
had checksums and locking but has had no stable release since 3.2.0 (June 2023); RoundhousE is superseded by grate.

**Choose Curiosus.Migrations** when you mix hand-tuned SQL with C# data migrations on PostgreSQL or SQL Server, need
heavy backfills kept out of the deployment path, and want free downgrades. **Choose something else** when your app is
EF Core-centric with one schema owner (EF Core Migrations), you target many database engines or want a fluent schema DSL
(FluentMigrator), you only need forward-only SQL scripts (DbUp, or grate with hash checks and a CLI), or a DBA-led team
needs compliance and drift reports (Flyway, Liquibase).

For a detailed comparison, see [The Philosophy Behind Curiosus.Migrations](https://curiosus-dev.github.io/Curiosus.Migrations/philosophy#comparison-to-net-alternatives).

## Available packages

| Package | Version | Downloads | Coverage |
|---------|---------|-----------|----------|
| [Curiosus.Migrations](https://github.com/curiosus-dev/Curiosus.Migrations/blob/main/src/Curiosus.Migrations/README.md) | [![NuGet](https://img.shields.io/nuget/v/Curiosus.Migrations.svg)](https://www.nuget.org/packages/Curiosus.Migrations/) | [![NuGet](https://img.shields.io/nuget/dt/Curiosus.Migrations)](https://www.nuget.org/packages/Curiosus.Migrations) | [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/Curiosus.Migrations.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml) |
| [Curiosus.Migrations.PostgreSQL](https://github.com/curiosus-dev/Curiosus.Migrations/blob/main/src/Curiosus.Migrations.PostgreSQL/README.md) | [![NuGet](https://img.shields.io/nuget/v/Curiosus.Migrations.PostgreSQL.svg)](https://www.nuget.org/packages/Curiosus.Migrations.PostgreSQL/) | [![NuGet](https://img.shields.io/nuget/dt/Curiosus.Migrations.PostgreSQL)](https://www.nuget.org/packages/Curiosus.Migrations.PostgreSQL) | [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/Curiosus.Migrations.PostgreSQL.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml) |
| [Curiosus.Migrations.SqlServer](https://github.com/curiosus-dev/Curiosus.Migrations/blob/main/src/Curiosus.Migrations.SqlServer/README.md) | [![NuGet](https://img.shields.io/nuget/v/Curiosus.Migrations.SqlServer.svg)](https://www.nuget.org/packages/Curiosus.Migrations.SqlServer/) | [![NuGet](https://img.shields.io/nuget/dt/Curiosus.Migrations.SqlServer)](https://www.nuget.org/packages/Curiosus.Migrations.SqlServer) | [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/Curiosus.Migrations.SqlServer.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml) |
| [Curiosus.Migrations.Utils](https://github.com/curiosus-dev/Curiosus.Migrations/blob/main/src/Curiosus.Migrations.Utils/README.md) | [![NuGet](https://img.shields.io/nuget/v/Curiosus.Migrations.Utils.svg)](https://www.nuget.org/packages/Curiosus.Migrations.Utils/) | [![NuGet](https://img.shields.io/nuget/dt/Curiosus.Migrations.Utils)](https://www.nuget.org/packages/Curiosus.Migrations.Utils) | [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/Curiosus.Migrations.Utils.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml) |

## Support

* [GitHub Issues](https://github.com/curiosus-dev/Curiosus.Migrations/issues) - Report bugs or request features

## License

Curiosus.Migrations is licensed under the [MIT License](https://github.com/curiosus-dev/Curiosus.Migrations/blob/main/LICENSE).
