---
sidebar_position: 3
sidebar_label: Migration Providers
---

# Migration Providers

Migration providers are responsible for supplying migrations from various sources to the migration engine. They implement the `IMigrationsProvider` interface, which defines a method to return a collection of migrations.

The purpose of a migration provider is to abstract the source of migrations, allowing the migration engine to apply them without needing to know their origin. This enables flexibility in how migrations are defined and retrieved, whether from code, scripts, or other sources.

Providers return their migrations when the engine is built (`MigrationEngineBuilder.Build()`): scripts are read and code
migrations are created at that moment. Versions must be unique across all providers of the engine.

## Existed providers

### CodeMigrationsProvider

The `CodeMigrationsProvider` is designed to handle migrations written in C#. Migrations are sourced from specified assemblies, allowing for a structured and organized approach to migration management. Every non-abstract `CodeMigration` class found is created through dependency injection (see [Dependency Injection](./code_migration/di.md)).

#### Methods

- **FromAssembly**
  
    Use this method to set up an assembly for scanning migrations. This is useful when you want to include all migrations from a specific assembly.

    ```csharp
    var builder = new MigrationEngineBuilder(services);
    builder.UseCodeMigrations().FromAssembly(assembly);
    ```

- **`FromAssembly<T>`**
  
    Use this method to set up an assembly for scanning migrations with a specified type. Only migrations implementing the interface `T` or inheriting the class `T` are included. This is beneficial when one assembly holds migrations of several databases, or both pre-migrations and migrations.

    ```csharp
    var builder = new MigrationEngineBuilder(services);
    builder.UseCodeMigrations().FromAssembly<IMyMigration>(assembly);
    ```

### ScriptMigrationsProvider

The `ScriptMigrationsProvider` is designed to handle migrations using raw SQL scripts. This provider is ideal for straightforward SQL-based migrations and can scan directories or assemblies for script files, allowing for a flexible approach to migration management.

#### Methods

- **FromDirectory**
  
    Use this method to set up a directory for scanning migrations. This is useful when you want to include all script migrations from a specific directory. Subdirectories are not scanned. A relative path is resolved against the current directory of the process.
  
    ```csharp
    var builder = new MigrationEngineBuilder(services);
    builder.UseScriptMigrations().FromDirectory("/path/to/scripts");
    ```

- **FromAssembly**
  
    Use this method to set up an assembly where script migrations are embedded. This is beneficial when you want to include script migrations from embedded resources within an assembly. The second argument is the namespace prefix of the resources: for a project with the default namespace `MyProject` and scripts in the `Migrations` folder, the resources are named `MyProject.Migrations.1.0.sql`, and the prefix is `MyProject.Migrations`.
  
    ```csharp
    var builder = new MigrationEngineBuilder(services);
    builder.UseScriptMigrations().FromAssembly(assembly, "MyProject.Migrations");
    ```

    ```xml
    <ItemGroup>
      <EmbeddedResource Include="Migrations\*.sql" />
    </ItemGroup>
    ```

Both methods take an optional `ScriptIncorrectNamingAction` that defines what happens with `.sql` files whose names
don't match the [naming pattern](./script_migration/index.md#file-naming): `LogToWarn` by default.

## How to add custom provider

To add a custom migrations provider, use the `UseCustomMigrationsProvider` method of the `MigrationEngineBuilder` class. This method allows you to specify your own implementation of the `IMigrationsProvider` interface.

```csharp
public MigrationEngineBuilder UseCustomMigrationsProvider(IMigrationsProvider provider)
```

- **provider**: An instance of a class that implements the `IMigrationsProvider` interface.

This method adds the custom provider to the list of migrations providers used by the migration engine.

```csharp
public class MyMigrationsProvider : IMigrationsProvider
{
    public ICollection<IMigration> GetMigrations(
        IMigrationConnection migrationConnection,
        IReadOnlyDictionary<string, string> variables,
        ILogger? migrationLogger)
    {
        // Load migrations from your source, for example a table of another service
        return new List<IMigration>();
    }
}

builder.UseCustomMigrationsProvider(new MyMigrationsProvider());
```

`GetMigrations` runs in `Build()`, so exceptions it throws come out of `Build()`. To fail a run with a specific error
code from a custom connection or migration, throw a `MigrationException` with the `MigrationErrorCode`: the engine
returns it in `MigrationResult.ErrorCode`.
