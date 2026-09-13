namespace Hospitality.Api.Data;

public sealed class WalletOpening
{
    public Guid Id { get; set; }
    public decimal CashOnHand { get; set; }
    public decimal Gcash { get; set; }
    public decimal Tpc { get; set; }
    public DateTimeOffset SetAt { get; set; }
    public string? SetByUserId { get; set; }
}
