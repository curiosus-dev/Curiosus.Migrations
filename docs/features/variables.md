---
sidebar_position: 4
sidebar_label: Variables
---

# Variables and variable substitution

`Curiosus.Migrations` supports basic variable substitution.
 
Variables in migrations provide several important benefits:

- **Environment independence**: Create migrations that work across different environments (development, staging, production) without code changes
- **Improved maintainability**: Centralize configuration values instead of hardcoding them throughout migrations
- **Reduced errors**: Avoid typos and inconsistencies by defining values once and reusing them

## How to use
 
To use variable substitution you should register variables when configuring migrator:

```csharp
var variableValue = "my_variable";
var builder = new MigrationEngineBuilder();
builder.UseVariable("%VARIABLE%", variableValue);
```

The name is the exact text replaced in scripts, so include the delimiters (`%VARIABLE%`). Registering a variable with
the same name again overwrites it, and a variable registered with `UseVariable` takes precedence over a default
variable of the provider with the same name.

### Script migrations

In script migrations (and script pre-migrations), variables are substituted with their values when the engine is
built, before execution.

```sql
-- %VARIABLE% %NotExistedVariable%
SELECT * FROM dbo.%VARIABLE%
```

After running migrator script will be transformed:

```sql
-- my_variable %NotExistedVariable%
SELECT * FROM dbo.my_variable
```

Substitution is a plain text replacement of each variable name over the whole script, comments and string literals
included, with no escaping. Placeholders without a registered variable are left as is. Avoid names that are a prefix of
another name: with variables `%A` and `%AB%`, the text `%AB%` may be replaced by the value of `%A` followed by `B%`,
depending on the order the variables were added.

:::warning

Don't pass secrets as variables. The substituted script is regular SQL text: it is written to the SQL log (the
PostgreSQL provider logs every executed SQL at the `Information` level when a SQL logger is set, see
[Logging](./logging.md)) and ends up in database logs and monitoring.

:::

Delimited tokens, protection from prefix collisions and redaction of sensitive values are planned in
[#35](https://github.com/curiosus-dev/Curiosus.Migrations/issues/35).

### Code migrations

In code migrations, variables are accessible via the `Variables` property in your migration class. This property is a `IReadOnlyDictionary<string, string>` which is populated automatically when your migration is initialized.

```csharp
public class MyCodeMigration : CodeMigration
{
    public override MigrationVersion Version => new MigrationVersion(1, 0);
    
    public override string? Comment => "My migration with variables";
    
    public override async Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        // Access variables using the Variables dictionary
        var user = Variables[DefaultVariables.User];
        var dbName = Variables[DefaultVariables.DbName];
        var customVariable = Variables["%VARIABLE%"];
        
        // Use variables in your migration logic
        Logger?.LogInformation($"Running migration as user {user} on database {dbName}");
        
        // Custom SQL with variables
        var sql = $"CREATE TABLE {customVariable}_table (id INT)";
        await MigrationConnection.ExecuteNonQuerySqlAsync(sql, null, cancellationToken);
    }
}
```

## Default variables

All providers by default provide next variables:
 
 - `%USER%` (`DefaultVariables.User`) - name of user from connection string (`unknown` for SQL Server integrated security)
 - `%DBNAME%` (`DefaultVariables.DbName`) - database name. The PostgreSQL provider currently sets it to the
   maintenance database (`postgres`) instead of the database from the connection string: register the name with
   `UseVariable("%DBNAME%", ...)` if your scripts need it.
