---
sidebar_position: 4
sidebar_label: Basics
---

# Basics

This article explains the fundamental concepts of `Curiosus.Migrations` that you need to understand before diving into specific features.

## How the Migration Engine Works

The `Curiosus.Migrations` library follows a structured process to manage database schema and data changes safely and consistently:

```
┌───────────────────┐     ┌───────────────────┐     ┌───────────────────┐
│  1. Configuration │     │  2. Infrastructure │     │  3. Version       │
│     Setup         │────▶│     Preparation    │────▶│     Comparison    │
└───────────────────┘     └───────────────────┘     └───────────────────┘
                                                              │
                                                              ▼
┌───────────────────┐     ┌───────────────────┐     ┌───────────────────┐
│  6. Result        │     │  5. Migration     │     │  4. Migration     │
│     Handling      │◀────│     Execution     │◀────│     Planning      │
└───────────────────┘     └───────────────────┘     └───────────────────┘
```

### 1. Configuration

The migration process begins with configuring the `MigrationEngine` using the `MigrationEngineBuilder`. This involves:

- Setting up migration providers (script migrations, code migrations)
- Configuring database connection details
- Defining migration policies for upgrades and downgrades
- Setting the target version (if needed)
- Configuring logging and error handling

```csharp
var builder = new MigrationEngineBuilder(services);
builder.UseScriptMigrations().FromDirectory("./Migrations");
builder.ConfigureForPostgreSql("Host=localhost;Database=mydb;Username=postgres;Password=password");
builder.UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed);
```

`UseScriptMigrations()` and `UseCodeMigrations()` return the migrations provider to configure, not the builder, so the
builder is configured with separate statements.

### 2. Infrastructure Preparation

The `MigrationEngine` ensures the necessary infrastructure exists:

- **Database**: If the database doesn't exist, it's created according to connection settings. The check connects to
  the maintenance database (`postgres` for PostgreSQL, `master` for SQL Server) on every run, so the user needs
  access to it
- **Migration Journal**: A table is created or verified to track applied migrations

### 3. Version Comparison

The migrator determines what needs to be done by comparing:

- Currently applied migrations (from the journal table)
- Available migrations (from providers)
- Target version (specified in configuration)

Every available migration that is not in the journal is applied, including versions lower than the latest applied
one: a migration merged from another branch later is applied too. Journal records without a matching available
migration are ignored.

### 4. Migration Planning

Based on the comparison:

- For **upgrades**: Plans to apply new migrations in ascending version order
- For **downgrades**: Plans to apply downgrade migrations in descending version order
- Migrations not allowed by the configured policy (e.g., only short-running) are skipped during execution and
  returned in `SkippedByPolicyMigrations`

### 5. Migration Execution

Migrations are executed according to the plan:

- Pre-migrations are executed first (if configured and there are migrations to apply)
- Each migration runs in its own transaction unless it disables it, and its journal record is written in the same
  transaction
- Results and progress are logged
- Journal table is updated as migrations are applied

### 6. Result Handling

The process concludes by:

- Returning a `MigrationResult`: `IsSuccessfully`, `AppliedMigrations`, `SkippedByPolicyMigrations`, and on failure
  `ErrorCode`, `ErrorMessage`, `FailedMigration` and the `Exception` that caused it
- Logging completion status

Errors are returned in the result, not thrown. Cancelling the token passed to `UpgradeDatabaseAsync` or
`DowngradeDatabaseAsync` throws `OperationCanceledException`.

## Versioning System

`Curiosus.Migrations` provides a flexible versioning system to organize and sequence your database changes.

### Version Structure

A migration version consists of:

- **Major**: Required primary version number (e.g., `1`, `20230101`)
- **Minor**: Optional secondary version number after a dot (e.g., `.1`, `.42`)

The complete pattern recognized is: `([\d_]+)(\.(\d+))*`. Underscores in the major part are ignored, so
`20230101_1430` is `202301011430`; the major is a `long` and the minor is a `short`, both compared as numbers.

### Version Examples

Valid version formats include:

