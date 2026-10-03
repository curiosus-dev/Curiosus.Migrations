using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Curiosus.Migrations.UnitTests;

/// <summary>
/// Unit tests for <see cref="ScriptMigrationsProvider"/>
/// </summary>
public class ScriptMigrationsProvider_Should
{
    [Fact]
    public void GetMigrations_FromDirectory_Ok()
    {
        var dbProvider = Mock.Of<IMigrationConnection>();
        var logger = Mock.Of<ILogger>();

        var migrationsProvider = new ScriptMigrationsProvider();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "ScriptsAsFiles");
        migrationsProvider.FromDirectory(path);

        var migrations = migrationsProvider
            .GetMigrations(dbProvider, new Dictionary<string, string>(), logger)
            .ToList();
            
        Assert.Equal(5, migrations.Count);
            
        Assert.True(migrations[0] is DowngradeScriptMigration);
        Assert.Equal(new MigrationVersion(1), migrations[0].Version);
        Assert.Equal("comment", migrations[0].Comment);
        Assert.Equal("up", ((DowngradeScriptMigration)migrations[0]).UpScripts[0].Script);
        Assert.Equal("down", ((DowngradeScriptMigration)migrations[0]).DownScripts[0].Script);
            
        Assert.True(migrations[1] is DowngradeScriptMigration);
        Assert.Equal(new MigrationVersion(1,1), migrations[1].Version);
        Assert.True(String.IsNullOrEmpty(migrations[1].Comment));
        Assert.Equal("up", ((DowngradeScriptMigration)migrations[1]).UpScripts[0].Script);
        Assert.Equal("down", ((DowngradeScriptMigration)migrations[1]).DownScripts[0].Script);

        Assert.True(migrations[2] is ScriptMigration);
        Assert.Equal(new MigrationVersion(1,2), migrations[2].Version);
        Assert.Equal("comment", migrations[2].Comment);
        Assert.Equal("up", ((ScriptMigration)migrations[2]).UpScripts[0].Script);
            
