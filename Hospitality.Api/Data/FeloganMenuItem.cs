namespace Hospitality.Api.Data;

public sealed class FeloganMenuItem
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public decimal SellPrice { get; set; }
    public decimal Cost { get; set; }
    public int OpeningPacks { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
