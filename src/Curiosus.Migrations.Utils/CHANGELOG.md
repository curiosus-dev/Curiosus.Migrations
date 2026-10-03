# Changelog: Curiosus.Migrations.Utils

## [6.0.1] - 2026-10-03

### Fixed

- `MassUpdateCodeMigrationBase.DoMassUpdateAsync` runs each batch in its own transaction on SQL Server too: the batch
  query wasn't attached to the transaction, which SqlClient requires.
- The `updateQuery` documentation of `DoMassUpdateAsync` asked for `WHERE id >= @id`, which selects the last
  processed row again on every step and never ends: the condition is `WHERE id > @id`, as in the example.

## [6.0.0] - 2026-09-29

### Changed

- **Breaking:** dropped `net8.0` and the `netstandard2.0`, `netstandard2.1`, `netcoreapp3.1`, `net6.0` and `net7.0` targets. Supported targets are `net9.0` and `net10.0`: stay on 5.x for older runtimes.

## [5.0.0] - 2026-09-28

### Changed

- **Breaking:** package renamed from `Curiosity.Migrations.Utils` to `Curiosus.Migrations.Utils` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.

## [4.1.0] - 2026-02-13

### Changed

- Upgraded dependencies.

## [4.0.2] - 2023-04-14

### Fixed

- Added xml comments to nuget package.

## [4.0.1] - 2023-04-10

### Added

- Added README.md to a package.

## [4.0.0] - 2023-04-10

- Moved `MassUpdateCodeMigrationBase` from main package.