        Assert.True(migrations[3] is ScriptMigration);
        Assert.Equal(new MigrationVersion(1,3), migrations[3].Version);
        Assert.True(String.IsNullOrEmpty(migrations[3].Comment));
        Assert.Equal("up", ((ScriptMigration)migrations[3]).UpScripts[0].Script);
    }

    [Fact]
    public void GetMigrations_FromAssembly_Ok()
    {
        var dbProvider = Mock.Of<IMigrationConnection>();
        var logger = Mock.Of<ILogger>();

        var migrationsProvider = new ScriptMigrationsProvider();
        migrationsProvider.FromAssembly(
            Assembly.GetExecutingAssembly(),
            "Curiosus.Migrations.UnitTests.ScriptsAsResources.Main");

        var migrations = migrationsProvider
            .GetMigrations(dbProvider, new Dictionary<string, string>(), logger)
            .ToList();
            
        Assert.Equal(4, migrations.Count);
            
        Assert.True(migrations[0] is DowngradeScriptMigration);
        Assert.Equal(new MigrationVersion(1), migrations[0].Version);
        Assert.Equal("comment", migrations[0].Comment);
        Assert.Equal("up", ((DowngradeScriptMigration)migrations[0]).UpScripts[0].Script);
        Assert.Equal("down", ((DowngradeScriptMigration)migrations[0]).DownScripts[0].Script);
            
            
        Assert.True(migrations[1] is DowngradeScriptMigration);
        Assert.Equal(new MigrationVersion(1,1), migrations[1].Version);
        Assert.True(String.IsNullOrEmpty(migrations[1].Comment));
        Assert.Equal("up", ((DowngradeScriptMigration)migrations[1]).UpScripts[0].Script);
        Assert.Equal("down", ((DowngradeScriptMigration)migrations[1]).DownScripts[0].Script);
            
            
        Assert.True(migrations[2] is ScriptMigration);
        Assert.Equal(new MigrationVersion(1,2), migrations[2].Version);
        Assert.Equal("comment", migrations[2].Comment);
        Assert.Equal("up", ((ScriptMigration)migrations[2]).UpScripts[0].Script);
            
        Assert.True(migrations[3] is ScriptMigration);
        Assert.Equal(new MigrationVersion(1,3), migrations[3].Version);
        Assert.True(String.IsNullOrEmpty(migrations[3].Comment));
        Assert.Equal("up", ((ScriptMigration)migrations[3]).UpScripts[0].Script);
    }
       
    [Fact]
    public void SubstituteVariableToTemplate()
    {
        // arrange
        var dbProvider = Mock.Of<IMigrationConnection>();
        var logger = Mock.Of<ILogger>();

        var migrationsProvider = new ScriptMigrationsProvider();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "ScriptsAsFiles");
        migrationsProvider.FromDirectory(path);

        var userName = "user";
        var variables = new Dictionary<string, string>
        {
            {DefaultVariables.User, userName}
        };
            
        // act
        var migrations = migrationsProvider
            .GetMigrations(dbProvider, variables, logger)
            .ToList();
            
        // assert
        migrations.Count.Should().Be(5, "because we have 5 migrations in scripts directory");

        ((ScriptMigration) migrations[4]).UpScripts[0].Script.Should()
            .BeEquivalentTo(userName, "because script contains only template");
    }


    [Fact]
    public void Should_ThrowException_BecauseOfIncorrectNaming()
    {
        var dbProvider = Mock.Of<IMigrationConnection>();
        var logger = Mock.Of<ILogger>();

        var migrationsProvider = new ScriptMigrationsProvider();
        migrationsProvider.FromAssembly(
            typeof(ScriptConstants).Assembly,
            "Curiosus.Migrations.UnitTests.ScriptsAsResources.IncorrectNamingTest",
            ScriptIncorrectNamingAction.ThrowException);

        Assert.Throws<MigrationException>(() =>
        {
            var _ = migrationsProvider
                .GetMigrations(dbProvider, new Dictionary<string, string>(), logger)
                .ToList();
        });
    }

    [Fact]
    public void GetMigrations_ScriptDirectives_SupportsCuriosusAndLegacyCuriosityPrefixes()
    {
        var dbProvider = Mock.Of<IMigrationConnection>();
        var logger = Mock.Of<ILogger>();

        var migrationsProvider = new ScriptMigrationsProvider();
        migrationsProvider.FromAssembly(
            Assembly.GetExecutingAssembly(),
            "Curiosus.Migrations.UnitTests.ScriptsAsResources.Directives");

        var migrations = migrationsProvider
            .GetMigrations(dbProvider, new Dictionary<string, string>(), logger)
            .Cast<ScriptMigration>()
            .ToList();

        migrations.Should().HaveCount(3);
        foreach (var migration in migrations.Take(2))
        {
            migration.IsTransactionRequired.Should().BeFalse($"migration {migration.Version} disables transactions");
            migration.IsLongRunning.Should().BeTrue($"migration {migration.Version} is long-running");
            migration.Dependencies.Should().Equal(new MigrationVersion(0, 1));
        }

        migrations[2].IsTransactionRequired.Should().BeTrue();
        migrations[2].IsLongRunning.Should().BeFalse();
        migrations[2].Dependencies.Should().BeEmpty();
    }

    [Theory]
    [InlineData("--CURIOSUS:TRANSACTION=OFF\nSELECT 1;")]
    [InlineData("-- CURIOSUS: TRANSACTION = OFF\nSELECT 1;")]
    [InlineData("-- curiosus:  transaction  =  off  \r\nSELECT 1;")]
    [InlineData("SELECT 1;\n-- CURIOSUS: TRANSACTION = OFF")]
    [InlineData("-- CURIOSITY: TRANSACTION = OFF;\nSELECT 1;")]
    public void GetMigrations_ScriptDirectives_AcceptSpacesCaseAndLastLine(string script)
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("1.sql", script);

        var migration = GetSingleMigration(directory);

        migration.IsTransactionRequired.Should().BeFalse();
    }

    [Fact]
    public void GetMigrations_ScriptDirectives_SpacedFormSetsAllOptions()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write(
            "2.sql",
            "-- CURIOSUS: LONG-RUNNING = TRUE\n-- CURIOSUS: DEPENDENCIES = 0.1, 1\nSELECT 1;");

        var migration = GetSingleMigration(directory);

        migration.IsLongRunning.Should().BeTrue();
        migration.Dependencies.Should().Equal(new MigrationVersion(0, 1), new MigrationVersion(1));
    }

    [Fact]
    public void GetMigrations_UnknownDirective_Throws()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("1.sql", "-- CURIOSUS: UNKNOWN = 1\nSELECT 1;");

        var act = () => GetSingleMigration(directory);

        act.Should().Throw<InvalidOperationException>().WithMessage("*\"UNKNOWN\" is unknown*");
    }

    [Fact]
    public void GetMigrations_DownScriptWithoutDirectives_InheritsUpScriptTransactionWithoutWarning()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("1.up-create_index.sql", "-- CURIOSUS: TRANSACTION = OFF\nCREATE INDEX CONCURRENTLY ix ON t (c);");
        directory.Write("1.down-drop_index.sql", "DROP INDEX CONCURRENTLY ix;");
        var logger = new Mock<ILogger>();

        var migration = GetSingleMigration(directory, logger.Object);

        migration.IsTransactionRequired.Should().BeFalse();
        migration.Should().BeOfType<DowngradeScriptMigration>().Which.IsDowngradeTransactionRequired.Should().BeFalse();
        migration.Comment.Should().Be("create_index");
        VerifyWarnings(logger, Times.Never());
    }

    [Fact]
    public void GetMigrations_DownScriptDeclaresTransaction_AppliesToDowngradeOnly()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("1.up.sql", "CREATE INDEX ix ON t (c);");
        directory.Write("1.down.sql", "-- CURIOSUS: TRANSACTION = OFF\nDROP INDEX CONCURRENTLY ix;");
        var logger = new Mock<ILogger>();

        var migration = GetSingleMigration(directory, logger.Object);

        migration.IsTransactionRequired.Should().BeTrue();
        migration.Should().BeOfType<DowngradeScriptMigration>().Which.IsDowngradeTransactionRequired.Should().BeFalse();
        VerifyWarnings(logger, Times.Never());
    }

    [Fact]
    public void GetMigrations_DownScriptDeclaresDifferentLongRunning_UpScriptOneAppliesWithWarning()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("1.up.sql", "-- CURIOSUS: LONG-RUNNING = TRUE\nSELECT 1;");
        directory.Write("1.down.sql", "-- CURIOSUS: LONG-RUNNING = FALSE\nSELECT 2;");
        var logger = new Mock<ILogger>();

        var migration = GetSingleMigration(directory, logger.Object);

        migration.IsLongRunning.Should().BeTrue();
        VerifyWarnings(logger, Times.Once());
    }

    [Fact]
    public void GetMigrations_SameDependenciesInAnotherOrder_NoWarning()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("3.up.sql", "-- CURIOSUS: DEPENDENCIES = 1, 2\nSELECT 1;");
        directory.Write("3.down.sql", "-- CURIOSUS: DEPENDENCIES = 2\n-- CURIOSUS: DEPENDENCIES = 1\nSELECT 2;");
        var logger = new Mock<ILogger>();

        var migration = GetSingleMigration(directory, logger.Object);

        migration.Dependencies.Should().Equal(new MigrationVersion(1), new MigrationVersion(2));
        VerifyWarnings(logger, Times.Never());
    }

    [Fact]
    public void GetMigrations_IncorrectDependency_ErrorNamesIt()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("3.sql", "-- CURIOSUS: DEPENDENCIES = 1, abc\nSELECT 1;");

        var act = () => GetSingleMigration(directory);

        act.Should().Throw<InvalidOperationException>().WithMessage("*\"abc\"*");
    }

    [Fact]
    public void GetMigrations_TurkishCulture_RecognizesLowerCaseDirectives()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("1.sql", "-- curiosus: transaction = off\n-- curiosus: long-running = true\nSELECT 1;");
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            var migration = GetSingleMigration(directory);

            migration.IsTransactionRequired.Should().BeFalse();
            migration.IsLongRunning.Should().BeTrue();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void GetMigrations_EmptyUpScriptComment_CommentIsTakenFromDownScript()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("1.up-.sql", "SELECT 1;");
        directory.Write("1.down-drop_users.sql", "SELECT 2;");

        var migration = GetSingleMigration(directory);

        migration.Comment.Should().Be("drop_users");
    }

    [Fact]
    public void GetMigrations_OnlyDownScriptHasComment_CommentIsTakenFromIt()
    {
        using var directory = new TempScriptsDirectory();
        directory.Write("1.up.sql", "SELECT 1;");
        directory.Write("1.down-comment.sql", "SELECT 2;");

        var migration = GetSingleMigration(directory);

        migration.Comment.Should().Be("comment");
        migration.IsTransactionRequired.Should().BeTrue();
    }

    private static void VerifyWarnings(Mock<ILogger> logger, Times times)
    {
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times);
    }

    private static IMigration GetSingleMigration(TempScriptsDirectory directory, ILogger logger = null)
    {
        var migrationsProvider = new ScriptMigrationsProvider();
        migrationsProvider.FromDirectory(directory.Path);

        return migrationsProvider
            .GetMigrations(Mock.Of<IMigrationConnection>(), new Dictionary<string, string>(), logger ?? Mock.Of<ILogger>())
            .Should()
            .ContainSingle()
            .Subject;
    }

    private sealed class TempScriptsDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"curiosus_scripts_{Guid.NewGuid():N}");

        public TempScriptsDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Write(string fileName, string script)
        {
            File.WriteAllText(System.IO.Path.Combine(Path, fileName), script);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
