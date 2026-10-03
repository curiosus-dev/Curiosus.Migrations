using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace Curiosus.Migrations.UnitTests;

/// <summary>
/// Transaction choice of <see cref="MigrationEngine"/> per direction.
/// </summary>
public class MigrationEngineTransactions_Should
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UseDowngradeTransactionSetting_When_Downgrading(bool isUpgradeTransactional, bool isDowngradeTransactional)
    {
        var connection = CreateConnection(applied: [new MigrationVersion(1), new MigrationVersion(2)]);
        var engine = new MigrationEngine(
            connection.Object,
            [CreateMigration(1, isUpgradeTransactional, isDowngradeTransactional),
                CreateMigration(2, isUpgradeTransactional, isDowngradeTransactional)],
            MigrationPolicy.AllAllowed,
            MigrationPolicy.AllAllowed,
            targetVersion: new MigrationVersion(1));

        var result = await engine.DowngradeDatabaseAsync(TestContext.Current.CancellationToken);

        result.IsSuccessfully.Should().BeTrue(result.ErrorMessage);
        connection.Verify(x => x.BeginTransaction(), isDowngradeTransactional ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UseUpgradeTransactionSetting_When_Upgrading(bool isUpgradeTransactional, bool isDowngradeTransactional)
    {
        var connection = CreateConnection(applied: []);
        var engine = new MigrationEngine(
            connection.Object,
            [CreateMigration(1, isUpgradeTransactional, isDowngradeTransactional)],
            MigrationPolicy.AllAllowed,
            MigrationPolicy.AllForbidden);

        var result = await engine.UpgradeDatabaseAsync(TestContext.Current.CancellationToken);

        result.IsSuccessfully.Should().BeTrue(result.ErrorMessage);
        connection.Verify(x => x.BeginTransaction(), isUpgradeTransactional ? Times.Once() : Times.Never());
    }

    private static Mock<IMigrationConnection> CreateConnection(IReadOnlyCollection<MigrationVersion> applied)
    {
        var connection = new Mock<IMigrationConnection>();
        connection
            .Setup(x => x.DatabaseName)
            .Returns("test");
        connection
            .Setup(x => x.CheckIfDatabaseExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        connection
            .Setup(x => x.CheckIfTableExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        connection
            .Setup(x => x.GetAppliedMigrationVersionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(applied);
        connection
            .Setup(x => x.BeginTransaction())
            .Returns(() => Mock.Of<DbTransaction>());

        return connection;
    }

    private static IMigration CreateMigration(long version, bool isUpgradeTransactional, bool isDowngradeTransactional)
    {
        var migration = new Mock<IDowngradeMigration>();
        migration
            .Setup(x => x.Version)
            .Returns(new MigrationVersion(version));
        migration
            .Setup(x => x.Dependencies)
            .Returns(Array.Empty<MigrationVersion>());
        migration
            .Setup(x => x.IsTransactionRequired)
            .Returns(isUpgradeTransactional);
        migration
            .Setup(x => x.IsDowngradeTransactionRequired)
            .Returns(isDowngradeTransactional);
        migration
            .Setup(x => x.UpgradeAsync(It.IsAny<DbTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        migration
            .Setup(x => x.DowngradeAsync(It.IsAny<DbTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return migration.Object;
    }
}
