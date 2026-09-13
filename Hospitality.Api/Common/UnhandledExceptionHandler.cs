using Microsoft.AspNetCore.Diagnostics;

namespace Hospitality.Api.Common;

public sealed class UnhandledExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorEnvelope(new ErrorBody(
                "INTERNAL_ERROR",
                "An unexpected error occurred.",
                httpContext.GetCorrelationId())),
            cancellationToken);
        return true;
    }
}
