namespace Hospitality.Api.Ledger;

public sealed class CreateTransactionRequest
{
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? Profit { get; set; }
    public bool ProfitInGcash { get; set; }
    public decimal? Cashback { get; set; }
    public decimal? ConvenienceCharge { get; set; }
    public decimal? GcashAmount { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public Guid? ClientRequestId { get; set; }
}

public sealed class UpdateTransactionRequest
{
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? Profit { get; set; }
    public bool ProfitInGcash { get; set; }
    public decimal? Cashback { get; set; }
    public decimal? ConvenienceCharge { get; set; }
    public decimal? GcashAmount { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
}

public sealed class SetOpeningRequest
{
    public decimal CashOnHand { get; set; }
    public decimal Gcash { get; set; }
    public decimal Tpc { get; set; }
}

public sealed record WalletDto(decimal CashOnHand, decimal Gcash, decimal Tpc);

public sealed record InChargeDto(string Id, string Username);

public sealed record TransactionDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Method,
    decimal Amount,
    decimal Profit,
    bool ProfitInGcash,
    decimal Cashback,
    decimal ConvenienceCharge,
    decimal GcashAmount,
    string? Note,
    InChargeDto InCharge,
    WalletDto Deltas,
    WalletDto BalancesAfter,
    DateTimeOffset? VoidedAt,
    Guid ClientRequestId);

public sealed record MethodTotalsDto(int Count, decimal Amount, decimal Profit);

public sealed record TransactionTotalsDto(
    int Count,
    decimal Profit,
    IReadOnlyDictionary<string, MethodTotalsDto> ByMethod);

public sealed record TransactionListDto(
    string Date,
    string From,
    string To,
    string Range,
    IReadOnlyList<TransactionDto> Items,
    TransactionTotalsDto Totals,
    WalletDto Wallets);

public sealed record MethodInfoDto(string Method, string Label, bool SuggestsProfit, string SuggestionRule);

public sealed record MethodsResponseDto(IReadOnlyList<MethodInfoDto> Items);
