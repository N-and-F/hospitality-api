using System.Security.Claims;
using Hospitality.Api.Common;
using Hospitality.Api.Felogan;
using Hospitality.Api.Identity;

namespace Hospitality.Api.Endpoints;

public static class FeloganEndpoints
{
    public static RouteGroupBuilder MapFeloganEndpoints(this RouteGroupBuilder group)
    {
        var felogan = group.MapGroup("/felogan")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithTags("Felogan");

        felogan.MapGet("/rooms", async (
            bool? includeArchived,
            FeloganService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListRoomsAsync(includeArchived == true, cancellationToken)))
            .WithName("ListFeloganRooms");

        felogan.MapPost("/rooms", async (
            CatalogItemRequest request,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateRoomAsync(request, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("CreateFeloganRoom");

        felogan.MapPut("/rooms/{id:guid}", async (
            Guid id,
            CatalogItemRequest request,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRoomAsync(id, request, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("UpdateFeloganRoom");

        felogan.MapPost("/rooms/{id:guid}/archive", async (
            Guid id,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ArchiveRoomAsync(id, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("ArchiveFeloganRoom");

        felogan.MapPost("/rooms/{id:guid}/unarchive", async (
            Guid id,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UnarchiveRoomAsync(id, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("UnarchiveFeloganRoom");

        felogan.MapGet("/cooks", async (
            bool? includeArchived,
            FeloganService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListCooksAsync(includeArchived == true, cancellationToken)))
            .WithName("ListFeloganCooks");

        felogan.MapPost("/cooks", async (
            CatalogItemRequest request,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCookAsync(request, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("CreateFeloganCook");

        felogan.MapPut("/cooks/{id:guid}", async (
            Guid id,
            CatalogItemRequest request,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateCookAsync(id, request, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("UpdateFeloganCook");

        felogan.MapPost("/cooks/{id:guid}/archive", async (
            Guid id,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ArchiveCookAsync(id, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("ArchiveFeloganCook");

        felogan.MapPost("/cooks/{id:guid}/unarchive", async (
            Guid id,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UnarchiveCookAsync(id, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("UnarchiveFeloganCook");

        felogan.MapGet("/menu-items", async (
            bool? includeArchived,
            FeloganService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListMenuItemsAsync(includeArchived == true, cancellationToken)))
            .WithName("ListFeloganMenuItems");

        felogan.MapPost("/menu-items", async (
            MenuItemRequest request,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateMenuItemAsync(request, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("CreateFeloganMenuItem");

        felogan.MapPut("/menu-items/{id:guid}", async (
            Guid id,
            MenuItemRequest request,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateMenuItemAsync(id, request, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("UpdateFeloganMenuItem");

        felogan.MapPost("/menu-items/{id:guid}/archive", async (
            Guid id,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ArchiveMenuItemAsync(id, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("ArchiveFeloganMenuItem");

        felogan.MapPost("/menu-items/{id:guid}/unarchive", async (
            Guid id,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UnarchiveMenuItemAsync(id, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("UnarchiveFeloganMenuItem");

        felogan.MapGet("/servings", async (
            string? date,
            string? range,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListServingsAsync(date, range, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("ListFeloganServings");

        felogan.MapPost("/servings", async (
            CreateServingRequest request,
            FeloganService service,
            ClaimsPrincipal user,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var userId = UserId(user);
            if (userId is null)
            {
                return ApiResults.Unauthorized(http);
            }

            var result = await service.CreateServingAsync(request, userId, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("CreateFeloganServing");

        felogan.MapPut("/servings/{id:guid}", async (
            Guid id,
            UpdateServingRequest request,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateServingAsync(id, request, cancellationToken);
            return result.Value is not null
                ? Results.Ok(result.Value)
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("UpdateFeloganServing");

        felogan.MapDelete("/servings/{id:guid}", async (
            Guid id,
            FeloganService service,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteServingAsync(id, cancellationToken);
            return result.Id is not null
                ? Results.Ok(new { id = result.Id.Value })
                : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
        })
            .WithName("DeleteFeloganServing");

        return group;
    }

    private static string? UserId(ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
}
