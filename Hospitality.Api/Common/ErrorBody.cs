using System.Text.Json.Serialization;

namespace Hospitality.Api.Common;

public sealed record ErrorEnvelope(ErrorBody Error);

public sealed record ErrorBody(
    string Code,
    string Message,
    [property: JsonPropertyName("correlation_id")] string CorrelationId);
