using System.Net;
using System.Text.Json;

namespace HygieneAudit.API.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;");
        context.Response.Headers.Append("Permissions-Policy", 
            "camera=(), microphone=(), geolocation=()");
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

        // Remove server header
        context.Response.Headers.Remove("Server");

        await _next(context);
    }
}

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            StatusCode = context.Response.StatusCode,
            Message = "An internal server error occurred. Please try again later.",
            // Don't expose exception details in production
            Detail = context.RequestServices.GetService<IWebHostEnvironment>()?.EnvironmentName == "Development" 
                ? exception.Message 
                : null
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User.Identity?.Name ?? "anonymous";
        var path = context.Request.Path;
        var method = context.Request.Method;

        _logger.LogInformation("Request: {Method} {Path} by {User} at {Time}", 
            method, path, user, DateTime.UtcNow);

        await _next(context);

        _logger.LogInformation("Response: {Method} {Path} - Status {StatusCode} for {User}",
            method, path, context.Response.StatusCode, user);
    }
}
