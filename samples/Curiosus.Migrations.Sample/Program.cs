using Curiosus.Migrations;
using Curiosus.Migrations.PostgreSQL;
using Curiosus.Migrations.Sample.CodeMigrations;
using Microsoft.Extensions.Logging;

// Usage:
//   dotnet run -f net10.0                    applies short-running migrations, as an application does on startup
//   dotnet run -f net10.0 -- --long-running  applies long-running migrations too, as a separate job does
//   dotnet run -f net10.0 -- --downgrade 1.0 reverts the database to version 1.0
var connectionString = Environment.GetEnvironmentVariable("CURIOSUS_SAMPLE_CONNECTION")
    ?? "Host=localhost;Port=5432;Database=curiosus_sample;Username=postgres;Password=postgres";

using var loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole(o => o.SingleLine = true));
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var downgradeIndex = Array.IndexOf(args, "--downgrade");
var isDowngrade = downgradeIndex >= 0;

var builder = new MigrationEngineBuilder();
builder.UseScriptMigrations().FromDirectory(Path.Combine(AppContext.BaseDirectory, "Migrations"));
builder.UseCodeMigrations().FromAssembly<ISampleMigration>(typeof(ISampleMigration).Assembly);
builder.ConfigureForPostgreSql(connectionString);
builder.UseLogger(loggerFactory.CreateLogger("Migrations"));
builder.UseVariable("%ADMIN_EMAIL%", "admin@example.com");

// Long-running migrations (data backfills, concurrent indexes) are kept out of the application startup.
builder.UseUpgradeMigrationPolicy(args.Contains("--long-running")
    ? MigrationPolicy.AllAllowed
    : MigrationPolicy.ShortRunningAllowed);
builder.UseDowngradeMigrationPolicy(isDowngrade
    ? MigrationPolicy.AllAllowed
    : MigrationPolicy.AllForbidden);

if (isDowngrade)
{
    if (downgradeIndex + 1 >= args.Length || !MigrationVersion.TryParse(args[downgradeIndex + 1], out var target))
    {
        Console.Error.WriteLine("Specify the version to downgrade to, for example: --downgrade 1.0");
        return 2;
    }

    builder.SetUpTargetVersion(target);
}

var engine = builder.Build();

var result = isDowngrade
    ? await engine.DowngradeDatabaseAsync(cancellation.Token)
    : await engine.UpgradeDatabaseAsync(cancellation.Token);

if (result.ErrorCode == MigrationErrorCode.Cancelled)
{
    Console.Error.WriteLine($"Migration {result.FailedMigration?.Version} was cancelled");
    return 3;
}

if (!result.IsSuccessfully)
{
    Console.Error.WriteLine($"Migration {result.FailedMigration?.Version} failed ({result.ErrorCode}): {result.ErrorMessage}");
    return 1;
}

Console.WriteLine($"Applied: {String.Join(", ", result.AppliedMigrations.Select(x => x.Version))}");
Console.WriteLine($"Skipped by policy: {String.Join(", ", result.SkippedByPolicyMigrations.Select(x => x.Version))}");
return 0;
