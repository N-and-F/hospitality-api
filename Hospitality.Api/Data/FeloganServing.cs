namespace Hospitality.Api.Data;

public sealed class FeloganServing
{
    public Guid Id { get; set; }
    public DateOnly ServedOn { get; set; }
    public Guid RoomId { get; set; }
    public Guid CookId { get; set; }
    public string? Note { get; set; }
    public decimal AdditionalProfit { get; set; }
    public decimal ExcessPaid { get; set; }
    public string PaidCook { get; set; } = "no";
    public string PaidSham { get; set; } = "na";
    public int CookPercent { get; set; } = 40;
    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid ClientRequestId { get; set; }
    public FeloganRoom Room { get; set; } = null!;
    public FeloganCook Cook { get; set; } = null!;
    public ICollection<FeloganServingLine> Lines { get; set; } = [];
}
