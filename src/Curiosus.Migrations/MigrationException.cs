using System;

namespace Curiosus.Migrations;

/// <summary>
/// Exception occured during migration
/// </summary>
/// <remarks>
/// Throw it from a custom <see cref="IMigrationConnection"/>, migrations provider or migration to fail the migration with
/// a specific <see cref="MigrationErrorCode"/>: the engine returns it in <see cref="MigrationResult.ErrorCode"/>.
/// </remarks>
public class MigrationException : Exception
{
    /// <summary>
    /// Code of migration error
    /// </summary>
    public MigrationErrorCode ErrorCode { get; }

    /// <summary>
    /// Migration that resulted in the exception.
    /// </summary>
    public MigrationInfo? MigrationInfo { get; }

    /// <summary>
    /// Name of database where exception was thrown.
    /// </summary>
    public string? DatabaseName { get; }
    
    /// <summary>
    /// Creates an exception that fails the migration with <paramref name="errorCode"/>.
    /// </summary>
    /// <param name="errorCode">Code returned in <see cref="MigrationResult.ErrorCode"/>.</param>
    /// <param name="message">Message returned in <see cref="MigrationResult.ErrorMessage"/>.</param>
    public MigrationException(MigrationErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates an exception that fails the migration with <paramref name="errorCode"/> because of
    /// <paramref name="innerException"/>.
    /// </summary>
    /// <param name="errorCode">Code returned in <see cref="MigrationResult.ErrorCode"/>.</param>
    /// <param name="message">Message returned in <see cref="MigrationResult.ErrorMessage"/>.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    public MigrationException(MigrationErrorCode errorCode, string message, Exception? innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    internal MigrationException(
        MigrationErrorCode errorCode,
        string message,
        string? databaseName,
        MigrationInfo? migrationInfo = null) : base(message)
    {
        DatabaseName = databaseName;
        ErrorCode = errorCode;
        MigrationInfo = migrationInfo;
    }

    internal MigrationException(
        MigrationErrorCode errorCode,
        string message,
        Exception innerException,
        string? databaseName,
        MigrationInfo? migrationInfo = null) : base(message, innerException)
    {
        DatabaseName = databaseName;
        ErrorCode = errorCode;
        MigrationInfo = migrationInfo;
    }
}