| Version Format | Example | Comments |
|----------------|---------|----------|
| Simple number | `1` | Basic sequential numbering |
| Decimal number | `1.5` | Major.Minor versioning |
| Date-based | `20230101` | Using date as version (YYYYMMDD) |
| Timestamp | `20230101_1430` | Date with time (YYYYMMDD_HHMM) |
| Complex | `20230101_1430.5` | Timestamp with minor version |

### Best Practices for Versioning

1. **Consistency**: Choose one versioning scheme and stick with it
2. **Simplicity**: For small projects, simple numbers (`1`, `2`, `3`) are often sufficient
3. **Timestamp-based**: For larger teams, date-based versions (`YYYYMMDD`) help avoid conflicts, especialy on mergre requests
4. **Minor versions**: Use minor versions to group related migrations that must run in sequence

```csharp
// Examples of creating version objects
var simpleVersion = new MigrationVersion(1);
var decimalVersion = new MigrationVersion(1, 5);
var dateVersion = new MigrationVersion(20230101);
var parsedVersion = new MigrationVersion("20230101_1430.5");
```

## Target Version Management

The target version controls which migrations should be applied or rolled back.

### Setting a Target Version

```csharp
// Set a specific target version (migrate up to version 3)
builder.SetUpTargetVersion(new MigrationVersion(3));

// Apply only migration 3, not the ones before it
builder.SetUpTargetVersion(new MigrationVersion(3), onlyTargetVersion: true);

// Migrate to the latest available version (default behavior)
// No need to call SetUpTargetVersion
```

The target version must be one of the available migrations, `Build()` throws otherwise.

### Migration Direction

The migration direction is not determined automatically. You need to manualy specify what you want - upgrade or downgrade.

Example:
```csharp
// Configure the migration engine
var builder = new MigrationEngineBuilder(services);
builder.UseScriptMigrations().FromDirectory("path/to/migrations");
builder.UseCodeMigrations().FromAssembly(Assembly.GetExecutingAssembly());
builder.ConfigureForPostgreSql("YourConnectionString");
builder.UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed);
builder.UseDowngradeMigrationPolicy(MigrationPolicy.ShortRunningAllowed);

// For upgrading to the latest version
await builder.Build().UpgradeDatabaseAsync();

// For upgrading to a specific version
builder.SetUpTargetVersion(new MigrationVersion(3));
await builder.Build().UpgradeDatabaseAsync();

// For downgrading to a specific version (the target version is required)
builder.SetUpTargetVersion(new MigrationVersion(1));
await builder.Build().DowngradeDatabaseAsync();
```

## Migration Types: Short-Running vs Long-Running

`Curiosus.Migrations` categorizes migrations as either short-running or long-running to help manage resource utilization and scheduling.

### Key Differences

| Short-Running Migrations | Long-Running Migrations |
|--------------------------|-------------------------|
| Schema changes, small data updates | Large data transformations |
| Typically completes in seconds | May run for minutes or hours (even days)|
| Safe to run during application startup | Better scheduled during maintenance windows |
| Default for all migrations | Must be explicitly configured |

### Use Cases

**Short-Running Example**: Adding a new column to a table
```sql
-- CURIOSUS: LONG-RUNNING = FALSE
ALTER TABLE users ADD COLUMN email VARCHAR(255);
```

**Long-Running Example**: Populating data for millions of rows
```sql
-- CURIOSUS: LONG-RUNNING = TRUE
UPDATE users SET email = CONCAT(username, '@example.com');
```

### Configuring Migration Types

