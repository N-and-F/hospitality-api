namespace Hospitality.Api.Data;

public sealed class LedgerTransaction
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Method { get; set; }
    public decimal Amount { get; set; }
    public decimal Profit { get; set; }
    public bool ProfitInGcash { get; set; }
    public string? Note { get; set; }
    public required string InChargeUserId { get; set; }
    public decimal DeltaCashOnHand { get; set; }
    public decimal DeltaGcash { get; set; }
    public decimal DeltaTpc { get; set; }
    public decimal BalanceAfterCashOnHand { get; set; }
    public decimal BalanceAfterGcash { get; set; }
    public decimal BalanceAfterTpc { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public string? VoidedByUserId { get; set; }
    public Guid ClientRequestId { get; set; }
}
