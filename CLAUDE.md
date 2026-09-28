# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@.claude/curiosus.md

## Project overview

Curiosus.Migrations is a database migration framework for .NET: raw SQL script migrations and C# code migrations,
downgrade migrations, long-running migrations separated from schema changes, per-environment migration policies.
Documentation (MkDocs, ReadTheDocs) lives in `docs/`, published at https://curiosity-migrations.readthedocs.io/.

## Packages

- `Curiosus.Migrations` — core engine, migration providers, policies; the other packages depend on it.
- `Curiosus.Migrations.PostgreSQL` — PostgreSQL connection (Npgsql).
- `Curiosus.Migrations.SqlServer` — SQL Server connection.
- `Curiosus.Migrations.Utils` — helpers for code migrations (e.g. mass updates).

Each package has its own `<PackageVersion>` and `CHANGELOG.md`, released independently.
Libraries multi-target down to `netstandard2.0` with `LangVersion` 11, so avoid APIs and language features
that are missing there (e.g. `ArgumentNullException.ThrowIfNull`), or guard them with `#if`.

## Tests

- `tests/UnitTests/` — unit tests per package.
- `tests/IntegrationTests/` — run against real PostgreSQL and SQL Server via Testcontainers, Docker is required.
