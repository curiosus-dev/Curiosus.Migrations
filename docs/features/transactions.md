---
sidebar_position: 5
sidebar_label: Transactions
---

# Transactions

Transactions are a fundamental aspect of database migrations, ensuring that changes are applied consistently and reliably. This atomicity is crucial for maintaining data integrity, especially in complex migrations involving multiple steps or operations.

By default, transactions are enabled for both script and code migrations. This means that each migration is executed within its own transaction, providing the benefits of atomicity, consistency, and error handling. The journal entry of the migration is written in the same transaction, so a migration is either applied and recorded, or neither. However, there are scenarios where you might want to disable transactions, such as when performing operations that cannot be executed within a transaction, like creating indexes concurrently.

A migration without a transaction that fails half-way leaves its changes in the database without a journal entry, and the whole migration runs again on the next start. Keep such migrations idempotent (`CREATE INDEX CONCURRENTLY IF NOT EXISTS`, `IF NOT EXISTS` checks).

The following sections provide detailed information on how to manage transactions in both script and code migrations.

## Transaction in a Script Migration

In script migrations, transactions are managed using directives within the SQL script. The `ScriptMigrationsProvider` class processes these scripts and extracts options from the directives, allowing for control over transaction management directly within the SQL script.

To manage transactions in script migrations:

- **Enable Transactions**: Use the directive `-- CURIOSUS: TRANSACTION = ON` within your SQL script to enable transactions (the default).
- **Disable Transactions**: Use the directive `-- CURIOSUS: TRANSACTION = OFF` to disable transactions.

Example of managing transactions in a script migration:

```sql
-- CURIOSUS: TRANSACTION = OFF
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_users_email ON users (email);
```

In this example, the transaction is disabled for the SQL script by using the `TRANSACTION = OFF` directive.

The directive is a line of its own, anywhere in the script; spaces are optional (`--CURIOSUS:TRANSACTION=OFF`) and
names and values are case-insensitive. The transaction is set per direction: a `.down.sql` script inherits the setting
of its `.up.sql` script unless it declares its own `TRANSACTION` directive, and a code migration can override
`IDowngradeMigration.IsDowngradeTransactionRequired` (by default the same as `IsTransactionRequired`).

All [batches](./script_migration/batches.md) of a script run in the transaction of the migration.

## Transaction in a Code Migration

In code migrations, transactions are managed programmatically using the `IsTransactionRequired` property of `CodeMigration` class. 

To manage transactions in code migrations:

- **Enable Transactions**: By default, transactions are enabled. This means that the migration engine will create a separate transaction for each migration. The transaction is passed as an argument to the `UpgradeAsync` method.
- **Disable Transactions**: Transactions can be disabled by setting the `IsTransactionRequired` property to `false`. In this case, the transaction argument in the `UpgradeAsync` method will be `null`. This is useful when you need to manually manage transactions or when performing operations that cannot be executed within a transaction, such as creating indexes concurrently.

Commands executed through `MigrationConnection` (`ExecuteNonQuerySqlAsync`, `ExecuteScalarSqlAsync`) run in the
current transaction of the connection. When you use `MigrationConnection.Connection` directly (ADO.NET commands,
Dapper, Entity Framework), pass the transaction yourself: SQL Server requires every command to be attached to the
pending transaction of the connection.

You can also create your own transactions within the migration, for example, for each mini-step of the migration process.

Example of disabling a transaction in a code migration:

```csharp
public class MyCodeMigration : CodeMigration
{
    public override MigrationVersion Version => new MigrationVersion(1, 0);
    public override string? Comment => "Example migration";

    public MyCodeMigration()
    {
        IsTransactionRequired = false; // Disable transaction
    }

    public override async Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        // Example of creating a custom transaction for a mini-step
        using (var customTransaction = MigrationConnection.BeginTransaction())
        {
            // Commands executed through MigrationConnection use this transaction
            await MigrationConnection.ExecuteNonQuerySqlAsync(
                "UPDATE users SET status = 'active' WHERE status IS NULL",
                null,
                cancellationToken);

            customTransaction.Commit();
        }
    }
}
```

In this example, the `IsTransactionRequired` property is set to `false`, disabling the transaction for this specific migration. However, a custom transaction is created within the `UpgradeAsync` method for a specific mini-step.

## Running Migrations Concurrently

The engine doesn't lock the database for the run yet: two processes starting at the same time (for example, several
replicas of an application that migrates on startup) apply the same migrations concurrently. A migration in a
transaction then fails in one of them on the unique version of the journal, and a migration without a transaction
runs twice. Until the run lock ([#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32)) is done, run
migrations from one instance only: a single replica, a deployment job or an init container.
