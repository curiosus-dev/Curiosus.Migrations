using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Curiosus.Migrations;

/// <summary>
/// Migration that uses raw sql to downgrade a database 
/// </summary>
public class DowngradeScriptMigration : ScriptMigration, IDowngradeMigration
{
    /// <summary>
    /// SQL script to undo migration split into batches
    /// </summary>
    public IReadOnlyList<ScriptMigrationBatch> DownScripts { get; }

    private readonly bool? _isDowngradeTransactionRequired;

    /// <inheritdoc />
    /// <remarks>
    /// Set by the <c>TRANSACTION</c> directive of the downgrade script, otherwise the same as for the upgrade.
    /// </remarks>
    public bool IsDowngradeTransactionRequired
    {
        get => _isDowngradeTransactionRequired ?? IsTransactionRequired;
        init => _isDowngradeTransactionRequired = value;
    }

    /// <inheritdoc cref="DowngradeScriptMigration"/>
    public DowngradeScriptMigration(
        ILogger? migrationLogger,
        IMigrationConnection migrationConnection,
        MigrationVersion version,
        IReadOnlyList<ScriptMigrationBatch> upScripts,
        IReadOnlyList<ScriptMigrationBatch>? downScripts,
        string? comment,
        bool isTransactionRequired = true,
        bool isLongRunning = false,
        List<MigrationVersion>? dependencies = null)
        : base(
            migrationLogger,
            migrationConnection,
            version,
            upScripts,
            comment,
            isTransactionRequired,
            isLongRunning,
            dependencies)
    {
        DownScripts = downScripts?.ToArray() ?? Array.Empty<ScriptMigrationBatch>();
    }

    /// <inheritdoc />
    public Task DowngradeAsync(DbTransaction? transaction = null, CancellationToken token = default)
    {
        return RunBatchesAsync(DownScripts, token);
    }
}
