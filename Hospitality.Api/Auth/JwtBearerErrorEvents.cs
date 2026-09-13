using Hospitality.Api.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Hospitality.Api.Auth;

public static class JwtBearerErrorEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new ErrorEnvelope(new ErrorBody(
                    "UNAUTHORIZED",
                    "Authentication is required.",
                    context.HttpContext.GetCorrelationId())));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ErrorEnvelope(new ErrorBody(
                    "FORBIDDEN",
                    "You do not have permission to perform this action.",
                    context.HttpContext.GetCorrelationId())));
        }
    };
}
