namespace Hospitality.Api.Common;

public static class HttpContextExtensions
{
    public const string CorrelationItemKey = "CorrelationId";
    public const string CorrelationHeader = "X-Correlation-Id";

    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationItemKey, out var value) && value is string id)
        {
            return id;
        }

        return $"req-{Guid.NewGuid():N}";
    }
}
