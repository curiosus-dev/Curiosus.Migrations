using System.Data.Common;
using Curiosus.Migrations.Utils;
using Microsoft.Extensions.Logging;

namespace Curiosus.Migrations.Sample.CodeMigrations;

/// <summary>
/// A long-running data migration: fills <c>email_normalized</c> in batches, each in its own transaction, with a pause
/// between them to keep the load on the database low.
/// </summary>
public class NormalizeEmailsMigration : MassUpdateCodeMigrationBase, IDowngradeMigration, ISampleMigration
{
    public override MigrationVersion Version => new(2, 1);

    public override string Comment => "Normalize user emails";

    public NormalizeEmailsMigration() : base(TimeSpan.FromMilliseconds(100))
    {
        Dependencies = [new MigrationVersion(1, 1)];
    }

    public override async Task UpgradeAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        const string query = """
            WITH batch AS (
                SELECT id
                FROM users
                WHERE id > @id
                ORDER BY id
                LIMIT 1000)
            UPDATE users u
                SET email_normalized = lower(trim(u.email))
            FROM batch
            WHERE batch.id = u.id
            RETURNING batch.id;
            """;

        var total = await DoMassUpdateAsync(
            query,
            (processed, totalProcessed) => Logger?.LogInformation($"Normalized {processed} emails, {totalProcessed} in total"),
            cancellationToken);

        Logger?.LogInformation($"Normalized {total} emails");
    }

    public Task DowngradeAsync(DbTransaction? transaction = null, CancellationToken token = default)
    {
        return MigrationConnection.ExecuteNonQuerySqlAsync("UPDATE users SET email_normalized = NULL", null, token);
    }
}
