# Curiosus.Migrations.PostgreSQL

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Migrations.PostgreSQL)](https://www.nuget.org/packages/Curiosus.Migrations.PostgreSQL) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Migrations.PostgreSQL)](https://www.nuget.org/packages/Curiosus.Migrations.PostgreSQL) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/Curiosus.Migrations.PostgreSQL.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml)

PostgreSQL provider for [Curiosus.Migrations](https://www.nuget.org/packages/Curiosus.Migrations), built on Npgsql.
Use it to run script and code migrations against PostgreSQL. The provider creates the database and the migration history table when they are missing.

## Installation

```bash
dotnet add package Curiosus.Migrations.PostgreSQL
```

## Usage

```csharp
using Curiosus.Migrations;
using Curiosus.Migrations.PostgreSQL;

var engine = new MigrationEngineBuilder(services)
    .UseScriptMigrations().FromDirectory("./Migrations")
    .UseCodeMigrations().FromAssembly(typeof(Program).Assembly)
    .ConfigureForPostgreSql(
        "Host=localhost;Database=myapp;Username=postgres;Password=secret",
        migrationTableHistoryName: "migration_history",
        databaseEncoding: "UTF8",
        template: "template0")
    .UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed)
    .Build();

var result = await engine.UpgradeDatabaseAsync(cancellationToken);
```

Every parameter except `connectionString` is optional. If you leave one out, the PostgreSQL default is used.

| Parameter | Purpose |
|-----------|---------|
| `migrationTableHistoryName` | Migration history table (default `migration_history`) |
| `databaseEncoding`, `lcCollate`, `lcCtype` | Encoding and locale of a newly created database |
| `connectionLimit`, `template`, `tableSpace` | Other `CREATE DATABASE` settings |

The provider also defines the `%USER%` and `%DBNAME%` [variables](https://curiosity-migrations.readthedocs.io/en/latest/features/variables/), which are substituted into script migrations.

## See also

- [Curiosus.Migrations](https://www.nuget.org/packages/Curiosus.Migrations): core engine
- [Curiosus.Migrations.Utils](https://www.nuget.org/packages/Curiosus.Migrations.Utils): batched mass updates (the CTE-based example targets PostgreSQL)
- [Supported databases: PostgreSQL](https://curiosity-migrations.readthedocs.io/en/latest/supported_databases/#postgresql)
- [Quick Start](https://curiosity-migrations.readthedocs.io/en/latest/quickstart/)
- [Curiosus.Migrations](https://github.com/curiosus-dev/Curiosus.Migrations): repository and all packages
