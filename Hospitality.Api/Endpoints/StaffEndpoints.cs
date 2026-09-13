using Hospitality.Api.Common;
using Hospitality.Api.Identity;

namespace Hospitality.Api.Endpoints;

public static class StaffEndpoints
{
    public static RouteGroupBuilder MapStaffEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/staff", async (UserService users) =>
        {
            var items = await users.ListStaffAsync();
            return Results.Ok(new StaffListResponse(items));
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
        .WithName("ListStaff")
        .WithTags("Staff");

        group.MapPost("/staff", async (
            RegisterRequest request,
            UserService users,
            HttpContext http) =>
        {
            var result = await users.CreateStaffAsync(request);
            return result.User is not null
                ? Results.Ok(result.User)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
        .WithName("CreateStaff")
        .WithTags("Staff");

        return group;
    }
}
