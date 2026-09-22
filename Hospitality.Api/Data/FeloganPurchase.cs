namespace Hospitality.Api.Data;

public sealed class FeloganPurchase
{
    public Guid Id { get; set; }
    public DateOnly PurchasedOn { get; set; }
    public Guid MenuItemId { get; set; }
    public int Qty { get; set; }
    public string? Note { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid ClientRequestId { get; set; }
    public FeloganMenuItem MenuItem { get; set; } = null!;
}
