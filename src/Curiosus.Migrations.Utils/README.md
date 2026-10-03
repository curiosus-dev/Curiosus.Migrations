# Curiosus.Migrations.Utils

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Migrations.Utils)](https://www.nuget.org/packages/Curiosus.Migrations.Utils) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Migrations.Utils)](https://www.nuget.org/packages/Curiosus.Migrations.Utils) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/badges/Curiosus.Migrations.Utils.json)](https://github.com/curiosus-dev/Curiosus.Migrations/actions/workflows/release-packages.yml)

Helpers for [Curiosus.Migrations](https://www.nuget.org/packages/Curiosus.Migrations) code migrations.
`MassUpdateCodeMigrationBase` updates large tables in small batches, each in its own short transaction, with a pause between batches. This lets data migrations run on a live production database without long locks.

## Installation

```bash
dotnet add package Curiosus.Migrations.Utils
```

## Usage

Derive from `MassUpdateCodeMigrationBase`. The base class marks the migration as long-running and turns off the engine transaction. Call `DoMassUpdateAsync` with a query that selects a limited batch where `id > @id` and returns the updated ids. The loop continues until a batch comes back empty.

```csharp
using System.Data.Common;
using Curiosus.Migrations;
using Curiosus.Migrations.Utils;
using Microsoft.Extensions.Logging;

public class FillNewResultCodeMigration : MassUpdateCodeMigrationBase
{
    public FillNewResultCodeMigration() : base(stepDelay: TimeSpan.FromMilliseconds(100)) { }

    public override MigrationVersion Version => new(3, 1);
    public override string? Comment => "Fill calls.new_result_code in batches";

    public override async Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            WITH cte AS (
                SELECT id FROM calls
                WHERE id > @id
                ORDER BY id
                LIMIT 10000)
            UPDATE calls c
                SET new_result_code = result_code + 1
            FROM cte
            WHERE cte.id = c.id
            RETURNING cte.id;";

        var total = await DoMassUpdateAsync(
            sql,
            (step, processed) => Logger?.LogInformation("Updated {Step} rows, {Total} total", step, processed),
            cancellationToken);
    }
}
```

The example is for PostgreSQL. On other databases, write an equivalent batched `UPDATE` that returns the ids it changed. Because the migration is long-running, it runs only when the policy allows it, for example `MigrationPolicy.LongRunningAllowed` or `MigrationPolicy.AllAllowed`.

## See also

- [Curiosus.Migrations](https://www.nuget.org/packages/Curiosus.Migrations): core engine
- [Curiosus.Migrations.PostgreSQL](https://www.nuget.org/packages/Curiosus.Migrations.PostgreSQL) and [Curiosus.Migrations.SqlServer](https://www.nuget.org/packages/Curiosus.Migrations.SqlServer): database providers
- [Code migrations](https://curiosus-dev.github.io/Curiosus.Migrations/features/code_migration)
- [Short-running vs long-running migrations](https://curiosus-dev.github.io/Curiosus.Migrations/basics#migration-types-short-running-vs-long-running)
- [Curiosus.Migrations](https://github.com/curiosus-dev/Curiosus.Migrations): repository and all packages
