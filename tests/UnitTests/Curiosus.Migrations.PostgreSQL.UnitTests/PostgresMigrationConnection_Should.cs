namespace Curiosus.Migrations.PostgreSQL.UnitTests;

public class PostgresMigrationConnection_Should
{
    [Fact]
    public void ProvideDatabaseAndUserOfConnectionString_AsDefaultVariables()
    {
        var connection = new PostgresMigrationConnection(
            new PostgresMigrationConnectionOptions("Host=localhost;Database=orders;Username=app;Password=secret"));

        var variables = connection.GetDefaultVariables();

        Assert.Equal("orders", variables[DefaultVariables.DbName]);
        Assert.Equal("app", variables[DefaultVariables.User]);
    }
}
