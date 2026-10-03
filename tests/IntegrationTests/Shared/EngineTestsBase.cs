using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Curiosus.Migrations.IntegrationTests.Shared;

/// <summary>
/// Runs the whole engine against a database: script and code migrations, with and without a transaction and a comment,
/// upgrade and downgrade. Linked into the integration test project of every provider.
/// </summary>
public abstract class EngineTestsBase : IDisposable
{
    private readonly string _scriptsDirectory;

    protected EngineTestsBase()
    {
        _scriptsDirectory = Path.Combine(Path.GetTempPath(), $"curiosus_engine_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_scriptsDirectory);

        WriteScript("1.sql", "CREATE TABLE engine_t1 (id INT NOT NULL);");
        WriteScript("2-with_comment.sql", "INSERT INTO engine_t1 (id) VALUES (2);");
        WriteScript("3.sql", "--CURIOSUS:TRANSACTION=OFF\nCREATE TABLE engine_t3 (id INT NOT NULL);");
        WriteScript("4.up.sql", "CREATE TABLE engine_t4 (id INT NOT NULL);");
        WriteScript("4.down.sql", "DROP TABLE engine_t4;");
    }

    [Fact]
    public async Task Upgrade_ScriptsAndCodeWithAndWithoutTransactionOrComment_AppliesAll()
    {
        var connectionString = GetConnectionString(NewDatabaseName());

        var result = await CreateEngine(connectionString).UpgradeDatabaseAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessfully, result.ErrorMessage);
        Assert.Equal(
            new[] { new MigrationVersion(1), new(2), new(3), new(4), new(5), new(6) },
            await GetAppliedVersionsAsync(connectionString));
        Assert.Equal(3, await CountRowsAsync(connectionString, "engine_t1"));
    }

    [Fact]
    public async Task Downgrade_ScriptAndCodeMigrations_RevertsToTarget()
    {
        var connectionString = GetConnectionString(NewDatabaseName());
        var upgradeResult = await CreateEngine(connectionString).UpgradeDatabaseAsync(TestContext.Current.CancellationToken);
        Assert.True(upgradeResult.IsSuccessfully, upgradeResult.ErrorMessage);

        var result = await CreateEngine(connectionString, new MigrationVersion(3))
            .DowngradeDatabaseAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessfully, result.ErrorMessage);
        Assert.Equal(
            new[] { new MigrationVersion(1), new(2), new(3) },
            await GetAppliedVersionsAsync(connectionString));
        Assert.Equal(1, await CountRowsAsync(connectionString, "engine_t1"));
        Assert.False(await TableExistsAsync(connectionString, "engine_t4"));
    }

    public void Dispose()
    {
        Directory.Delete(_scriptsDirectory, recursive: true);
    }

    protected abstract string GetConnectionString(string database);

    protected abstract void ConfigureConnection(MigrationEngineBuilder builder, string connectionString);

    protected abstract IMigrationConnection CreateConnection(string connectionString);

    private IMigrationEngine CreateEngine(string connectionString, MigrationVersion? targetVersion = null)
    {
        var builder = new MigrationEngineBuilder();
        builder.UseScriptMigrations().FromDirectory(_scriptsDirectory);
        builder.UseCodeMigrations().FromAssembly<IEngineTestMigration>(Assembly.GetExecutingAssembly());
        ConfigureConnection(builder, connectionString);
        builder.UseUpgradeMigrationPolicy(MigrationPolicy.AllAllowed);
        builder.UseDowngradeMigrationPolicy(MigrationPolicy.AllAllowed);
        if (targetVersion.HasValue)
        {
            builder.SetUpTargetVersion(targetVersion.Value);
        }

        return builder.Build();
    }

    private void WriteScript(string fileName, string script)
    {
        File.WriteAllText(Path.Combine(_scriptsDirectory, fileName), script);
    }

    private static string NewDatabaseName() => $"engine_{Guid.NewGuid():N}";

    private async Task<IReadOnlyCollection<MigrationVersion>> GetAppliedVersionsAsync(string connectionString)
    {
        using var connection = CreateConnection(connectionString);
        await connection.OpenConnectionAsync();

        return await connection.GetAppliedMigrationVersionsAsync();
    }

    private async Task<long> CountRowsAsync(string connectionString, string tableName)
    {
        using var connection = CreateConnection(connectionString);
        await connection.OpenConnectionAsync();

        return Convert.ToInt64(await connection.ExecuteScalarSqlAsync($"SELECT COUNT(*) FROM {tableName}", null));
    }

    private async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        using var connection = CreateConnection(connectionString);
        await connection.OpenConnectionAsync();

        return await connection.CheckIfTableExistsAsync(tableName);
    }
}

public interface IEngineTestMigration;

/// <summary>
/// Transactional code migration without a comment that writes through the migration connection.
/// </summary>
public class EngineCodeMigration : CodeMigration, IDowngradeMigration, IEngineTestMigration
{
    public override MigrationVersion Version => new(5);

    public override string? Comment => null;

    public override Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return MigrationConnection.ExecuteNonQuerySqlAsync("INSERT INTO engine_t1 (id) VALUES (5)", null, cancellationToken);
    }

    public Task DowngradeAsync(DbTransaction? transaction = null, CancellationToken token = default)
    {
        return MigrationConnection.ExecuteNonQuerySqlAsync("DELETE FROM engine_t1 WHERE id = 5", null, token);
    }
}

/// <summary>
/// Transactional code migration that runs a command directly on the connection, passing the transaction it gets:
/// SqlClient requires it, Npgsql accepts it.
/// </summary>
public class EngineDirectConnectionMigration : CodeMigration, IDowngradeMigration, IEngineTestMigration
{
    public override MigrationVersion Version => new(6);

    public override string Comment => "direct_connection";

    public override Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("INSERT INTO engine_t1 (id) VALUES (6)", transaction, cancellationToken);
    }

    public Task DowngradeAsync(DbTransaction? transaction = null, CancellationToken token = default)
    {
        return ExecuteAsync("DELETE FROM engine_t1 WHERE id = 6", transaction, token);
    }

    private async Task ExecuteAsync(string sql, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = MigrationConnection.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
