using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Migrations.SqlServer.IntegrationTests.Fixtures;
using Xunit;

namespace Curiosus.Migrations.SqlServer.IntegrationTests.EngineTests;

/// <summary>
/// Runs the whole engine against SQL Server: script and code migrations, with and without a transaction and a comment,
/// upgrade and downgrade.
/// </summary>
public class SqlServerEngineTests : IClassFixture<SqlServerContainerFixture>, IDisposable
{
    private readonly SqlServerContainerFixture _containerFixture;
    private readonly string _scriptsDirectory;

    public SqlServerEngineTests(SqlServerContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
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
        var connectionString = _containerFixture.GetConnectionString(NewDatabaseName());

        var result = await CreateEngine(connectionString).UpgradeDatabaseAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessfully, result.ErrorMessage);
        Assert.Equal(
            new[] { new MigrationVersion(1), new(2), new(3), new(4), new(5) },
            await GetAppliedVersionsAsync(connectionString));
        Assert.Equal(2, await CountRowsAsync(connectionString, "engine_t1"));
    }

    [Fact]
    public async Task Downgrade_ScriptAndCodeMigrations_RevertsToTarget()
    {
        var connectionString = _containerFixture.GetConnectionString(NewDatabaseName());
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

    private IMigrationEngine CreateEngine(string connectionString, MigrationVersion? targetVersion = null)
    {
        var builder = new MigrationEngineBuilder();
        builder.UseScriptMigrations().FromDirectory(_scriptsDirectory);
        builder.UseCodeMigrations().FromAssembly<IEngineTestMigration>(Assembly.GetExecutingAssembly());
        builder.ConfigureForSqlServer(connectionString);
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

    private static async Task<IReadOnlyCollection<MigrationVersion>> GetAppliedVersionsAsync(string connectionString)
    {
        using var connection = new SqlServerMigrationConnection(new SqlServerMigrationConnectionOptions(connectionString));
        await connection.OpenConnectionAsync();

        return await connection.GetAppliedMigrationVersionsAsync();
    }

    private static async Task<long> CountRowsAsync(string connectionString, string tableName)
    {
        using var connection = new SqlServerMigrationConnection(new SqlServerMigrationConnectionOptions(connectionString));
        await connection.OpenConnectionAsync();

        return Convert.ToInt64(await connection.ExecuteScalarSqlAsync($"SELECT COUNT(*) FROM {tableName}", null));
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        using var connection = new SqlServerMigrationConnection(new SqlServerMigrationConnectionOptions(connectionString));
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
