---
sidebar_position: 3
sidebar_label: Philosophy
---

# The Philosophy Behind `Curiosus.Migrations`

## Introduction

`Curiosus.Migrations` is a .NET database migration framework for applications that need precise control over schema and data changes, especially on large production databases. Unlike ORM-focused migration tools, `Curiosus.Migrations` embraces both direct SQL and C# code approaches and keeps them in one ordered history, so developers keep full control over what runs and when.

## Core Principles

### 1. Migration as Code

`Curiosus.Migrations` embraces the migration-as-code philosophy, treating database changes the same way you treat application code changes:

- **Version Control**: Every database change is versioned and tracked in your source repository
- **History Tracking**: The [journal](./features/journal.md) table records every applied migration with its version, name and time
- **Environment Consistency**: Same migration process across development, testing, and production
- **Schema-Code Alignment**: Database schema changes are synchronized with application code changes

### 2. Raw SQL Migrations

The library prioritizes raw SQL migrations, giving developers precise control over database operations:

- **Performance Optimization**: Write highly optimized SQL for critical operations
- **Database-Specific Features**: Leverage database-specific features and syntax
- **Execution Transparency**: What you write is exactly what executes against your database
- **Full Control**: No "magic" or auto-generated queries with unexpected behavior

```sql
-- 20230615-add_user_indexes.sql: adds optimized indexes with database-specific options
-- CREATE INDEX CONCURRENTLY can't run in a transaction, so turn it off for this migration
-- CURIOSUS: TRANSACTION = OFF
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_users_email 
ON users(email) 
WHERE email IS NOT NULL;

-- Using database-specific functionality directly
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX IF NOT EXISTS idx_users_name_trigram 
ON users USING gin(name gin_trgm_ops);
```

### 3. Code Migrations

For complex data transformation scenarios, `Curiosus.Migrations` supports code-based migrations:

- **Complex Logic**: Implement sophisticated business rules during migration
- **External Integration**: Connect to external systems during migration process
- **Batched Processing**: Efficiently process large datasets with controlled resource usage
- **.NET Ecosystem**: Leverage the full power of C# and the .NET ecosystem

```csharp
using System.Data.Common;
using Curiosus.Migrations;
using Curiosus.Migrations.Utils;
using Microsoft.Extensions.Logging;

public class NormalizeUserEmails : MassUpdateCodeMigrationBase
{
    // Process data in batches with a pause between them to reduce database load
    public NormalizeUserEmails() : base(TimeSpan.FromMilliseconds(100)) { }

    public override MigrationVersion Version => new(20230616);
    public override string? Comment => "Normalize email addresses to lowercase";

    public override async Task UpgradeAsync(
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        // PostgreSQL: each step updates up to 1000 rows after the last processed id and returns their ids
        const string updateQuery = @"
            WITH cte AS (
                SELECT id FROM users
                WHERE id > @id AND email <> lower(email)
                ORDER BY id
                LIMIT 1000)
            UPDATE users u
                SET email = lower(u.email)
            FROM cte
            WHERE cte.id = u.id
            RETURNING cte.id;";

        await DoMassUpdateAsync(
            updateQuery,
            (stepCount, totalCount) => Logger?.LogInformation("Normalized {Step} emails, {Total} in total", stepCount, totalCount),
            cancellationToken);
    }
}
```

`MassUpdateCodeMigrationBase` from `Curiosus.Migrations.Utils` marks the migration as long-running and runs each batch in
its own short transaction, so a migration over millions of rows doesn't hold long locks.

### 4. Safety in Production

`Curiosus.Migrations` implements robust safety mechanisms for production environments:

- **Migration Policies**: Configure what types of migrations can run in different environments
- **Dependency Management**: A migration runs only when the migrations it [depends on](./features/dependencies.md) are applied
- **Transactions**: Each migration runs in its own [transaction](./features/transactions.md) together with its journal record,
  so a failed migration leaves neither changes nor a record; turn it off per migration for statements like
  `CREATE INDEX CONCURRENTLY`
- **Downgrades**: [Downgrade migrations](./features/downgrade.md) roll the database back to a target version, under their
  own policy
- **Long-running Migration Control**: Separate potentially dangerous long-running operations

```csharp
// Configure strict policies for production environments
var builder = new MigrationEngineBuilder(services)
    // Only allow safe, quick schema changes in production during startup
    .UseUpgradeMigrationPolicy(MigrationPolicy.ShortRunningAllowed)
    // Prevent accidental downgrades in production
    .UseDowngradeMigrationPolicy(MigrationPolicy.AllForbidden);
```

:::warning No concurrency lock yet

