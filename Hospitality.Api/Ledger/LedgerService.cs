using Hospitality.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Api.Ledger;

public sealed class LedgerService(AppDbContext db, UserManager<IdentityUser> users)
{
    public static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public async Task<WalletDto> GetWalletsAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        var wallets = await CurrentWalletsAsync(walletId, cancellationToken);
        return ToDto(wallets);
    }

    public MethodsResponseDto ListMethods()
    {
        var items = TransactionMethods.All.Select(method => new MethodInfoDto(
            method,
            TransactionMethods.Labels[method],
            !TransactionMethods.ForcesZeroProfit.Contains(method),
            SuggestionRule(method))).ToArray();
        return new MethodsResponseDto(items);
    }

    public async Task<(int Status, string? Code, string? Message, TransactionDto? Value)> CreateAsync(
        CreateTransactionRequest request,
        string userId,
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId is null || request.ClientRequestId == Guid.Empty)
        {
            return (400, "VALIDATION_ERROR", "clientRequestId is required.", null);
        }

        if (!TransactionMethods.IsKnown(request.Method))
        {
            return (400, "VALIDATION_ERROR", "Unknown method.", null);
        }

        if (request.Amount <= 0 || HasExtraDecimals(request.Amount))
        {
            return (400, "VALIDATION_ERROR", "Amount must be greater than 0 with at most 2 decimal places.", null);
        }

        if (request.Profit is < 0 || (request.Profit is { } postedProfit && HasExtraDecimals(postedProfit)))
        {
            return (400, "VALIDATION_ERROR", "Profit cannot be negative and must have at most 2 decimal places.", null);
        }

        if (request.Cashback is < 0 || (request.Cashback is { } postedCashback && HasExtraDecimals(postedCashback)))
        {
            return (400, "VALIDATION_ERROR", "Cashback cannot be negative and must have at most 2 decimal places.", null);
        }

        if (request.Note is { Length: > 200 })
        {
            return (400, "VALIDATION_ERROR", "Note must be 200 characters or fewer.", null);
        }

        var existing = await db.LedgerTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ClientRequestId == request.ClientRequestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.InChargeUserId != userId)
            {
                return (409, "CLIENT_REQUEST_CONFLICT", "That clientRequestId was already used.", null);
            }

            return (200, null, null, await ToDtoAsync(existing, cancellationToken));
        }

        var appliedProfit = TransactionMethods.ForcesZeroProfit.Contains(request.Method)
            ? 0
            : request.Profit ?? ProfitSuggestion.For(request.Method, request.Amount);
        var profitInGcash = appliedProfit > 0 && request.ProfitInGcash;
        var appliedCashback = AppliedCashback(request.Method, request.Cashback);
        var deltas = WalletMath.Deltas(request.Method, request.Amount, appliedProfit, profitInGcash, appliedCashback);
        var current = await CurrentWalletsAsync(walletId, cancellationToken);
        if (WalletMath.MakesCashOrGcashNegative(current, deltas))
        {
            return (409, "INSUFFICIENT_WALLET", "This transaction would make cash on hand or GCash negative.", null);
        }

        var after = current.Add(deltas);
        var occurredAt = request.OccurredAt?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        var row = new LedgerTransaction
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
            Method = request.Method,
            Amount = decimal.Round(request.Amount, 2),
            Profit = decimal.Round(appliedProfit, 2),
            ProfitInGcash = profitInGcash,
            Cashback = decimal.Round(appliedCashback, 2),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            InChargeUserId = userId,
            WalletId = walletId,
            DeltaCashOnHand = deltas.CashOnHand,
            DeltaGcash = deltas.Gcash,
            DeltaTpc = deltas.Tpc,
            BalanceAfterCashOnHand = after.CashOnHand,
            BalanceAfterGcash = after.Gcash,
            BalanceAfterTpc = after.Tpc,
            ClientRequestId = request.ClientRequestId.Value
        };

        db.LedgerTransactions.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var replay = await db.LedgerTransactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.ClientRequestId == request.ClientRequestId, cancellationToken);
            if (replay is not null && replay.InChargeUserId == userId)
            {
                return (200, null, null, await ToDtoAsync(replay, cancellationToken));
            }

            return (409, "CLIENT_REQUEST_CONFLICT", "That clientRequestId was already used.", null);
        }

        return (200, null, null, await ToDtoAsync(row, cancellationToken));
    }

    public async Task<(int Status, string? Code, string? Message, TransactionDto? Value)> UpdateAsync(
        Guid id,
        UpdateTransactionRequest request,
        string userId,
        bool isAdmin,
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.LedgerTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (row is null || row.WalletId != walletId)
        {
            return (404, "TRANSACTION_NOT_FOUND", "Transaction was not found.", null);
        }

        if (row.VoidedAt is not null)
        {
            return (409, "ALREADY_VOIDED", "Transaction is already voided.", null);
        }

        if (!TransactionMethods.IsKnown(request.Method))
        {
            return (400, "VALIDATION_ERROR", "Unknown method.", null);
        }

        if (request.Amount <= 0 || HasExtraDecimals(request.Amount))
        {
            return (400, "VALIDATION_ERROR", "Amount must be greater than 0 with at most 2 decimal places.", null);
        }

        if (request.Profit is < 0 || (request.Profit is { } postedProfit && HasExtraDecimals(postedProfit)))
        {
            return (400, "VALIDATION_ERROR", "Profit cannot be negative and must have at most 2 decimal places.", null);
        }

        if (request.Cashback is < 0 || (request.Cashback is { } postedCashback && HasExtraDecimals(postedCashback)))
        {
            return (400, "VALIDATION_ERROR", "Cashback cannot be negative and must have at most 2 decimal places.", null);
        }

        if (request.Note is { Length: > 200 })
        {
            return (400, "VALIDATION_ERROR", "Note must be 200 characters or fewer.", null);
        }

        var appliedProfit = TransactionMethods.ForcesZeroProfit.Contains(request.Method)
            ? 0
            : request.Profit ?? ProfitSuggestion.For(request.Method, request.Amount);
        var profitInGcash = appliedProfit > 0 && request.ProfitInGcash;
        var appliedCashback = AppliedCashback(request.Method, request.Cashback);
        var newDeltas = WalletMath.Deltas(request.Method, request.Amount, appliedProfit, profitInGcash, appliedCashback);
        var current = await CurrentWalletsAsync(walletId, cancellationToken);
        var without = current.Add(new WalletAmounts(-row.DeltaCashOnHand, -row.DeltaGcash, -row.DeltaTpc));
        if (WalletMath.MakesCashOrGcashNegative(without, newDeltas))
        {
            return (409, "INSUFFICIENT_WALLET", "This transaction would make cash on hand or GCash negative.", null);
        }

        var after = without.Add(newDeltas);
        row.Method = request.Method;
        row.Amount = decimal.Round(request.Amount, 2);
        row.Profit = decimal.Round(appliedProfit, 2);
        row.ProfitInGcash = profitInGcash;
        row.Cashback = decimal.Round(appliedCashback, 2);
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        row.DeltaCashOnHand = newDeltas.CashOnHand;
        row.DeltaGcash = newDeltas.Gcash;
        row.DeltaTpc = newDeltas.Tpc;
        row.BalanceAfterCashOnHand = after.CashOnHand;
        row.BalanceAfterGcash = after.Gcash;
        row.BalanceAfterTpc = after.Tpc;
        if (request.OccurredAt is not null)
        {
            row.OccurredAt = request.OccurredAt.Value.ToUniversalTime();
        }

        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, await ToDtoAsync(row, cancellationToken));
    }

    public async Task<(int Status, string? Code, string? Message)> DeleteAsync(
        Guid id,
        string userId,
        bool isAdmin,
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.LedgerTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (row is null || row.WalletId != walletId)
        {
            return (404, "TRANSACTION_NOT_FOUND", "Transaction was not found.");
        }

        if (!isAdmin && row.InChargeUserId != userId)
        {
            return (403, "FORBIDDEN", "You do not have permission to perform this action.");
        }

        db.LedgerTransactions.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null);
    }

    public async Task<(int Status, string? Code, string? Message, TransactionListDto? Value)> ListAsync(
        string? date,
        string? range,
        string userId,
        bool isAdmin,
        bool includeVoided,
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(date, out var day))
        {
            var manilaNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Manila);
            day = DateOnly.FromDateTime(manilaNow.DateTime);
        }

        var period = (range ?? "day").Trim().ToLowerInvariant();
        if (period is not ("day" or "week" or "month"))
        {
            return (400, "VALIDATION_ERROR", "Range must be day, week, or month.", null);
        }

        var (from, exclusiveEnd) = PeriodBounds(day, period);
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), Manila);
        var end = TimeZoneInfo.ConvertTimeToUtc(exclusiveEnd.ToDateTime(TimeOnly.MinValue), Manila);

        var query = db.LedgerTransactions.AsNoTracking()
            .Where(t => t.WalletId == walletId && t.OccurredAt >= start && t.OccurredAt < end);

        if (!isAdmin)
        {
            includeVoided = false;
        }

        if (!includeVoided)
        {
            query = query.Where(t => t.VoidedAt == null);
        }

        var rows = await query.OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id).ToListAsync(cancellationToken);
        var items = new List<TransactionDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await ToDtoAsync(row, cancellationToken));
        }

        var active = rows.Where(r => r.VoidedAt is null).ToList();
        var byMethod = active
            .GroupBy(r => r.Method)
            .ToDictionary(
                g => g.Key,
                g => new MethodTotalsDto(g.Count(), g.Sum(x => x.Amount), g.Sum(x => x.Profit)));

        var totals = new TransactionTotalsDto(active.Count, active.Sum(x => x.Profit), byMethod);
        return (200, null, null, new TransactionListDto(
            day.ToString("yyyy-MM-dd"),
            from.ToString("yyyy-MM-dd"),
            exclusiveEnd.AddDays(-1).ToString("yyyy-MM-dd"),
            period,
            items,
            totals,
            await GetWalletsAsync(walletId, cancellationToken)));
    }

    private static (DateOnly From, DateOnly ExclusiveEnd) PeriodBounds(DateOnly day, string range) =>
        range switch
        {
            "week" => WeekBounds(day),
            "month" => (new DateOnly(day.Year, day.Month, 1), new DateOnly(day.Year, day.Month, 1).AddMonths(1)),
            _ => (day, day.AddDays(1))
        };

    private static (DateOnly From, DateOnly ExclusiveEnd) WeekBounds(DateOnly day)
    {
        var offset = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = day.AddDays(-offset);
        return (monday, monday.AddDays(7));
    }

    public async Task<(int Status, string? Code, string? Message)> VoidAsync(
        Guid id,
        string adminUserId,
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.LedgerTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (row is null || row.WalletId != walletId)
        {
            return (404, "TRANSACTION_NOT_FOUND", "Transaction was not found.");
        }

        if (row.VoidedAt is not null)
        {
            return (409, "ALREADY_VOIDED", "Transaction is already voided.");
        }

        row.VoidedAt = DateTimeOffset.UtcNow;
        row.VoidedByUserId = adminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null);
    }

    public async Task<(int Status, string? Code, string? Message, WalletDto? Value)> SetOpeningAsync(
        SetOpeningRequest request,
        string adminUserId,
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        if (HasExtraDecimals(request.CashOnHand) || HasExtraDecimals(request.Gcash) || HasExtraDecimals(request.Tpc))
        {
            return (400, "VALIDATION_ERROR", "Wallet amounts must have at most 2 decimal places.", null);
        }

        var posted = await db.LedgerTransactions.AsNoTracking()
            .Where(t => t.WalletId == walletId && t.VoidedAt == null)
            .Select(t => new { t.DeltaCashOnHand, t.DeltaGcash, t.DeltaTpc })
            .ToListAsync(cancellationToken);
        var postedCash = posted.Sum(t => t.DeltaCashOnHand);
        var postedGcash = posted.Sum(t => t.DeltaGcash);
        var postedTpc = posted.Sum(t => t.DeltaTpc);

        var opening = await db.WalletOpenings.FirstOrDefaultAsync(o => o.WalletId == walletId, cancellationToken);
        if (opening is null)
        {
            opening = new WalletOpening { Id = Guid.NewGuid(), WalletId = walletId };
            db.WalletOpenings.Add(opening);
        }

        opening.CashOnHand = decimal.Round(request.CashOnHand - postedCash, 2);
        opening.Gcash = decimal.Round(request.Gcash - postedGcash, 2);
        opening.Tpc = decimal.Round(request.Tpc - postedTpc, 2);
        opening.SetAt = DateTimeOffset.UtcNow;
        opening.SetByUserId = adminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, await GetWalletsAsync(walletId, cancellationToken));
    }

    private async Task<WalletAmounts> CurrentWalletsAsync(Guid walletId, CancellationToken cancellationToken)
    {
        var opening = await db.WalletOpenings.AsNoTracking()
            .FirstOrDefaultAsync(o => o.WalletId == walletId, cancellationToken);
        var baseAmounts = opening is null
            ? WalletAmounts.Zero
            : new WalletAmounts(opening.CashOnHand, opening.Gcash, opening.Tpc);

        var posted = await db.LedgerTransactions.AsNoTracking()
            .Where(t => t.WalletId == walletId && t.VoidedAt == null)
            .Select(t => new { t.DeltaCashOnHand, t.DeltaGcash, t.DeltaTpc })
            .ToListAsync(cancellationToken);

        if (posted.Count == 0)
        {
            return baseAmounts;
        }

        return baseAmounts.Add(new WalletAmounts(
            posted.Sum(t => t.DeltaCashOnHand),
            posted.Sum(t => t.DeltaGcash),
            posted.Sum(t => t.DeltaTpc)));
    }

    private async Task<TransactionDto> ToDtoAsync(LedgerTransaction row, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(row.InChargeUserId);
        return new TransactionDto(
            row.Id,
            TimeZoneInfo.ConvertTime(row.OccurredAt, Manila),
            row.Method,
            row.Amount,
            row.Profit,
            row.ProfitInGcash,
            row.Cashback,
            row.Note,
            new InChargeDto(row.InChargeUserId, user?.UserName ?? ""),
            new WalletDto(row.DeltaCashOnHand, row.DeltaGcash, row.DeltaTpc),
            new WalletDto(row.BalanceAfterCashOnHand, row.BalanceAfterGcash, row.BalanceAfterTpc),
            row.VoidedAt,
            row.ClientRequestId);
    }

    private static WalletDto ToDto(WalletAmounts wallets) =>
        new(wallets.CashOnHand, wallets.Gcash, wallets.Tpc);

    private static bool HasExtraDecimals(decimal value) => decimal.Round(value, 2) != value;

    private static decimal AppliedCashback(string method, decimal? cashback) =>
        method == TransactionMethods.LoadViaTpc ? cashback ?? 0 : 0;

    private static string SuggestionRule(string method) => method switch
    {
        TransactionMethods.LoadViaGcash or TransactionMethods.LoadViaTpc =>
            "₱5 if amount < 100, otherwise ₱10",
        TransactionMethods.CashIn or TransactionMethods.CashOut or TransactionMethods.CashOutWithCharge =>
            "₱5 if amount < 100; otherwise 2% rounded to the nearest ₱10, minimum ₱10",
        _ => "₱0"
    };
}
