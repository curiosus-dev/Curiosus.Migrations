using Curiosus.Migrations.IntegrationTests.Shared;
using Curiosus.Migrations.PostgreSQL;
using Curiosus.Migrations.PostgreSql.IntegrationTests.Fixtures;
using Xunit;

namespace Curiosus.Migrations.PostgreSql.IntegrationTests.EngineTests;

public class PostgresEngineTests(PostgresContainerFixture containerFixture)
    : EngineTestsBase, IClassFixture<PostgresContainerFixture>
{
    protected override string GetConnectionString(string database) => containerFixture.GetConnectionString(database);

    protected override void ConfigureConnection(MigrationEngineBuilder builder, string connectionString) =>
        builder.ConfigureForPostgreSql(connectionString);

    protected override IMigrationConnection CreateConnection(string connectionString) =>
        new PostgresMigrationConnection(new PostgresMigrationConnectionOptions(connectionString));
}