The engine doesn't lock the database while it migrates: two instances starting at the same time can apply the same
migration twice or fail on the journal. Until [#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32)
lands, run migrations from one place: a Kubernetes Job or init container, a deployment pipeline step, or the startup
of a single replica.

:::

### 5. Logging and Results

The engine reports what it does through `Microsoft.Extensions.Logging` and returns a result instead of throwing:

- **Logging**: The engine logs each step; a separate [SQL logger](./features/logging.md) logs executed queries, and code
  migrations get a `Logger`
- **Result**: `MigrationResult` lists the applied migrations, the ones skipped by policy and the failed one, with an
  error code, a message and the exception
- **Progress of long migrations**: Log it from the migration itself, for example from the `DoMassUpdateAsync` callback
  shown above

```csharp
var builder = new MigrationEngineBuilder(services)
    .UseLogger(loggerFactory.CreateLogger("Migrations"))
    .UseLoggerForSql(loggerFactory.CreateLogger("Migrations.Sql"));
builder.ConfigureForPostgreSql(connectionString);
builder.UseScriptMigrations().FromDirectory("./Migrations");

var migrationEngine = builder.Build();
var result = await migrationEngine.UpgradeDatabaseAsync();
if (result.IsSuccessfully)
{
    logger.LogInformation(
        "Applied {Count} migrations, {Skipped} skipped by policy",
        result.AppliedMigrations.Count,
        result.SkippedByPolicyMigrations.Count);
}
else
{
    logger.LogError(
        result.Exception,
        "Migration {Version} failed: {ErrorCode} {ErrorMessage}",
        result.FailedMigration?.Version,
        result.ErrorCode,
        result.ErrorMessage);
}
```

### 6. Testability

`Curiosus.Migrations` simplifies database testing:

- **Isolated Test Databases**: The engine creates a missing database, so each test can migrate its own
- **Migration State Control**: Test against specific database versions
- **Integration Testing**: Verify application compatibility across schema changes
- **Mock Support**: Test migration logic with a mocked `IMigrationConnection`

```csharp
// In a test fixture: create a test database with migrations applied up to a specific version
public async Task InitializeDatabaseAsync()
{
    var builder = new MigrationEngineBuilder();
    builder.UseScriptMigrations().FromDirectory("./TestMigrations");
    builder.ConfigureForPostgreSql(TestConnectionString);
    builder.SetUpTargetVersion(new MigrationVersion(20230501)); // Apply migrations up to this version

    var migrationEngine = builder.Build();
    var result = await migrationEngine.UpgradeDatabaseAsync();

    // Now tests will run against a database at the specific version
}
```

## When to Use `Curiosus.Migrations`

`Curiosus.Migrations` is particularly well-suited for:

1. **Hand-tuned SQL with C# data migrations**: On PostgreSQL or SQL Server, in one ordered history
2. **Performance-Critical Systems**: When you need optimized SQL for large datasets
3. **Complex Database Operations**: When migrations involve sophisticated business logic or your services (DI)
4. **Multi-Environment Deployments**: When you need different behavior across dev/test/prod
5. **Long-Running Migrations**: When migrations that affect millions of records must run outside the deployment
6. **Rollbacks without a paid edition**: Downgrade scripts and code migrations to a target version

### When to Choose Something Else

- **EF Core-centric application with one schema owner**: EF Core Migrations, with bundles for deployment. Since EF Core 9
  it locks the database while migrating and checks for pending model changes.
- **Many database engines, or a fluent schema DSL**: FluentMigrator.
- **Forward-only SQL scripts with minimal ceremony**: DbUp, or grate if you want hash checks of applied scripts,
  environment scripts and a CLI.
- **DBA-led or polyglot organization, compliance and drift reports**: Flyway or Liquibase; undo, drift reports and
  targeted rollback are in their paid editions.
- **Today, several replicas migrating on startup**: any tool with a lock (EF Core, Flyway, Liquibase), or run
  `Curiosus.Migrations` from one instance until [#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32)
  lands.

## Comparison to .NET Alternatives

As of October 2026. The gaps of `Curiosus.Migrations` listed below are planned for v7, see the
[roadmap](https://github.com/curiosus-dev/Curiosus.Migrations/issues/44).

### Where `Curiosus.Migrations` Lags

| Capability | `Curiosus.Migrations` | Who has it | Planned |
|------------|----------------------|------------|---------|
| **Concurrency lock** | None: run migrations from one instance | EF Core (since 9), Evolve, Flyway, Liquibase | [#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32) |
| **Checksums, drift detection** | An edited applied script is ignored | grate, Evolve, Flyway, Liquibase | [#33](https://github.com/curiosus-dev/Curiosus.Migrations/issues/33) |
| **Repeatable migrations** (views, functions, grants) | New version per change | DbUp, grate, Evolve, Flyway, Liquibase | [#39](https://github.com/curiosus-dev/Curiosus.Migrations/issues/39) |
| **Dry-run, migration plan** | None | EF Core (scripts), grate, Liquibase, Flyway (paid) | [#40](https://github.com/curiosus-dev/Curiosus.Migrations/issues/40) |
| **CLI** | Library only: write a host app | EF Core (`dotnet ef`, bundles), FluentMigrator (`dotnet-fm`), grate, Flyway, Liquibase | [#42](https://github.com/curiosus-dev/Curiosus.Migrations/issues/42) |
| **Databases** | PostgreSQL, SQL Server | FluentMigrator 6, DbUp 7, grate 5, Flyway 20+, Liquibase 50+ | [#36](https://github.com/curiosus-dev/Curiosus.Migrations/issues/36), [#37](https://github.com/curiosus-dev/Curiosus.Migrations/issues/37), [#38](https://github.com/curiosus-dev/Curiosus.Migrations/issues/38) |

### Entity Framework Core Migrations

| Feature | EF Core Migrations (10.x) | `Curiosus.Migrations` |
|---------|---------------------------|----------------------|
| **Approach** | Generated from the EF model, editable C# with `migrationBuilder.Sql` | SQL-first with C# code migrations |
| **Data migrations** | Raw SQL inside a migration, no DI | C# code migrations with DI, batched mass updates |
| **Downgrade** | Generated `Down()`, `database update <target>`, bundles, rollback scripts | Hand-written downgrade scripts and code migrations |
| **Long-running migrations** | No distinction | Separate policy for long-running migrations |
| **Concurrency lock** | Yes, since EF Core 9 | Not yet ([#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32)) |
| **Drift** | `has-pending-model-changes`, `Migrate` fails on pending model changes | Not yet ([#33](https://github.com/curiosus-dev/Curiosus.Migrations/issues/33)) |
| **Tooling** | `dotnet ef`, self-contained bundles, idempotent scripts | Library |
| **Best For** | Applications whose schema is owned by the EF model | Hand-tuned SQL and heavy data migrations |

### FluentMigrator

| Feature | FluentMigrator (8.x) | `Curiosus.Migrations` |
|---------|----------------------|----------------------|
| **Approach** | Fluent C# API, raw SQL through `Execute.Sql` | Raw SQL scripts and C# code |
| **Database Support** | SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, Oracle, Firebird | PostgreSQL, SQL Server |
| **Downgrade** | `Down()`, auto-reversing migrations | Downgrade scripts and code migrations |
| **Environment selection** | Tags, profiles, maintenance migrations | Upgrade/downgrade policies for short- and long-running migrations |
| **Dependencies between migrations** | No | Yes |
| **DI** | Runner built on `Microsoft.Extensions.DependencyInjection` | Code migrations built through DI |
| **Concurrency lock** | None built in | Not yet ([#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32)) |
| **Best For** | Cross-database projects, schema DSL | Database-specific SQL with C# data migrations |

### DbUp

| Feature | DbUp (6.x) | `Curiosus.Migrations` |
|---------|------------|----------------------|
| **Approach** | SQL script runner, `IScript` code scripts | SQL + C# code migrations |
| **Database Support** | SQL Server, PostgreSQL, MySQL, SQLite, Oracle, Firebird, Redshift | PostgreSQL, SQL Server |
| **Rollback Support** | None (forward-only) | Downgrade to a target version |
| **Transactions** | None, per script or one for all | Per migration, can be turned off |
| **Repeatable scripts** | Run-always scripts | Not yet ([#39](https://github.com/curiosus-dev/Curiosus.Migrations/issues/39)) |
| **Selection** | Script filters | Policies, dependencies, target version |
| **Best For** | Simple forward-only deployments | Rollbacks, long-running data migrations |

### grate

| Feature | grate (2.x) | `Curiosus.Migrations` |
|---------|-------------|----------------------|
| **Approach** | SQL scripts in one-time, any-time and every-time folders | SQL + C# code migrations |
| **Database Support** | SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, Oracle | PostgreSQL, SQL Server |
| **Changed applied scripts** | Hash checks: fails when a one-time script changes | Not yet ([#33](https://github.com/curiosus-dev/Curiosus.Migrations/issues/33)) |
| **Rollback Support** | No down scripts | Downgrade to a target version |
| **Concurrency lock** | Not documented | Not yet ([#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32)) |
| **Tooling** | CLI, Docker image, library with `AddGrate` | Library |
| **Best For** | SQL-only teams that want a CLI | C# data migrations, rollbacks |

### Flyway and Liquibase

JVM tools that cover the most databases (Flyway 20+, Liquibase 50+), with locking, checksums and CLI/Docker
distribution. Undo in Flyway and targeted rollback and drift reports in Liquibase are in paid editions; Liquibase
Community is licensed under FSL since 5.0. Pick them for DBA-led or polyglot organizations. `Curiosus.Migrations`
stays inside your .NET application: C# migrations with DI and free downgrades.

### Evolve and RoundhousE

Evolve, a .NET tool inspired by Flyway, had checksums and locking, but has had no stable release since 3.2.0
(June 2023). RoundhousE is superseded by grate. Neither is a good choice for a new project.

## Summary

`Curiosus.Migrations` balances the precision of direct SQL with the power of C# code migrations, with policies that keep heavy data migrations out of the deployment path and free downgrades. It's designed for teams that need fine-grained control over their database changes across different environments; concurrency locking, drift detection and tooling are the next steps on the [v7 roadmap](https://github.com/curiosus-dev/Curiosus.Migrations/issues/44).