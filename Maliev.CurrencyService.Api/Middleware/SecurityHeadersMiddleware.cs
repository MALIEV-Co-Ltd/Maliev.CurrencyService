namespace Maliev.CurrencyService.Api.Middleware;

/// <summary>
/// Middleware to add security headers to all responses
/// </summary>
/// <remarks>
/// Per Constitution Principle VI: Adds security headers for protection against
/// common web vulnerabilities (XSS, clickjacking, MIME sniffing, etc.)
/// </remarks>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the middleware to add security headers to the response.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A <see cref="Task"/> that represents the execution of this middleware.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context);

        await _next(context);
    }

    private static void AddSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent clickjacking attacks
        headers["X-Frame-Options"] = "DENY";

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Enable XSS protection
        headers["X-XSS-Protection"] = "1; mode=block";

        // Content Security Policy - restrictive default
        headers["Content-Security-Security"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;";

        // Strict Transport Security - enforce HTTPS (1 year)
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Referrer Policy - only send origin for same-origin requests
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions Policy - restrict browser features
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    }
}

/// <summary>
/// Extension methods for adding the <see cref="SecurityHeadersMiddleware"/> to the application pipeline.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="SecurityHeadersMiddleware"/> to the application's request pipeline.
    /// </summary>
    /// <param name="builder">The <see cref="IApplicationBuilder"/>.</param>
    /// <returns>The configured <see cref="IApplicationBuilder"/>.</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
