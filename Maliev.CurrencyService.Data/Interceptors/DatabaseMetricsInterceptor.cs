using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace Maliev.CurrencyService.Data.Interceptors;

/// <summary>
/// Database metrics interceptor for observability and performance monitoring
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseMetricsInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger for the interceptor.</param>
    public DatabaseMetricsInterceptor(ILogger<DatabaseMetricsInterceptor> logger)
    {
        _logger = logger;
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
        _logger.LogError(
            eventData.Exception,
            "Database command failed after {Duration}ms. CommandType: {CommandType}, SQL: {CommandText}",
            eventData.Duration.TotalMilliseconds,
            command.CommandType,
            command.CommandText);

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
        _logger.LogError(
            eventData.Exception,
            "Database command failed after {Duration}ms. CommandType: {CommandType}, SQL: {CommandText}",
            eventData.Duration.TotalMilliseconds,
            command.CommandType,
            command.CommandText);

        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void LogCommandExecution(DbCommand command, TimeSpan duration)
    {
        var commandType = GetCommandTypeFromSql(command.CommandText);

        // Log slow queries (> 100ms threshold)
        if (duration.TotalMilliseconds > 100)
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

        // TODO: Future enhancement - Record metrics to Prometheus counters:
        // - database_queries_total{type=SELECT|INSERT|UPDATE|DELETE}
        // - database_query_duration_seconds{type, percentile}
        // - database_errors_total{type, error_code}
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
