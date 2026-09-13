using System.Security.Claims;
using Hospitality.Api.Common;
using Hospitality.Api.Identity;
using Hospitality.Api.Ledger;

namespace Hospitality.Api.Endpoints;

public static class LedgerEndpoints
{
    public static RouteGroupBuilder MapLedgerEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/wallets", async (LedgerService ledger) => Results.Ok(await ledger.GetWalletsAsync()))
            .RequireAuthorization()
            .WithName("GetWallets")
            .WithTags("Ledger");

        group.MapPut("/wallets/opening", async (
                SetOpeningRequest request,
                LedgerService ledger,
                ClaimsPrincipal user,
                HttpContext http) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var result = await ledger.SetOpeningAsync(request, userId);
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithName("SetWalletOpening")
            .WithTags("Ledger");

        group.MapGet("/transaction-methods", (LedgerService ledger) => Results.Ok(ledger.ListMethods()))
            .RequireAuthorization()
            .WithName("ListTransactionMethods")
            .WithTags("Ledger");

        group.MapGet("/transactions", async (
                string? date,
                string? range,
                bool? includeVoided,
                LedgerService ledger,
                ClaimsPrincipal user,
                HttpContext http) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var isAdmin = user.IsInRole(Roles.Admin);
                var result = await ledger.ListAsync(date, range, userId, isAdmin, includeVoided == true && isAdmin);
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization()
            .WithName("ListTransactions")
            .WithTags("Ledger");

        group.MapPost("/transactions", async (
                CreateTransactionRequest request,
                LedgerService ledger,
                ClaimsPrincipal user,
                HttpContext http) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var result = await ledger.CreateAsync(request, userId);
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization()
            .WithName("CreateTransaction")
            .WithTags("Ledger");

        group.MapPut("/transactions/{id:guid}", async (
                Guid id,
                UpdateTransactionRequest request,
                LedgerService ledger,
                ClaimsPrincipal user,
                HttpContext http) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var result = await ledger.UpdateAsync(id, request, userId, user.IsInRole(Roles.Admin));
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization()
            .WithName("UpdateTransaction")
            .WithTags("Ledger");

        group.MapDelete("/transactions/{id:guid}", async (
                Guid id,
                LedgerService ledger,
                ClaimsPrincipal user,
                HttpContext http) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var result = await ledger.DeleteAsync(id, userId, user.IsInRole(Roles.Admin));
                if (result.Code is not null)
                {
                    return ApiResults.Error(http, result.Status, result.Code, result.Message!);
                }

                return Results.Ok(await ledger.GetWalletsAsync());
            })
            .RequireAuthorization()
            .WithName("DeleteTransaction")
            .WithTags("Ledger");

        group.MapPost("/transactions/{id:guid}/void", async (
                Guid id,
                LedgerService ledger,
                ClaimsPrincipal user,
                HttpContext http) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var result = await ledger.VoidAsync(id, userId);
                if (result.Code is not null)
                {
                    return ApiResults.Error(http, result.Status, result.Code, result.Message!);
                }

                return Results.Ok(await ledger.GetWalletsAsync());
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithName("VoidTransaction")
            .WithTags("Ledger");

        return group;
    }

    private static string? UserId(ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
}
