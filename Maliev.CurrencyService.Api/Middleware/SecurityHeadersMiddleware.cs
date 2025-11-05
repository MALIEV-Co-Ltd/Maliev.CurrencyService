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

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

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
        headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;";

        // Strict Transport Security - enforce HTTPS (1 year)
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Referrer Policy - only send origin for same-origin requests
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions Policy - restrict browser features
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
