---
sidebar_position: 6
sidebar_label: Pre-migrations
---

# Pre-migrations

Pre-migrations are a set of migrations that are executed before the main migrations. They are useful for preparing the database environment or performing tasks that must be completed before the main migration logic is applied. Pre-migrations are not stored in the migration journal, so they run on every run of the engine that has migrations to apply: write them to be idempotent (`CREATE EXTENSION IF NOT EXISTS`, `INSERT ... ON CONFLICT DO NOTHING`).

How pre-migrations run:

- Only when the run has migrations to apply: when the database is up to date, they are skipped.
- Before the main migrations of both upgrades and downgrades, in version order, each in its own transaction unless it disables it (`-- CURIOSUS: TRANSACTION = OFF`, `IsTransactionRequired = false`).
- Before the migration policy is checked, so they run even when the policy skips or forbids the main migrations.
- The journal is read again after them, so a pre-migration may change the list of applied migrations.
- A failed pre-migration fails the run with `MigrationErrorCode.MigratingError`.

Pre-migrations are particularly useful in scenarios where certain setup or configuration tasks need to be completed before the main migration logic is applied. Here are some examples:

- **Setting Up PostgreSQL Extensions**: Use pre-migrations to install or configure PostgreSQL extensions, such as enabling the `uuid-ossp` extension for UUID generation.
- **Seeding Initial Data**: Insert initial data into the database, which is required for the main migrations to function correctly.
- **Configuring Database Settings**: Adjust database settings or parameters that need to be in place before the main migrations are applied.

## Script Pre-migrations

Script pre-migrations allow you to execute raw SQL scripts before the main migration. They follow the same
[file naming](./script_migration/index.md#file-naming) and directives as script migrations.

```csharp
var builder = new MigrationEngineBuilder();
builder.UseScriptPreMigrations().FromDirectory("/path/to/pre-migrations");
```

To use script pre-migrations, you can configure the `MigrationEngineBuilder` to include SQL scripts that should be executed prior to the main migration logic.

## Code Pre-migrations

Code pre-migrations enable you to execute C# code before the main migration. This is particularly useful for complex logic that cannot be easily expressed in SQL. They are `CodeMigration` classes found in an assembly, like code migrations.

```csharp
var builder = new MigrationEngineBuilder();
builder.UseCodePreMigrations().FromAssembly<IPreMigration>(Assembly.GetExecutingAssembly());
builder.UseCodeMigrations().FromAssembly<IMainMigration>(Assembly.GetExecutingAssembly());

public interface IPreMigration;

public interface IMainMigration;

public class EnableExtensionsPreMigration : CodeMigration, IPreMigration
{
    public override MigrationVersion Version => new(1);

    public override string? Comment => "Enable extensions";

    public override Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return MigrationConnection.ExecuteNonQuerySqlAsync(
            "CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\"",
            null,
            cancellationToken);
    }
}
```

`FromAssembly(assembly)` picks up every `CodeMigration` class of the assembly, so when pre-migrations and migrations
live in the same assembly, separate them with a marker interface or base class and `FromAssembly<T>`, as above.
Otherwise the pre-migrations are also registered as main migrations.
