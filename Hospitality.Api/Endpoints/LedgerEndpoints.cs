using System.Security.Claims;
using Hospitality.Api.Common;
using Hospitality.Api.Identity;
using Hospitality.Api.Ledger;

namespace Hospitality.Api.Endpoints;

public static class LedgerEndpoints
{
    public static RouteGroupBuilder MapLedgerEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/wallets", async (
                Guid? walletId,
                LedgerService ledger,
                WalletSetService wallets,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var resolved = await ResolveWallet(walletId, user, wallets, http, cancellationToken);
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                return Results.Ok(await ledger.GetWalletsAsync(resolved.WalletId, cancellationToken));
            })
            .RequireAuthorization()
            .WithName("GetWallets")
            .WithTags("Ledger");

        group.MapPut("/wallets/opening", async (
                Guid? walletId,
                SetOpeningRequest request,
                LedgerService ledger,
                WalletSetService wallets,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var resolved = await ResolveWallet(walletId, user, wallets, http, cancellationToken);
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                var result = await ledger.SetOpeningAsync(request, userId, resolved.WalletId, cancellationToken);
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
                Guid? walletId,
                string? date,
                string? range,
                bool? includeVoided,
                LedgerService ledger,
                WalletSetService wallets,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var resolved = await ResolveWallet(walletId, user, wallets, http, cancellationToken);
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                var isAdmin = user.IsInRole(Roles.Admin);
                var result = await ledger.ListAsync(
                    date,
                    range,
                    userId,
                    isAdmin,
                    includeVoided == true && isAdmin,
                    resolved.WalletId,
                    cancellationToken);
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization()
            .WithName("ListTransactions")
            .WithTags("Ledger");

        group.MapPost("/transactions", async (
                Guid? walletId,
                CreateTransactionRequest request,
                LedgerService ledger,
                WalletSetService wallets,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var resolved = await ResolveWallet(walletId, user, wallets, http, cancellationToken);
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                var result = await ledger.CreateAsync(request, userId, resolved.WalletId, cancellationToken);
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization()
            .WithName("CreateTransaction")
            .WithTags("Ledger");

        group.MapPut("/transactions/{id:guid}", async (
                Guid id,
                Guid? walletId,
                UpdateTransactionRequest request,
                LedgerService ledger,
                WalletSetService wallets,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var resolved = await ResolveWallet(walletId, user, wallets, http, cancellationToken);
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                var result = await ledger.UpdateAsync(id, request, userId, user.IsInRole(Roles.Admin), resolved.WalletId, cancellationToken);
                return result.Value is not null
                    ? Results.Ok(result.Value)
                    : ApiResults.Error(http, result.Status, result.Code!, result.Message!);
            })
            .RequireAuthorization()
            .WithName("UpdateTransaction")
            .WithTags("Ledger");

        group.MapDelete("/transactions/{id:guid}", async (
                Guid id,
                Guid? walletId,
                LedgerService ledger,
                WalletSetService wallets,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var resolved = await ResolveWallet(walletId, user, wallets, http, cancellationToken);
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                var result = await ledger.DeleteAsync(id, userId, user.IsInRole(Roles.Admin), resolved.WalletId, cancellationToken);
                if (result.Code is not null)
                {
                    return ApiResults.Error(http, result.Status, result.Code, result.Message!);
                }

                return Results.Ok(await ledger.GetWalletsAsync(resolved.WalletId, cancellationToken));
            })
            .RequireAuthorization()
            .WithName("DeleteTransaction")
            .WithTags("Ledger");

        group.MapPost("/transactions/{id:guid}/void", async (
                Guid id,
                Guid? walletId,
                LedgerService ledger,
                WalletSetService wallets,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var userId = UserId(user);
                if (userId is null)
                {
                    return ApiResults.Unauthorized(http);
                }

                var resolved = await ResolveWallet(walletId, user, wallets, http, cancellationToken);
                if (resolved.Error is not null)
                {
                    return resolved.Error;
                }

                var result = await ledger.VoidAsync(id, userId, resolved.WalletId, cancellationToken);
                if (result.Code is not null)
                {
                    return ApiResults.Error(http, result.Status, result.Code, result.Message!);
                }

                return Results.Ok(await ledger.GetWalletsAsync(resolved.WalletId, cancellationToken));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithName("VoidTransaction")
            .WithTags("Ledger");

        return group;
    }

    private static string? UserId(ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

    private static async Task<(IResult? Error, Guid WalletId)> ResolveWallet(
        Guid? walletId,
        ClaimsPrincipal user,
        WalletSetService wallets,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var userId = UserId(user);
        if (userId is null)
        {
            return (ApiResults.Unauthorized(http), Guid.Empty);
        }

        var result = await wallets.ResolveAsync(userId, user.IsInRole(Roles.Admin), walletId, cancellationToken);
        if (result.Code is not null)
        {
            return (ApiResults.Error(http, result.Status, result.Code, result.Message!), Guid.Empty);
        }

        return (null, result.WalletId);
    }
}
