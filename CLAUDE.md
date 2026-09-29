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
Libraries and tests target `net9.0` and `net10.0` only (no `net8.0`, unlike the other Curiosus libraries), with the
default C# version of each. EF Core 10 supports `net10.0` only, so test projects using it reference EF Core 9 for
`net9.0`. Tests use xUnit v3 (`xunit.v3.mtp-off`: VSTest mode, which the shared Cake coverage collection needs).

## Tests

- `tests/UnitTests/` — unit tests per package.
- `tests/IntegrationTests/` — run against real PostgreSQL and SQL Server via Testcontainers, Docker is required.
