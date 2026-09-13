namespace Hospitality.Api.Common;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HttpContextExtensions.CorrelationHeader].FirstOrDefault();
        var id = string.IsNullOrWhiteSpace(incoming) ? $"req-{Guid.NewGuid():N}" : incoming;
        context.Items[HttpContextExtensions.CorrelationItemKey] = id;
        context.Response.Headers[HttpContextExtensions.CorrelationHeader] = id;
        await next(context);
    }
}
