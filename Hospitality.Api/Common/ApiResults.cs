namespace Hospitality.Api.Common;

public static class ApiResults
{
    public static IResult Error(HttpContext context, int statusCode, string code, string message)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorBody(code, message, context.GetCorrelationId())),
            statusCode: statusCode);
    }

    public static IResult Unauthorized(HttpContext context, string code = "UNAUTHORIZED", string message = "Authentication is required.")
        => Error(context, StatusCodes.Status401Unauthorized, code, message);

    public static IResult Forbidden(HttpContext context)
        => Error(context, StatusCodes.Status403Forbidden, "FORBIDDEN", "You do not have permission to perform this action.");

    public static IResult Conflict(HttpContext context, string code, string message)
        => Error(context, StatusCodes.Status409Conflict, code, message);

    public static IResult BadRequest(HttpContext context, string message)
        => Error(context, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", message);
}
