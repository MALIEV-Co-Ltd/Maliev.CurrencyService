using Maliev.CurrencyService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace Maliev.CurrencyService.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Database metrics interceptor for observability and performance monitoring.
/// </summary>
/// <remarks>
/// Captures database command execution metrics including:
/// - Query execution time
/// - Command type (SELECT, INSERT, UPDATE, DELETE)
/// - Error tracking
/// Used for Prometheus metrics and performance optimization.
/// </remarks>
public class DatabaseMetricsInterceptor : DbCommandInterceptor
{
    private readonly ILogger<DatabaseMetricsInterceptor> _logger;
    private readonly IDatabaseMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseMetricsInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger for the interceptor.</param>
    /// <param name="metrics">The metrics recorder.</param>
    public DatabaseMetricsInterceptor(
        ILogger<DatabaseMetricsInterceptor> logger = null!,
        IDatabaseMetrics metrics = null!)
    {
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Intercepts the execution of a <see cref="DbCommand"/> that is expected to return a <see cref="DbDataReader"/> asynchronously.
    /// </summary>
    /// <param name="command">The command being executed.</param>
    /// <param name="eventData">Contextual data associated with a command being executed.</param>
    /// <param name="result">The result of the command execution.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="ValueTask{DbDataReader}"/> representing the asynchronous interception.</returns>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecution(command, eventData.Duration);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts the execution of a <see cref="DbCommand"/> that is expected to return a <see cref="DbDataReader"/>.
    /// </summary>
    /// <param name="command">The command being executed.</param>
    /// <param name="eventData">Contextual data associated with a command being executed.</param>
    /// <param name="result">The result of the command execution.</param>
    /// <returns>The <see cref="DbDataReader"/> result.</returns>
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogCommandExecution(command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    /// <summary>
    /// Intercepts the execution of a <see cref="DbCommand"/> that is expected to not return a result asynchronously.
    /// </summary>
    /// <param name="command">The command being executed.</param>
    /// <param name="eventData">Contextual data associated with a command being executed.</param>
    /// <param name="result">The result of the command execution.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="ValueTask{Int32}"/> representing the asynchronous interception.</returns>
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecution(command, eventData.Duration);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Intercepts the execution of a <see cref="DbCommand"/> that is expected to not return a result.
    /// </summary>
    /// <param name="command">The command being executed.</param>
    /// <param name="eventData">Contextual data associated with a command being executed.</param>
    /// <param name="result">The result of the command execution.</param>
    /// <returns>The integer result.</returns>
    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogCommandExecution(command, eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }

    /// <summary>
    /// Intercepts the failure of a <see cref="DbCommand"/> execution.
    /// </summary>
    /// <param name="command">The command being executed.</param>
    /// <param name="eventData">Contextual data associated with a command execution failure.</param>
    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        if (IsMigrationHistoryCheckFailure(command, eventData.Exception))
        {
            base.CommandFailed(command, eventData);
            return;
        }

        _logger.LogError(
            eventData.Exception,
            "Database command failed after {Duration}ms. CommandType: {CommandType}, SQL: {CommandText}",
            eventData.Duration.TotalMilliseconds,
            command.CommandType,
            command.CommandText);

        _metrics?.RecordDatabaseError(GetCommandTypeFromSql(command.CommandText), eventData.Exception.GetType().Name);
        base.CommandFailed(command, eventData);
    }

    /// <summary>
    /// Intercepts the asynchronous failure of a <see cref="DbCommand"/> execution.
    /// </summary>
    /// <param name="command">The command being executed.</param>
    /// <param name="eventData">Contextual data associated with a command execution failure.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous interception.</returns>
    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (IsMigrationHistoryCheckFailure(command, eventData.Exception))
        {
            return base.CommandFailedAsync(command, eventData, cancellationToken);
        }

        _logger.LogError(
            eventData.Exception,
            "Database command failed after {Duration}ms. CommandType: {CommandType}, SQL: {CommandText}",
            eventData.Duration.TotalMilliseconds,
            command.CommandType,
            command.CommandText);

        _metrics?.RecordDatabaseError(GetCommandTypeFromSql(command.CommandText), eventData.Exception.GetType().Name);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private static bool IsMigrationHistoryCheckFailure(DbCommand command, Exception exception)
    {
        if (exception is Npgsql.PostgresException pgEx &&
            pgEx.SqlState == "42P01" &&
            command.CommandText.Contains("__EFMigrationsHistory"))
        {
            return true;
        }
        return false;
    }

    private void LogCommandExecution(DbCommand command, TimeSpan duration)
    {
        var commandType = GetCommandTypeFromSql(command.CommandText);

        _metrics?.RecordDatabaseQuery(commandType);
        _metrics?.RecordDatabaseQueryDuration(commandType, duration.TotalSeconds);

        var slowThreshold = commandType == "OTHER" ? 500 : 100;
        if (duration.TotalMilliseconds > slowThreshold && command.CommandText != "SELECT 1")
        {
            _logger.LogWarning(
               "Slow database query detected: {Duration}ms, Type: {CommandType}, SQL: {CommandText}",
               duration.TotalMilliseconds,
               commandType,
               command.CommandText);
        }
        else if (duration.TotalMilliseconds > 50)
        {
            _logger.LogInformation(
               "Database query: {Duration}ms, Type: {CommandType}",
               duration.TotalMilliseconds,
               commandType);
        }
    }

    private static string GetCommandTypeFromSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "UNKNOWN";

        var firstWord = sql.TrimStart().Split(' ')[0].ToUpperInvariant();
        return firstWord switch
        {
            "SELECT" => "SELECT",
            "INSERT" => "INSERT",
            "UPDATE" => "UPDATE",
            "DELETE" => "DELETE",
            _ => "OTHER"
        };
    }
}
