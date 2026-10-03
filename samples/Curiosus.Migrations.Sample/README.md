# Curiosus.Migrations sample

A console application that migrates a PostgreSQL database with script and code migrations:

| Version | Migration | Shows |
|---|---|---|
| 1.0 | `Migrations/1.0-create_users.sql` | a script migration without a downgrade |
| 1.1 | `Migrations/1.1.up-add_email.sql`, `1.1.down.sql` | a script migration with a downgrade script |
| 1.2 | `CodeMigrations/SeedAdminMigration.cs` | a code migration with a downgrade, a dependency and a variable |
| 2.0 | `Migrations/2.0.up-index_users_email.sql`, `2.0.down.sql` | a long-running script outside a transaction (`CREATE INDEX CONCURRENTLY`) |
| 2.1 | `CodeMigrations/NormalizeEmailsMigration.cs` | a long-running batched data migration (`MassUpdateCodeMigrationBase`) |

## Run

Start PostgreSQL, for example in Docker:

```bash
docker run -d --rm --name curiosus-sample-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16
```

Then from this directory:

```bash
dotnet run -f net10.0                     # short-running migrations only, as an application does on startup
dotnet run -f net10.0 -- --long-running   # long-running migrations too, as a separate job does
dotnet run -f net10.0 -- --downgrade 1.0  # revert the database to version 1.0
```

The database `curiosus_sample` is created on the first run. Set `CURIOSUS_SAMPLE_CONNECTION` to use another
connection string.

The first run applies 1.0, 1.1 and 1.2 and skips 2.0 and 2.1 by policy: the upgrade policy of the application allows
short-running migrations only, so a big backfill or index never delays its startup. The `--long-running` run applies
them.

Curiosus.Migrations has no lock against concurrent runs yet ([#32](https://github.com/curiosus-dev/Curiosus.Migrations/issues/32)):
run migrations from one instance, for example a Kubernetes Job or a single replica.
