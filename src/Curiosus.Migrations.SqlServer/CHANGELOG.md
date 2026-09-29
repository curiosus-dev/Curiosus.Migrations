# Changelog: Curiosus.Migrations.SqlServer

## [6.0.0] - 2026-09-29

### Changed

- **Breaking:** dropped `net8.0` and the `netstandard2.0`, `netstandard2.1`, `netcoreapp3.1`, `net6.0` and `net7.0` targets. Supported targets are `net9.0` and `net10.0`: stay on 5.x for older runtimes.
- **Breaking:** `Microsoft.Data.SqlClient` 7.0.0 (was 6.1.4): a new major version, see its [release notes](https://github.com/dotnet/SqlClient/tree/main/release-notes/7.0) if your application references SqlClient directly.

## [5.0.0] - 2026-09-28

### Changed

- **Breaking:** package renamed from `Curiosity.Migrations.SqlServer` to `Curiosus.Migrations.SqlServer` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.

## [4.1.0] - 2026-02-13

### Changed

- Upgraded `Microsoft.Data.SqlClient` to version `6.1.4`.

## [4.0.0-beta2] - 2023-05-01

### Changed

- Improved cancellation token support.
- Simplified fetching applied migrations

## [4.0.0-beta1] - 2023-05-01

- Initial release