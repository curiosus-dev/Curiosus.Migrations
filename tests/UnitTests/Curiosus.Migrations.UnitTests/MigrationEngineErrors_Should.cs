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
/// Error and cancellation handling of <see cref="MigrationEngine"/>.
/// </summary>
public class MigrationEngineErrors_Should
{
    [Fact]
    public async Task ReturnExceptionAndItsMessage_When_MigrationFails()
    {
        var failure = new InvalidOperationException("relation \"users\" already exists");
        var engine = CreateEngine(CreateMigration((_, _) => throw failure));

        var result = await engine.UpgradeDatabaseAsync(TestContext.Current.CancellationToken);

        result.IsSuccessfully.Should().BeFalse();
        result.ErrorCode.Should().Be(MigrationErrorCode.MigratingError);
        result.ErrorMessage.Should().Contain("relation \"users\" already exists");
        result.Exception.Should().BeOfType<MigrationException>().Which.InnerException.Should().BeSameAs(failure);
        result.FailedMigration!.Value.Version.Should().Be(new MigrationVersion(1));
    }

    [Fact]
    public async Task ReturnErrorCodeOfMigrationException_When_MigrationThrowsIt()
    {
        var engine = CreateEngine(CreateMigration((_, _) =>
            throw new MigrationException(MigrationErrorCode.AuthorizationError, "No permission")));

        var result = await engine.UpgradeDatabaseAsync(TestContext.Current.CancellationToken);

        result.ErrorCode.Should().Be(MigrationErrorCode.AuthorizationError);
        result.ErrorMessage.Should().Be("No permission");
    }

    [Fact]
    public async Task ReturnUnknownErrorWithMessage_When_ConnectionFailsUnexpectedly()
    {
        var connection = CreateConnection();
        connection
            .Setup(x => x.GetAppliedMigrationVersionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Server timeout"));
        var engine = CreateEngine(connection, CreateMigration((_, _) => Task.CompletedTask));

        var result = await engine.UpgradeDatabaseAsync(TestContext.Current.CancellationToken);

        result.ErrorCode.Should().Be(MigrationErrorCode.UnknownError);
        result.ErrorMessage.Should().EndWith("Server timeout");
        result.Exception.Should().BeOfType<TimeoutException>();
    }

    [Fact]
    public async Task ReturnCancelledWithOriginalException_When_CancelledDuringMigration()
    {
        using var cts = new CancellationTokenSource();
        OperationCanceledException cancellation = null;
        var engine = CreateEngine(CreateMigration((_, token) =>
        {
            cts.Cancel();
            try
            {
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException e)
            {
                cancellation = e;
                throw;
            }

            return Task.CompletedTask;
        }));

        var result = await engine.UpgradeDatabaseAsync(cts.Token);

        result.ErrorCode.Should().Be(MigrationErrorCode.Cancelled);
        result.Exception.Should().BeSameAs(cancellation);
        result.FailedMigration!.Value.Version.Should().Be(new MigrationVersion(1));
    }

    [Fact]
    public async Task ReturnOriginalError_When_FailedWithoutCancellationWhileTokenIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        var engine = CreateEngine(CreateMigration((_, _) =>
        {
            cts.Cancel();
            throw new MigrationException(MigrationErrorCode.MigratingError, "syntax error at or near \"SELEC\"");
        }));

        var result = await engine.UpgradeDatabaseAsync(cts.Token);

        result.ErrorCode.Should().Be(MigrationErrorCode.MigratingError);
        result.ErrorMessage.Should().Contain("SELEC");
    }

    [Fact]
    public async Task ReturnErrorCodeOfMigrationException_When_PreMigrationThrowsIt()
    {
        var preMigration = new Mock<IMigration>();
        preMigration
            .Setup(x => x.Version)
            .Returns(new MigrationVersion(0, 1));
        preMigration
            .Setup(x => x.Dependencies)
            .Returns(new List<MigrationVersion>());
        preMigration
            .Setup(x => x.UpgradeAsync(It.IsAny<DbTransaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MigrationException(MigrationErrorCode.AuthorizationError, "No permission"));
        var engine = new MigrationEngine(
            CreateConnection().Object,
            new List<IMigration> { CreateMigration((_, _) => Task.CompletedTask) },
            MigrationPolicy.AllAllowed,
            MigrationPolicy.AllForbidden,
            new List<IMigration> { preMigration.Object });

        var result = await engine.UpgradeDatabaseAsync(TestContext.Current.CancellationToken);

        result.ErrorCode.Should().Be(MigrationErrorCode.AuthorizationError);
        result.ErrorMessage.Should().EndWith("No permission");
    }

    [Fact]
    public void CreateFailedWithNullFailedMigration_StillCompiles()
    {
        var result = MigrationResult.CreateFailed(MigrationErrorCode.MigratingError, "error", null);

        result.Exception.Should().BeNull();
    }

    private static Mock<IMigrationConnection> CreateConnection()
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
            .ReturnsAsync(Array.Empty<MigrationVersion>());
        connection
            .Setup(x => x.BeginTransaction())
            .Returns(() => Mock.Of<DbTransaction>());

        return connection;
    }

    private static IMigration CreateMigration(Func<DbTransaction, CancellationToken, Task> upgrade)
    {
        var migration = new Mock<IMigration>();
        migration
            .Setup(x => x.Version)
            .Returns(new MigrationVersion(1));
        migration
            .Setup(x => x.Dependencies)
            .Returns(new List<MigrationVersion>());
        migration
            .Setup(x => x.UpgradeAsync(It.IsAny<DbTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(upgrade);

        return migration.Object;
    }

    private static MigrationEngine CreateEngine(IMigration migration)
    {
        return CreateEngine(CreateConnection(), migration);
    }

    private static MigrationEngine CreateEngine(Mock<IMigrationConnection> connection, IMigration migration)
    {
        return new MigrationEngine(
            connection.Object,
            new List<IMigration> { migration },
            MigrationPolicy.AllAllowed,
            MigrationPolicy.AllForbidden);
    }
}