**For code migrations**:
```csharp
public class PopulateUserEmails : CodeMigration
{
    public override MigrationVersion Version => new MigrationVersion(1, 1);
    public override string? Comment => "Populate user email addresses";
    
    public PopulateUserEmails()
    {
        IsLongRunning = true; // Mark as long-running
    }
    
    public override async Task UpgradeAsync(DbTransaction? transaction = null, 
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

**For script migrations**:
Add a directive comment to your SQL file, usually at the top:
```sql
-- CURIOSUS: LONG-RUNNING = TRUE
```

Directives are written as `-- CURIOSUS: <OPTION> = <VALUE>` on their own line: spaces are optional
(`--CURIOSUS:LONG-RUNNING=TRUE` works too), and option names and values are case-insensitive. The options are
`TRANSACTION` (`ON`/`OFF`), `LONG-RUNNING` (`TRUE`/`FALSE`) and `DEPENDENCIES` (comma-separated versions);
an unknown option fails the build of the engine. For a migration with `.up.sql` and `.down.sql` scripts,
`LONG-RUNNING` and `DEPENDENCIES` come from the upgrade script, while `TRANSACTION` is set per direction: the downgrade
script inherits the upgrade setting unless it declares its own.

:::note

Scripts written for Curiosity.Migrations (before the rename to Curiosus.Migrations) use the `-- CURIOSITY:` prefix.
It is still supported, so existing scripts don't need to be changed.

:::

## Migration Policies

Migration policies control which types of migrations are allowed to run in different scenarios.

### Available Policies

- **AllForbidden**: No migrations are allowed to run. If there are migrations to apply, the result fails with
  `MigrationErrorCode.PolicyError` (pre-migrations have already run by then)
- **ShortRunningAllowed**: Only short-running migrations can run
- **LongRunningAllowed**: Only long-running migrations can run
- **AllAllowed**: All migrations can run, regardless of type

`MigrationPolicy` is a flags enum: `ShortRunningAllowed | LongRunningAllowed` allows the same migrations as `AllAllowed`.
By default upgrades are `AllAllowed` and downgrades are `AllForbidden`.

### Configuring Policies

You can set different policies for upgrade and downgrade operations:

```csharp
var builder = new MigrationEngineBuilder(services)
    // Allow all migrations during upgrades
    .UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed)
    // Only allow short-running migrations during downgrades
    .UseDowngradeMigrationPolicy(MigrationPolicy.ShortRunningAllowed);
```

Policy methods return the builder, so they can be chained.

### Policy Usage Scenarios

1. **Development Environment**:
   ```csharp
   .UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed)
   .UseDowngradeMigrationPolicy(MigrationPolicy.AllAllowed)
   ```

2. **Production Application Startup**:
   ```csharp
   .UseUpgradeMigrationPolicy(MigrationPolicy.ShortRunningAllowed)
   .UseDowngradeMigrationPolicy(MigrationPolicy.AllForbidden)
   ```

3. **Scheduled Maintenance Window**:
   ```csharp
   .UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed)
   .UseDowngradeMigrationPolicy(MigrationPolicy.ShortRunningAllowed)
   ```

## Migration Providers

Migration providers determine where migrations come from. You can configure multiple providers to source migrations from different locations.

```csharp
var builder = new MigrationEngineBuilder(services);
// Add migrations from SQL scripts
builder.UseScriptMigrations().FromDirectory("./Migrations/Scripts");
// Add migrations from embedded resources
builder.UseScriptMigrations().FromAssembly(Assembly.GetExecutingAssembly(), "MyNamespace.Migrations");
// Add migrations from code
builder.UseCodeMigrations().FromAssembly(Assembly.GetExecutingAssembly());
```

For more information on available providers and custom implementations, see the [Migration Providers](./features/migration_providers.md) article.

## Complete Configuration Example

Here's a complete example showing how to configure the migration engine with all major options:

```csharp
using Curiosus.Migrations;
using Curiosus.Migrations.PostgreSQL;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

// Create service collection and logger (AddConsole needs the Microsoft.Extensions.Logging.Console package)
var services = new ServiceCollection();
using var loggerFactory = LoggerFactory.Create(configure => configure.AddConsole());

// Configure and build the migration engine
var builder = new MigrationEngineBuilder(services);
// Add script migrations from directory, fail on files with incorrect names
builder.UseScriptMigrations()
    .FromDirectory("./DatabaseMigrations", ScriptIncorrectNamingAction.ThrowException);
