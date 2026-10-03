using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.Migrations;

/// <summary>
/// Engine that executes configured migrations.
/// </summary>
public interface IMigrationEngine
{
    /// <summary>
    /// Upgrades database according to migration configuration.
    /// </summary>
    /// <remarks>
    /// Errors, including cancellation with <paramref name="cancellationToken"/>, are returned in the result:
    /// cancellation gives <see cref="MigrationErrorCode.Cancelled"/>.
    /// </remarks>
    Task<MigrationResult> UpgradeDatabaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downgrades database according to migration configuration.
    /// </summary>
    /// <remarks>
    /// Errors, including cancellation with <paramref name="cancellationToken"/>, are returned in the result:
    /// cancellation gives <see cref="MigrationErrorCode.Cancelled"/>.
    /// </remarks>
    Task<MigrationResult> DowngradeDatabaseAsync(CancellationToken cancellationToken = default);
}
