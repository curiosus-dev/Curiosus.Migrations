---
sidebar_position: 5
sidebar_label: Supported Databases
---

# Supported databases

<table>
  <tbody>
    <tr>
      <td align="center" valign="middle">
          <img src="https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/main/docs/images/postgresql.png" width="200" />
          <br />
          <b>PostgreSQL</b>
      </td>
      <td align="center" valign="middle">
          <img src="https://raw.githubusercontent.com/curiosus-dev/Curiosus.Migrations/main/docs/images/sqlserver.svg" width="200" />
          <br />
          <b>SQL Server</b>
      </td>
    </tr>
  </tbody>
</table>

MySQL/MariaDB ([#37](https://github.com/curiosus-dev/Curiosus.Migrations/issues/37)) and SQLite
([#38](https://github.com/curiosus-dev/Curiosus.Migrations/issues/38)) are planned for v7, after the engine rework that
makes a new database a small dialect ([#36](https://github.com/curiosus-dev/Curiosus.Migrations/issues/36)). Until then,
a custom `IMigrationConnection` passed through `UseMigrationConnectionFactory` adds any other database; contributions are
welcome.

## PostgreSQL

### Installation

```bash
dotnet add package Curiosus.Migrations.PostgreSQL
```

### Configuration

```csharp
// Configure the migration engine for PostgreSQL
var builder = new MigrationEngineBuilder(services);
builder.UseScriptMigrations().FromDirectory("./Migrations"); // returns the provider, not the builder
builder.UseCodeMigrations().FromAssembly(Assembly.GetExecutingAssembly());
builder.ConfigureForPostgreSql("Host=localhost;Database=myapp;Username=postgres;Password=secret");
builder.UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed);

var migrationEngine = builder.Build();
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| connectionString | PostgreSQL connection string | required |
| migrationTableHistoryName | Name of the table to store migration history | migration_history |
| databaseEncoding | Character set encoding for new database | template database encoding |
| lcCollate | Collation order (LC_COLLATE) for new database | template database value |
| lcCtype | Character classification (LC_CTYPE) for new database | template database value |
| connectionLimit | Max concurrent connections to database | DB default (-1, no limit) |
| template | Template database name for new database creation | template1 |
| tableSpace | Default tablespace for the new database | DB default |

## SQL Server

### Installation

```bash
dotnet add package Curiosus.Migrations.SqlServer
```

### Configuration

```csharp
// Configure the migration engine for SQL Server
var builder = new MigrationEngineBuilder(services);
builder.UseScriptMigrations().FromDirectory("./Migrations"); // returns the provider, not the builder
builder.UseCodeMigrations().FromAssembly(Assembly.GetExecutingAssembly());
builder.ConfigureForSqlServer("Server=localhost;Database=myapp;User Id=sa;Password=YourStrong@Passw0rd;");
builder.UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed);

var migrationEngine = builder.Build();
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| connectionString | SQL Server connection string | required |
| migrationHistoryTableName | Name of table to store migration history | migration_history |
| schemaName | Schema name for migration history table | default user schema (typically dbo) |
| defaultDatabase | Database to connect when target database does not exist | master |
| allowSnapshotIsolation | Whether to enable snapshot isolation | false |
| readCommittedSnapshot | Whether to enable read committed snapshot | false |
| collation | Database collation | server default |
| dataFilePath | Path to data file | SQL Server default |
| logFilePath | Path to log file | SQL Server default |
| initialSize | Initial size of database (MB) | SQL Server default |
| maxSize | Maximum size of database (MB) | SQL Server default |
| fileGrowth | File growth increment (MB) | SQL Server default |
