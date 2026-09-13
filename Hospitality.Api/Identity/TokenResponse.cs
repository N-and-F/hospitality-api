using System.Text.Json.Serialization;

namespace Hospitality.Api.Identity;

public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType = "Bearer");