// Add code migrations from assembly
builder.UseCodeMigrations()
    .FromAssembly(typeof(Program).Assembly);
// Configure pre-migrations
builder.UseScriptPreMigrations()
    .FromDirectory("./DatabasePreMigrations");
builder
    // Configure database connection and the journal table name
    .ConfigureForPostgreSql(
        "Host=localhost;Database=myapp;Username=postgres;Password=secret",
        migrationTableHistoryName: "migration_history")
    // Set migration policies
    .UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed)
    .UseDowngradeMigrationPolicy(MigrationPolicy.ShortRunningAllowed)
    // Add variables for substitution in scripts
    .UseVariable("%SCHEMA%", "public")
    .UseVariable("%TABLE_PREFIX%", "app_")
    // Configure logging
    .UseLogger(loggerFactory.CreateLogger("Migrations"))
    // Set target version (optional)
    .SetUpTargetVersion(new MigrationVersion(20230101));

// Build the engine
var migrationEngine = builder.Build();

// Execute migrations
var result = await migrationEngine.UpgradeDatabaseAsync();

// Handle results
if (result.IsSuccessfully)
{
    Console.WriteLine($"Applied {result.AppliedMigrations.Count} migrations");
    Console.WriteLine($"Skipped by policy {result.SkippedByPolicyMigrations.Count} migrations");
}
else
{
    Console.WriteLine($"Migration {result.FailedMigration?.Version} failed ({result.ErrorCode}): {result.ErrorMessage}");
}
```

## Troubleshooting Common Issues

### Version Parsing Problems

**Problem**: Migration version cannot be parsed from filename or class.

**Solution**: Ensure your version format matches the pattern `([\d_]+)(\.(\d+))*` and the file name matches the
[script naming pattern](./features/script_migration/index.md#file-naming). Files with incorrect names are skipped with a
warning by default: pass `ScriptIncorrectNamingAction.ThrowException` to `FromDirectory`/`FromAssembly` to fail instead.

Version strings are parsed leniently: the first part of the string matching the pattern is used, so check versions
that come from strings, for example in the `DEPENDENCIES` directive:

```csharp
new MigrationVersion(1, 5);          // 1.5
new MigrationVersion(20230101);      // 20230101
new MigrationVersion("1.5");         // 1.5
new MigrationVersion("1.05");        // 1.5, the minor part is a number
new MigrationVersion("v1");          // 1, the letter is skipped
new MigrationVersion("1.2.3");       // 1.3, the last minor part wins

// Incorrect (will cause errors)
// new MigrationVersion("abc");      // No digits: ArgumentException
// new MigrationVersion(-1);         // Cannot use negative numbers: ArgumentOutOfRangeException
```

### Missing Dependencies

**Problem**: Migrations fail with errors about missing dependencies.

**Solution**: Dependencies are checked right before a migration runs: every dependency must already be in the journal.
Ensure the referenced migrations exist, have lower versions (migrations run in version order, so a dependency on a
higher version always fails) and are allowed by the policy of the run.

### Transaction Errors

**Problem**: Migrations fail with transaction-related errors.

**Solution**: Some operations cannot run within a transaction (like certain DDL statements in some databases).
Set `IsTransactionRequired = false` for these migrations:

```csharp
public class CreateIndexMigration : CodeMigration
{
    public CreateIndexMigration()
    {
        IsTransactionRequired = false; // Disable transaction for this migration
    }
    
    // Implementation
}
```

### Database Connection Issues

**Problem**: Migrations fail with connection errors.

**Solution**: 
- Verify connection string parameters
- Ensure the database server is running and accessible
- Check network connectivity and firewall settings
- Verify the user has sufficient permissions

### Policy Restrictions

**Problem**: Migrations don't run due to policy restrictions.

**Solution**: Check `result.SkippedByPolicyMigrations` and your migration policy settings, and ensure they allow the
types of migrations you're trying to run:

```csharp
// Make sure your policy allows the migrations you want to run
builder.UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed);
```

For more detailed information on specific features, please refer to the corresponding feature articles.
