using Curiosus.Migrations.IntegrationTests.Shared;
using Curiosus.Migrations.SqlServer.IntegrationTests.Fixtures;
using Xunit;

namespace Curiosus.Migrations.SqlServer.IntegrationTests.EngineTests;

public class SqlServerEngineTests(SqlServerContainerFixture containerFixture) : EngineTestsBase, IClassFixture<SqlServerContainerFixture>
{
    protected override string GetConnectionString(string database) => containerFixture.GetConnectionString(database);

    protected override void ConfigureConnection(MigrationEngineBuilder builder, string connectionString) =>
        builder.ConfigureForSqlServer(connectionString);

    protected override IMigrationConnection CreateConnection(string connectionString) =>
        new SqlServerMigrationConnection(new SqlServerMigrationConnectionOptions(connectionString));
}
