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

    public DatabaseMetricsInterceptor(ILogger<DatabaseMetricsInterceptor> logger)
    {
        _logger = logger;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecution(command, eventData.Duration);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogCommandExecution(command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecution(command, eventData.Duration);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogCommandExecution(command, eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }

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
