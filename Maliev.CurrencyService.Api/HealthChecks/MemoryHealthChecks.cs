using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace Maliev.CurrencyService.Api.HealthChecks;

public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder AddPrivateMemoryHealthCheck(
        this IHealthChecksBuilder builder,
        long thresholdBytes,
        string name,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck(name, new PrivateMemoryHealthCheck(thresholdBytes), tags: tags ?? Enumerable.Empty<string>());
    }

    public static IHealthChecksBuilder AddWorkingSetHealthCheck(
        this IHealthChecksBuilder builder,
        long thresholdBytes,
        string name,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck(name, new WorkingSetHealthCheck(thresholdBytes), tags: tags ?? Enumerable.Empty<string>());
    }
}

public class PrivateMemoryHealthCheck : IHealthCheck
{
    private readonly long _thresholdBytes;

    public PrivateMemoryHealthCheck(long thresholdBytes)
    {
        _thresholdBytes = thresholdBytes;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var privateMemorySize = process.PrivateMemorySize64;

            // Convert to MB for readability
            var privateMemoryMB = privateMemorySize / 1024 / 1024;
            var thresholdMB = _thresholdBytes / 1024 / 1024;

            // Log the current memory usage for debugging
            // In a real implementation, you might want to use a logger here
            System.Diagnostics.Debug.WriteLine($"Private memory usage: {privateMemoryMB} MB, Threshold: {thresholdMB} MB");

            if (privateMemorySize > _thresholdBytes)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Private memory usage is {privateMemoryMB} MB, which exceeds the threshold of {thresholdMB} MB"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Private memory usage is {privateMemoryMB} MB, which is below the threshold of {thresholdMB} MB"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Error checking private memory: {ex.Message}", ex));
        }
    }
}

public class WorkingSetHealthCheck : IHealthCheck
{
    private readonly long _thresholdBytes;

    public WorkingSetHealthCheck(long thresholdBytes)
    {
        _thresholdBytes = thresholdBytes;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;

            // Convert to MB for readability
            var workingSetMB = workingSet / 1024 / 1024;
            var thresholdMB = _thresholdBytes / 1024 / 1024;

            // Log the current memory usage for debugging
            // In a real implementation, you might want to use a logger here
            System.Diagnostics.Debug.WriteLine($"Working set memory usage: {workingSetMB} MB, Threshold: {thresholdMB} MB");

            if (workingSet > _thresholdBytes)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Working set memory usage is {workingSetMB} MB, which exceeds the threshold of {thresholdMB} MB"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Working set memory usage is {workingSetMB} MB, which is below the threshold of {thresholdMB} MB"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Error checking working set memory: {ex.Message}", ex));
        }
    }
}