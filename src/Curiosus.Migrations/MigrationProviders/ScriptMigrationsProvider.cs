using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Curiosus.Migrations;

/// <summary>
/// Provide migrations that uses raw sql scripts from specified directories
/// </summary>
public class ScriptMigrationsProvider : IMigrationsProvider
{
    private static readonly Regex MigrationFileNameRegex = new(
        MigrationConstants.MigrationFileNamePattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // CURIOSITY is the prefix of the package before it was renamed to Curiosus, existing scripts still use it.
    private static readonly Regex OptionSplitRegex = new(
        @"(?=--\s*(?:CURIOSUS|CURIOSITY):)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OptionRegex = new(
        @"--\s*(?:CURIOSUS|CURIOSITY):\s*([^\s=]+)\s*=\s*(.*?)\s*(?:\n|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BatchSplitRegex = new(@"(?=--\s*BATCH:)", RegexOptions.Compiled);

    private static readonly Regex BatchNameRegex = new(
        @"--\s*BATCH:\s*(.*)\s*\n(.*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const ScriptIncorrectNamingAction DefaultScriptIncorrectNamingAction = ScriptIncorrectNamingAction.LogToWarn;

    private readonly Dictionary<string, ScriptParsingOptions> _absoluteDirectoriesPathParsingOptions;
    private readonly Dictionary<Assembly, ScriptParsingOptions> _assembliesParsingOptions;

    /// <inheritdoc cref="ScriptMigrationsProvider"/>
    public ScriptMigrationsProvider()
    {
        // usually only one item will be added
        _absoluteDirectoriesPathParsingOptions = new Dictionary<string, ScriptParsingOptions>(1);
        // usually only one item will be added
        _assembliesParsingOptions = new Dictionary<Assembly, ScriptParsingOptions>(1);
    }

    /// <summary>
    /// Setup directory to scan for migrations
    /// </summary>
    /// <param name="path">
    /// Path to directory where scripts are located. Can be relative and absolute.
    /// If relative, <see cref="Directory.GetCurrentDirectory"/> will be used to specify full path to migrations.
    /// </param>
    /// <param name="scriptIncorrectNamingAction">What should we do if found script file with incorrect naming?</param>
    /// <exception cref="ArgumentNullException"></exception>
    public ScriptMigrationsProvider FromDirectory(
        string path,
        ScriptIncorrectNamingAction scriptIncorrectNamingAction = DefaultScriptIncorrectNamingAction)
    {
        Guard.AssertNotEmpty(path, nameof(path));

        var innerPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(Directory.GetCurrentDirectory(), path);

        _absoluteDirectoriesPathParsingOptions[innerPath] = new ScriptParsingOptions(
            innerPath,
            scriptIncorrectNamingAction);

        return this;
    }

    /// <summary>
    /// Setup assembly where script migrations embedded.
    /// </summary>
    /// <param name="assembly">Assembly with script migrations files.</param>
    /// <param name="migrationsNamespace">Namespace for script migration embedded files.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public ScriptMigrationsProvider FromAssembly(
        Assembly assembly,
        string migrationsNamespace,
        ScriptIncorrectNamingAction scriptIncorrectNamingAction = DefaultScriptIncorrectNamingAction)
    {
        Guard.AssertNotNull(assembly, nameof(assembly));
        Guard.AssertNotEmpty(migrationsNamespace, nameof(migrationsNamespace));

        _assembliesParsingOptions[assembly] =
            new ScriptParsingOptions($"{migrationsNamespace.TrimEnd('.')}.", scriptIncorrectNamingAction);

        return this;
    }

    /// <inheritdoc />
    public ICollection<IMigration> GetMigrations(
        IMigrationConnection migrationConnection,
        IReadOnlyDictionary<string, string> variables,
        ILogger? migrationLogger)
    {
        Guard.AssertNotNull(migrationConnection, nameof(migrationConnection));
        Guard.AssertNotNull(variables, nameof(variables));

        if (_absoluteDirectoriesPathParsingOptions.Count == 0 && _assembliesParsingOptions.Count == 0)
            throw new InvalidOperationException(
                $"No directories or assemblies specified. First use method {nameof(FromDirectory)} or {nameof(FromAssembly)}");

        var migrations = new List<IMigration>();
        foreach (var keyValuePair in _absoluteDirectoriesPathParsingOptions)
        {
            var directoryPath = keyValuePair.Key;
            var parsingOptions = keyValuePair.Value;

            if (String.IsNullOrEmpty(directoryPath)) throw new ArgumentNullException(nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
                throw new ArgumentException($"Directory \"{directoryPath}\" does not exists");

            var fileNames = Directory.GetFiles(directoryPath);
            Array.Sort(fileNames, StringComparer.Ordinal);

            var directoryMigrations = GetMigrations(
                fileNames,
                File.ReadAllText,
                Path.GetFileName,
                migrationConnection,
                parsingOptions,
                variables,
                migrationLogger);

            if (directoryMigrations.Count == 0) continue;

            migrations.AddRange(directoryMigrations);
        }

        foreach (var keyValuePair in _assembliesParsingOptions)
        {
            var assembly = keyValuePair.Key;
            var scriptParsingOptions = keyValuePair.Value;

            var resourceFileNames = assembly.GetManifestResourceNames();
            Array.Sort(resourceFileNames, StringComparer.Ordinal);

            var assemblyMigrations = GetMigrations(
                resourceFileNames,
                resourceName =>
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null) throw new InvalidOperationException($"Can't open a stream for resource \"{resourceName}\"");

                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                },
                name => name.Replace(scriptParsingOptions.MigrationNamePrefix, String.Empty),
                migrationConnection,
                scriptParsingOptions,
                variables,
                migrationLogger);

            if (assemblyMigrations.Count == 0) continue;

            migrations.AddRange(assemblyMigrations);
        }

        return migrations.OrderBy(x => x.Version).ToArray();
    }

    private ICollection<IMigration> GetMigrations(
        IReadOnlyList<string> fileNames,
        Func<string, string> sqlScriptReadFunc,
        Func<string, string> getCleanedNameFunc,
        IMigrationConnection migrationConnection,
        ScriptParsingOptions scriptParsingOptions,
        IReadOnlyDictionary<string, string> variables,
        ILogger? migrationLogger)
    {
        Guard.AssertNotNull(fileNames, nameof(fileNames));
        Guard.AssertNotNull(sqlScriptReadFunc, nameof(sqlScriptReadFunc));
        Guard.AssertNotNull(getCleanedNameFunc, nameof(getCleanedNameFunc));
        Guard.AssertNotNull(migrationConnection, nameof(migrationConnection));
        Guard.AssertNotNull(variables, nameof(variables));

        var scripts = new Dictionary<MigrationVersion, MigrationScriptInfo>();

        for (var i = 0; i < fileNames.Count; i++)
        {
            var fileName = fileNames[i];

            if (fileName.EndsWith("sql", StringComparison.OrdinalIgnoreCase))
            {
                if (!fileName.StartsWith(scriptParsingOptions.MigrationNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    migrationLogger?.LogTrace($"\"{fileName}\" skipped because of incorrect prefix. Prefix \"{scriptParsingOptions.MigrationNamePrefix}\" is expected");
                    continue;
                }

                var cleanedFileName = getCleanedNameFunc(fileName);
                var match = MigrationFileNameRegex.Match(cleanedFileName);
                if (!match.Success)
                {
                    var message = $"\"{fileName}\" has incorrect name for script migration. File must matches this regex pattern - \"{MigrationConstants.MigrationFileNamePattern}\"";
                    switch (scriptParsingOptions.ScriptIncorrectNamingAction)
                    {
                        case ScriptIncorrectNamingAction.Ignore:
                            migrationLogger?.LogTrace(message);
                            break;
                        case ScriptIncorrectNamingAction.LogToWarn:
                            migrationLogger?.LogWarning(message);
                            break;
                        case ScriptIncorrectNamingAction.LogToError:
                            migrationLogger?.LogError(message);
                            break;
                        case ScriptIncorrectNamingAction.ThrowException:
                            throw new MigrationException(MigrationErrorCode.IncorrectMigrationFileName, message);
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    continue;
                }

                if (!MigrationVersion.TryParse(match.Groups[1].Value, out var version))
                {
                    migrationLogger?.LogWarning($"\"{fileName}\" has incorrect version migration.");
                    continue;
                }

                if (!scripts.TryGetValue(version, out var scriptInfo))
                {
                    scriptInfo = new MigrationScriptInfo();
                    scripts[version] = scriptInfo;
                }

                var script = sqlScriptReadFunc.Invoke(fileName);
                var options = ExtractMigrationOptions(script);

                // split into batches
                var batches = new List<ScriptMigrationBatch>();
                var batchIndex = 0;

                // Use positive lookahead to split script into batches.
                foreach (var batch in BatchSplitRegex.Split(script))
                {
                    if (String.IsNullOrWhiteSpace(batch)) continue;

                    var batchNameMatch = BatchNameRegex.Match(batch);
                    batches.Add(new ScriptMigrationBatch(
                        batchIndex++,
                        batchNameMatch.Success ? batchNameMatch.Groups[1].Value : null,
                        batch));
                }

                if (match.Groups[6].Success)
                {
                    if (scriptInfo.DownScript.Count > 0)
                        throw new InvalidOperationException(
                            $"There is more than one downgrade script with version {version}");

                    scriptInfo.DownScript.AddRange(batches);
                    scriptInfo.DownOptions = options;
                    scriptInfo.DownComment = GetComment(match);
                }
                else
                {
                    if (scriptInfo.UpScript.Count > 0)
                        throw new InvalidOperationException(
                            $"There is more than one upgrade script with version {version}");

                    scriptInfo.UpScript.AddRange(batches);
                    scriptInfo.UpOptions = options;
                    scriptInfo.UpComment = GetComment(match);
                }
            }
            else
            {
                migrationLogger?.LogTrace($"\"{fileName}\" has incorrect extension. \".sql\" is expected");
            }
        }

        return scripts
            .Select(scriptInfo =>
                CreateScriptMigration(
                    scriptInfo.Key,
                    scriptInfo.Value,
                    migrationConnection,
                    variables,
                    migrationLogger))
            .ToArray();
    }

    private static string? GetComment(Match fileNameMatch)
    {
        var comment = fileNameMatch.Groups[9];

        return comment.Success && comment.Value.Length > 0
            ? comment.Value
            : null;
    }

    private static MigrationOptions ExtractMigrationOptions(string sourceScript)
    {
        Guard.AssertNotEmpty(sourceScript, nameof(sourceScript));

        var options = new MigrationOptions();

        foreach (var line in OptionSplitRegex.Split(sourceScript))
        {
            if (String.IsNullOrWhiteSpace(line)) continue;

            var optionsMatch = OptionRegex.Match(line);
            if (!optionsMatch.Success) continue;

            var name = optionsMatch.Groups[1].Value;
            var rawValue = optionsMatch.Groups[2].Value;
            var value = rawValue.Trim().TrimEnd(';').ToUpperInvariant();

            switch (name.ToUpperInvariant())
            {
                case "TRANSACTION":
                    options.IsTransactionRequired = value switch
                    {
                        "ON" => true,
                        "OFF" => false,
                        _ => throw new InvalidOperationException($"Value \"{rawValue}\" is not assignable to the option \"{name}\"")
                    };
                    break;
                case "LONG-RUNNING":
                    options.IsLongRunning = value switch
                    {
                        "TRUE" => true,
                        "FALSE" => false,
                        _ => throw new InvalidOperationException($"Value \"{rawValue}\" is not assignable to the option \"{name}\"")
                    };
                    break;
                case "DEPENDENCIES":
                    if (String.IsNullOrWhiteSpace(value))
                        throw new InvalidOperationException($"Value \"{rawValue}\" is not assignable to the option \"{name}\"");

                    options.Dependencies ??= new List<MigrationVersion>();
                    foreach (var rawVersion in value.Split(','))
                    {
                        if (!MigrationVersion.TryParse(rawVersion.Trim(), out var version))
                            throw new InvalidOperationException($"Can't parse migration dependency \"{rawVersion.Trim()}\"");

                        options.Dependencies.Add(version);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Option \"{name}\" is unknown");
            }
        }

        return options;
    }

    /// <summary>
    /// Creates script migration. Replace variables placeholders with real values
    /// </summary>
    /// <param name="migrationVersion"></param>
    /// <param name="migrationScriptInfo"></param>
    /// <param name="migrationConnection"></param>
    /// <param name="variables"></param>
    /// <param name="migrationLogger"></param>
    /// <returns></returns>
    private IMigration CreateScriptMigration(
        MigrationVersion migrationVersion,
        MigrationScriptInfo migrationScriptInfo,
        IMigrationConnection migrationConnection,
        IReadOnlyDictionary<string, string> variables,
        ILogger? migrationLogger)
    {
        Guard.AssertNotNull(migrationScriptInfo, nameof(migrationScriptInfo));
        Guard.AssertNotNull(migrationConnection, nameof(migrationConnection));
        Guard.AssertNotNull(variables, nameof(variables));

        var upScript = migrationScriptInfo.UpScript;
        var downScript = migrationScriptInfo.DownScript;

        // LONG-RUNNING and DEPENDENCIES describe the whole migration: the upgrade script declares them.
        // TRANSACTION is per direction: the downgrade script inherits the upgrade setting unless it declares its own.
        var upOptions = migrationScriptInfo.UpOptions ?? new MigrationOptions();
        var downOptions = migrationScriptInfo.DownOptions ?? new MigrationOptions();
        if (upOptions.IsLongRunning.HasValue
            && downOptions.IsLongRunning.HasValue
            && upOptions.IsLongRunning != downOptions.IsLongRunning)
        {
            migrationLogger?.LogWarning(
                $"LONG-RUNNING directive of the downgrade script of migration {migrationVersion} differs from the upgrade script " +
                "and is ignored: the upgrade script one applies to the migration");
        }

        if (upOptions.Dependencies != null
            && downOptions.Dependencies != null
            && !upOptions.Dependencies.ToHashSet().SetEquals(downOptions.Dependencies))
        {
            migrationLogger?.LogWarning(
                $"DEPENDENCIES directive of the downgrade script of migration {migrationVersion} differs from the upgrade script " +
                "and is ignored: the upgrade script one applies to the migration");
        }

        var isTransactionRequired = upOptions.IsTransactionRequired ?? true;
        var isLongRunning = upOptions.IsLongRunning ?? downOptions.IsLongRunning ?? false;
        var dependencies = upOptions.Dependencies ?? downOptions.Dependencies ?? new List<MigrationVersion>();

        var comment = migrationScriptInfo.UpComment ?? migrationScriptInfo.DownComment;

        foreach (var keyValuePair in variables)
        {
            foreach (var batch in upScript)
            {
                batch.Script = batch.Script.Replace(keyValuePair.Key, keyValuePair.Value);
            }

            foreach (var batch in downScript)
            {
                batch.Script = batch.Script.Replace(keyValuePair.Key, keyValuePair.Value);
            }
        }

        return downScript.Count > 0
            ? new DowngradeScriptMigration(
                migrationLogger,
                migrationConnection,
                migrationVersion,
                upScript,
                downScript,
                comment,
                isTransactionRequired,
                isLongRunning,
                dependencies)
            {
                IsDowngradeTransactionRequired = downOptions.IsTransactionRequired ?? isTransactionRequired
            }
            : new ScriptMigration(
                migrationLogger,
                migrationConnection,
                migrationVersion,
                upScript,
                comment,
                isTransactionRequired,
                isLongRunning,
                dependencies);
    }

    private struct ScriptParsingOptions
    {
        /// <summary>
        /// Directory name with migration files or namespace of folder with migration embedded resources. 
        /// </summary>
        public string MigrationNamePrefix { get; }

        public ScriptIncorrectNamingAction ScriptIncorrectNamingAction { get; }

        internal ScriptParsingOptions(
            string migrationNamePrefix,
            ScriptIncorrectNamingAction scriptIncorrectNamingAction)
        {
            MigrationNamePrefix = migrationNamePrefix;
            ScriptIncorrectNamingAction = scriptIncorrectNamingAction;
        }
    }

    /// <summary>
    /// Internal class for analysis sql script files
    /// </summary>
    private class MigrationScriptInfo
    {
        public string? UpComment { get; set; }

        public string? DownComment { get; set; }

        public List<ScriptMigrationBatch> UpScript { get; } = new();

        public List<ScriptMigrationBatch> DownScript { get; } = new();

        public MigrationOptions? UpOptions { get; set; }

        public MigrationOptions? DownOptions { get; set; }
    }

    /// <summary>
    /// Directives of one script; <see langword="null"/> when the script doesn't declare the directive.
    /// </summary>
    private class MigrationOptions
    {
        public bool? IsTransactionRequired { get; set; }

        public bool? IsLongRunning { get; set; }

        public List<MigrationVersion>? Dependencies { get; set; }
    }
}
