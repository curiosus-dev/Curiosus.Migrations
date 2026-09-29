# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@.claude/curiosus.md

## Project overview

Curiosus.Migrations is a database migration framework for .NET: raw SQL script migrations and C# code migrations,
downgrade migrations, long-running migrations separated from schema changes, per-environment migration policies.
Documentation (Docusaurus, GitHub Pages) lives in `docs/`, site settings in `docs.json`; `docs/index.md` and
`docs/changelog/*` are symlinks to `README.md` and the package CHANGELOGs. Published at
https://curiosus-dev.github.io/Curiosus.Migrations/, `website/` is synced from dotnet-tools.

## Packages

- `Curiosus.Migrations` — core engine, migration providers, policies; the other packages depend on it.
- `Curiosus.Migrations.PostgreSQL` — PostgreSQL connection (Npgsql).
- `Curiosus.Migrations.SqlServer` — SQL Server connection.
- `Curiosus.Migrations.Utils` — helpers for code migrations (e.g. mass updates).

Each package has its own `CHANGELOG.md`, whose top `## [x.y.z]` section is the package version; packages are released independently.
Libraries multi-target down to `netstandard2.0` with `LangVersion` 11, so avoid APIs and language features
that are missing there (e.g. `ArgumentNullException.ThrowIfNull`), or guard them with `#if`.

## Tests

- `tests/UnitTests/` — unit tests per package.
- `tests/IntegrationTests/` — run against real PostgreSQL and SQL Server via Testcontainers, Docker is required.
