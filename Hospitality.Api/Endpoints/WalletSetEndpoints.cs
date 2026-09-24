using System.Security.Claims;
using Hospitality.Api.Common;
using Hospitality.Api.Identity;
using Hospitality.Api.Ledger;

namespace Hospitality.Api.Endpoints;

public static class WalletSetEndpoints
{
    public static RouteGroupBuilder MapWalletSetEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/wallet-sets", async (
                WalletSetService wallets,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var userId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var items = await wallets.ListAsync(userId, user.IsInRole(Roles.Admin), cancellationToken);
                return Results.Ok(new WalletSetListResponse(items));
            })
            .RequireAuthorization()
            .WithName("ListWalletSets")
            .WithTags("Ledger");

        group.MapPost("/wallet-sets", async (
                CreateWalletSetRequest request,
                WalletSetService wallets,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await wallets.CreateAsync(request.Name, cancellationToken);
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithName("CreateWalletSet")
            .WithTags("Ledger");

        group.MapPut("/wallet-sets/{id:guid}", async (
                Guid id,
                RenameWalletSetRequest request,
                WalletSetService wallets,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await wallets.RenameAsync(id, request.Name, cancellationToken);
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithName("RenameWalletSet")
            .WithTags("Ledger");

        group.MapDelete("/wallet-sets/{id:guid}", async (
                Guid id,
                WalletSetService wallets,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await wallets.DeleteAsync(id, cancellationToken);
                return result.Code is null
                    ? Results.Ok()
                    : ApiResults.Error(http, result.Status, result.Code, result.Message!);
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithName("DeleteWalletSet")
            .WithTags("Ledger");

        return group;
    }
}
