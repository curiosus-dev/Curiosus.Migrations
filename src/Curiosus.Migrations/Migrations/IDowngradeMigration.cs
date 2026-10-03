using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.Migrations;

/// <summary>
/// Migration that supports downgrade/
/// </summary>
public interface IDowngradeMigration : IMigration
{
    /// <summary>
    /// Downgrades database to the previous version undoing changes of this migration.
    /// </summary>
    Task DowngradeAsync(DbTransaction? transaction = null, CancellationToken token = default);

    /// <summary>
    /// Should the downgrade be executed in a transaction? By default the same as <see cref="IMigration.IsTransactionRequired"/>.
    /// </summary>
    /// <remarks>
    /// Override it when the downgrade can't run in a transaction while the upgrade can, or vice versa,
    /// for example <c>DROP INDEX CONCURRENTLY</c> undoing a transactional <c>CREATE INDEX</c>.
    /// </remarks>
    bool IsDowngradeTransactionRequired => IsTransactionRequired;
}
