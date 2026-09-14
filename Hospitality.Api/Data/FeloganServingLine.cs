namespace Hospitality.Api.Data;

public sealed class FeloganServingLine
{
    public Guid Id { get; set; }
    public Guid ServingId { get; set; }
    public Guid MenuItemId { get; set; }
    public required string SnapshotName { get; set; }
    public required string SnapshotKind { get; set; }
    public decimal SnapshotSellPrice { get; set; }
    public decimal SnapshotCost { get; set; }
    public int Qty { get; set; }
    public FeloganServing Serving { get; set; } = null!;
    public FeloganMenuItem MenuItem { get; set; } = null!;
}
