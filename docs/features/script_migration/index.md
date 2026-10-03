---
sidebar_position: 1
sidebar_label: What is it
---

# Script migration

Script migrations are used to apply changes to a database using raw SQL scripts. Script migrations can be organized into batches, allowing for more granular control over the execution order and transaction management.

To use script migrations, you can define them in SQL files and organize them in directories or embed them in assemblies. The `ScriptMigrationsProvider` class facilitates the discovery and execution of these scripts from specified locations.

For more advanced scenarios, you can create custom script providers by implementing the `IMigrationsProvider` interface. This allows you to tailor the migration process to your specific requirements (for more detail read [Migration Providers](../migration_providers.md) article).

## File naming

Migration scripts must follow a specific naming pattern to be recognized and processed correctly. The required pattern is defined by the following regular expression (case-insensitive):

```csharp
^(([\d_]+)(\.(\d+))*)(.(down)|.(up))?(-([\w]*))?\.sql$
```

- **Version**: The script name must start with a version number, which can include underscores and dots (see [Versioning System](../../basics.md#versioning-system)).
- **Direction**: Optionally, the script can specify a direction (`up` or `down`) to indicate whether it is an upgrade or downgrade script. A script without a direction is an upgrade script.
- **Comment**: A comment can be included at the end of the file name, prefixed by a dash. It may contain letters, digits and underscores only: use `1.0-add_users.sql`, not `1.0-add-users.sql`. The comment is saved to the journal.

Examples:

| File name | Version | Direction | Comment |
|-----------|---------|-----------|---------|
| `1.sql` | 1 | up | — |
| `1.1-add_users.sql` | 1.1 | up | `add_users` |
| `2.up.sql` | 2 | up | — |
| `2.down.sql` | 2 | down | — |
| `20230101_1430.up-add_email.sql` | 202301011430 | up | `add_email` |

If a script file does not match this pattern, the behavior can be configured using the `ScriptIncorrectNamingAction` enum, which provides the following options:

- `Ignore`: The script is ignored, and the process continues with the next scripts.
- `LogToWarn` (default): A warning is logged, and the process continues.
- `LogToError`: An error is logged, and the process continues.
- `ThrowException`: An exception is thrown, aborting the execution.

The action is passed to `FromDirectory` or `FromAssembly`. Files without the `.sql` extension are skipped.

```csharp
builder.UseScriptMigrations().FromDirectory("./Migrations", ScriptIncorrectNamingAction.ThrowException);
```

When a migration has both `.up.sql` and `.down.sql` scripts, its comment is taken from the upgrade script, or from the
downgrade script when the upgrade script has none.

## Directives

Script options are set with directive comments, each on its own line:

```sql
-- CURIOSUS: TRANSACTION = OFF
-- CURIOSUS: LONG-RUNNING = TRUE
-- CURIOSUS: DEPENDENCIES = 1.0, 2.0
```

- `TRANSACTION` (`ON`/`OFF`): run the script in a transaction, see [Transactions](../transactions.md).
- `LONG-RUNNING` (`TRUE`/`FALSE`): see [short-running vs long-running](../../basics.md#migration-types-short-running-vs-long-running).
- `DEPENDENCIES` (comma-separated versions): see [Dependencies](../dependencies.md).

Spaces around the prefix, the option name, `=` and the value are optional (`--CURIOSUS:TRANSACTION=OFF`), names and
values are case-insensitive, and a directive may be the last line of the script. An unknown option or value fails the
build of the engine. The legacy `-- CURIOSITY:` prefix is still supported.

For a migration with `.up.sql` and `.down.sql` scripts:

- `LONG-RUNNING` and `DEPENDENCIES` describe the whole migration and come from the upgrade script; a warning is logged
  when the downgrade script declares a different value.
- `TRANSACTION` is set per direction: the downgrade script inherits the upgrade setting unless it declares its own.
  So a transactional `CREATE INDEX` can be undone with `DROP INDEX CONCURRENTLY` and `-- CURIOSUS: TRANSACTION = OFF`
  in the downgrade script, and a `CREATE INDEX CONCURRENTLY` upgrade with `TRANSACTION = OFF` needs no directive in its
  downgrade script.
