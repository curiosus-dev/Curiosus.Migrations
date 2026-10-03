---
sidebar_position: 8
sidebar_label: Journal
---

# Journal

Journal is database table that records the history of all applied migrations. This table is essential for managing database schema changes, ensuring that migrations are applied consistently and accurately across different environments.

#### Key Features

- **Version Tracking**: Each migration entry in the journal includes a version number. The engine applies every available migration whose version is not in the journal, in version order.

- **Unique Entries**: The table enforces uniqueness for each migration version, preventing duplicate entries and maintaining the integrity of the migration history. This ensures that each migration is applied only once.

- **Timestamp**: The table records the time (UTC) when each migration was applied, providing a chronological history of changes. This is useful for auditing and understanding the evolution of the database schema over time.

- **Rollback Support**: A downgrade removes the entry of each reverted migration, so the journal always lists the migrations currently applied to the database.

The journal has the columns `id`, `created` (UTC time of applying), `name` (the migration comment, empty when the migration has none) and `version`.
When a migration runs in a transaction, its journal entry is written in the same transaction.

The journal doesn't store checksums of the applied migrations yet, so editing an already applied script is not
detected: see [#33](https://github.com/curiosus-dev/Curiosus.Migrations/issues/33) on the
[v7 roadmap](https://github.com/curiosus-dev/Curiosus.Migrations/issues/44).

#### Configuration in `Curiosus.Migrations`

In `Curiosus.Migrations`, the journal is automatically managed by the migration engine. It is created if it does not exist and updated with each applied migration. The table name is `migration_history` by default and can be changed in the connection options:

- **PostgreSQL**: the `migrationTableHistoryName` argument of `ConfigureForPostgreSql` or `PostgresMigrationConnectionOptions`. Use a lower-case name: names are not quoted in all queries yet ([#34](https://github.com/curiosus-dev/Curiosus.Migrations/issues/34)).
- **SQL Server**: the `migrationHistoryTableName` and `schemaName` arguments of `ConfigureForSqlServer` or `SqlServerMigrationConnectionOptions`; the schema of the connection user is used when no schema is set.

```csharp
builder.ConfigureForPostgreSql(connectionString, migrationTableHistoryName: "app_migration_history");
```
