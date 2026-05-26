using HygieneAudit.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace HygieneAudit.API.Filters;

public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationEx)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                new { message = validationEx.Message }, cancellationToken);
            return true;
        }

        if (exception is NotFoundException notFoundEx)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await httpContext.Response.WriteAsJsonAsync(
                new { message = notFoundEx.Message }, cancellationToken);
            return true;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new { message = "Internal server error." }, cancellationToken);
        return true;
    }
}
