# Curiosus.Migrations.SqlServer

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Migrations.SqlServer)](https://www.nuget.org/packages/Curiosus.Migrations.SqlServer) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Migrations.SqlServer)](https://www.nuget.org/packages/Curiosus.Migrations.SqlServer) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/Curiosus.Migrations.SqlServer.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml)

Microsoft SQL Server provider for [Curiosus.Migrations](https://www.nuget.org/packages/Curiosus.Migrations), built on Microsoft.Data.SqlClient.
Use it to run script and code migrations against SQL Server. The provider creates the database and the migration history table when they are missing.

## Installation

```bash
dotnet add package Curiosus.Migrations.SqlServer
```

## Usage

```csharp
using Curiosus.Migrations;
using Curiosus.Migrations.SqlServer;

var builder = new MigrationEngineBuilder(services);
builder.UseScriptMigrations().FromDirectory("./Migrations"); // returns the provider, not the builder
builder.UseCodeMigrations().FromAssembly(typeof(Program).Assembly);
builder.ConfigureForSqlServer(
    "Server=localhost;Database=myapp;User Id=sa;Password=secret;TrustServerCertificate=True",
    schemaName: "dbo",
    readCommittedSnapshot: true);
builder.UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed);
var engine = builder.Build();

var result = await engine.UpgradeDatabaseAsync(cancellationToken);
```

Every parameter except `connectionString` is optional:

| Parameter | Purpose |
|-----------|---------|
| `migrationHistoryTableName`, `schemaName` | Migration history table (default `migration_history`, in the user's default schema) |
| `defaultDatabase` | Database used to create the target database (default `master`) |
| `allowSnapshotIsolation`, `readCommittedSnapshot` | Isolation settings of a newly created database |
| `collation`, `dataFilePath`, `logFilePath` | Collation and file locations for `CREATE DATABASE` |
| `initialSize`, `maxSize`, `fileGrowth` | Data file sizing, in MB |

## See also

- [Curiosus.Migrations](https://www.nuget.org/packages/Curiosus.Migrations): core engine
- [Curiosus.Migrations.Utils](https://www.nuget.org/packages/Curiosus.Migrations.Utils): batched mass updates
- [Supported databases: SQL Server](https://curiosus-dev.github.io/Curiosus.Migrations/supported_databases#sql-server)
- [Script migrations and batches](https://curiosus-dev.github.io/Curiosus.Migrations/features/script_migration/batches)
- [Curiosus.Migrations](https://github.com/curiosus-dev/Curiosus.Migrations): repository and all packages
