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

        return group;
    }
}
