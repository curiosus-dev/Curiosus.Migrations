using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace Curiosus.Migrations.Sample.CodeMigrations;

/// <summary>
/// A code migration with a downgrade: creates the administrator, whose email comes from a variable.
/// </summary>
public class SeedAdminMigration : CodeMigration, IDowngradeMigration, ISampleMigration
{
    public override MigrationVersion Version => new(1, 2);

    public override string Comment => "Seed the administrator";

    public SeedAdminMigration()
    {
        Dependencies = [new MigrationVersion(1, 1)];
    }

    public override async Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        await MigrationConnection.ExecuteNonQuerySqlAsync(
            "INSERT INTO users (name, email) VALUES ('admin', @email)",
            new Dictionary<string, object?> { { "@email", Variables["%ADMIN_EMAIL%"] } },
            cancellationToken);

        Logger?.LogInformation("Administrator created");
    }

    public Task DowngradeAsync(DbTransaction? transaction = null, CancellationToken token = default)
    {
        return MigrationConnection.ExecuteNonQuerySqlAsync("DELETE FROM users WHERE name = 'admin'", null, token);
    }
}
