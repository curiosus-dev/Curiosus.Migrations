---
sidebar_position: 3
sidebar_label: Dependency Injection
---

# Dependency Injection

The `Curiosus.Migrations` library supports Dependency Injection (DI) to facilitate the creation and management of migration classes. This is primarily achieved through the `CodeMigrationsProvider`. This class is responsible for discovering and creating code migrations. It utilizes the `IServiceCollection` to register migration types, enabling the DI container to resolve and inject dependencies into migration classes.

## Usage

To use DI with `CodeMigrationsProvider`, follow these steps:

1. **Pass Your Services**: Create the `MigrationEngineBuilder` with your `IServiceCollection` (or use `services.AddMigrations(...)`, which does it for you). Without it the builder uses an empty service collection.

    ```csharp
    var services = new ServiceCollection();
    services.AddSingleton<IDependency, Dependency>();

    var builder = new MigrationEngineBuilder(services);
    builder.UseCodeMigrations().FromAssembly(typeof(YourMigrationClass).Assembly);
    ```

2. **Inject Dependencies**: Define your migration classes to accept dependencies through their constructors. The DI container will automatically resolve and inject these dependencies when the migration is created.

    ```csharp
    public class YourMigrationClass : CodeMigration
    {
       private readonly IDependency _dependency;

       public YourMigrationClass(IDependency dependency)
       {
           _dependency = dependency;
       }

       public override MigrationVersion Version => new(1);

       public override string? Comment => "Uses a dependency";

       public override Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
       {
           // Use _dependency here
           return Task.CompletedTask;
       }
    }
    ```

3. **Build and Run**: Ensure that your DI container is properly configured and that the migration engine is built and executed as needed.

## How Migrations Are Created

When the engine is built, the provider registers the migration classes in the service collection as transient
services, builds a service provider from the collection and resolves the migrations from it. This has consequences:

- Only services registered before `Build()` (or before the `AddMigrations` call) are available to migrations.
- The service provider is separate from the one of your application: singletons resolved by migrations are other
  instances than the ones your application gets.
- Scoped services, such as an Entity Framework `DbContext`, are resolved from the root of that provider and are not
  disposed. Prefer creating a `DbContext` on the migration connection, see
  [Entity Framework Core Integration](./ef_integration.md).

Creating migrations from the application's service provider is planned in
[#29](https://github.com/curiosus-dev/Curiosus.Migrations/issues/29).
