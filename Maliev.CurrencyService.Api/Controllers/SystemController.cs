using Maliev.Aspire.ServiceDefaults.Authorization;
using Asp.Versioning;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Maliev.CurrencyService.Api.Controllers;

/// <summary>System management and diagnostics endpoints.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("currency/v{version:apiVersion}/system")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("AuthenticatedApi")]
public class SystemController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly ILogger<SystemController> _logger;

    /// <summary>Initializes a new instance of the <see cref="SystemController"/> class.</summary>
    public SystemController(
        ICacheService cacheService,
        CurrencyServiceMetrics metrics,
        ILogger<SystemController> logger)
    {
        _cacheService = cacheService;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Rebuild all currency and rate caches (admin only)
    /// </summary>
    [HttpPost("rebuild-cache")]
    [RequirePermission(CurrencyPermissions.SystemRebuildCache)]
    public async Task<IActionResult> RebuildCache(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin triggered system cache rebuild");
        
        // Invalidate all caches
        await _cacheService.RemoveByPatternAsync("currency:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("rate:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync("snapshot:*", cancellationToken);
        
        return Ok(new { message = "Cache rebuild triggered successfully" });
    }

    /// <summary>
    /// View service statistics and metrics (admin only)
    /// </summary>
    [HttpGet("stats")]
    [RequirePermission(CurrencyPermissions.SystemViewStats)]
    public IActionResult GetStats()
    {
        _logger.LogInformation("Admin viewed system statistics");
        
        // This is a stub for returning high-level stats. 
        // Real metrics are exposed via the /metrics endpoint.
        return Ok(new
        {
            service = "CurrencyService",
            version = "1.0",
            status = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}