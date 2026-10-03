---
sidebar_position: 2
sidebar_label: EntityFramework Integration
---

# Entity Framework Core Integration

## Overview

`Curiosus.Migrations` supports using Entity Framework Core inside your code migrations. This allows you to perform complex data operations using an ORM rather than raw SQL.

## Using Entity Framework in Migrations

### Attaching to Connection

When creating an Entity Framework `DbContext` within a code migration, you can attach it to the same database connection that the migration is using (the example is for PostgreSQL, use `UseSqlServer` for SQL Server):

```csharp
public override async Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
{
    // Create DbContext options using the migration's connection
    var contextOptionsBuilder = new DbContextOptionsBuilder<MyDbContext>();
    contextOptionsBuilder.UseNpgsql(MigrationConnection.Connection!);
    
    // Create your context with the configured options
    await using (var dbContext = new MyDbContext(contextOptionsBuilder.Options))
    {
        // Attach the migration's transaction to the context
        await dbContext.Database.UseTransactionAsync(transaction, cancellationToken);
        
        // Perform EF operations
        // ...
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

Attaching the context to the migration transaction is required on SQL Server, where every command must be attached
to the pending transaction of the connection; on PostgreSQL it makes the changes of the context part of the
migration transaction.

### Transaction Control

By default, code migrations run within a transaction. You can disable this behavior by setting `IsTransactionRequired = false` in your migration constructor:

```csharp
public MyCodeMigration()
{
    IsTransactionRequired = false; // Disable automatic transaction
}
```

When transactions are disabled, you can manually create transactions for specific operations:

```csharp
public override async Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
{
    // Create a custom transaction for a specific operation
    using (var customTransaction = MigrationConnection.BeginTransaction())
    {
        // Use the transaction with Entity Framework
        var contextOptionsBuilder = new DbContextOptionsBuilder<MyDbContext>();
        contextOptionsBuilder.UseNpgsql(MigrationConnection.Connection!);
        
        await using (var dbContext = new MyDbContext(contextOptionsBuilder.Options))
        {
            await dbContext.Database.UseTransactionAsync(customTransaction, cancellationToken);
            
            // Perform operations
            // ...
            
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        
        customTransaction.Commit();
    }
}
```