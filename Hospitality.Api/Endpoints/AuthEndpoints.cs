using Hospitality.Api.Common;
using Hospitality.Api.Identity;

namespace Hospitality.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/register", async (RegisterRequest request, UserService users, HttpContext http) =>
        {
            var result = await users.RegisterAsync(request);
            return result.Token is not null
                ? Results.Ok(result.Token)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
        .AllowAnonymous()
        .WithName("Register")
        .WithTags("Auth");

        group.MapPost("/auth/login", async (LoginRequest request, UserService users, HttpContext http) =>
        {
            var result = await users.LoginAsync(request);
            return result.Token is not null
                ? Results.Ok(result.Token)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithTags("Auth");

        return group;
    }
}
