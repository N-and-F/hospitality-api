using System.Security.Claims;
using Hospitality.Api.Common;
using Hospitality.Api.Identity;

namespace Hospitality.Api.Endpoints;

public static class CurrentUserEndpoints
{
    public static RouteGroupBuilder MapCurrentUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/users/current", async (ClaimsPrincipal principal, UserService users, HttpContext http) =>
        {
            var id = principal.FindFirstValue("sub")
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(id))
            {
                return ApiResults.Unauthorized(http);
            }

            var currentUser = await users.GetByIdAsync(id);
            return currentUser is null ? ApiResults.Unauthorized(http) : Results.Ok(currentUser);
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .WithTags("Auth");

        group.MapPatch("/users/{id}/role", async (
            string id,
            SetRoleRequest request,
            ClaimsPrincipal principal,
            UserService users,
            HttpContext http) =>
        {
            var actorId = principal.FindFirstValue("sub")
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(actorId))
            {
                return ApiResults.Unauthorized(http);
            }

            var result = await users.SetRoleAsync(id, request.Role, actorId);
            return result.User is not null
                ? Results.Ok(result.User)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithName("SetUserRole")
            .WithTags("Staff");

        group.MapDelete("/users/{id}", async (
            string id,
            ClaimsPrincipal principal,
            UserService users,
            HttpContext http) =>
        {
            var actorId = principal.FindFirstValue("sub")
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(actorId))
            {
                return ApiResults.Unauthorized(http);
            }

            var result = await users.DeleteAsync(id, actorId);
            return result.Code is null
                ? Results.Ok()
                : ApiResults.Error(http, result.Status, result.Code, result.Message!);
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
        .WithName("DeleteUser")
        .WithTags("Staff");

        return group;
    }
}
