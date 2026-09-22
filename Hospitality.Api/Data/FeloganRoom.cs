namespace Hospitality.Api.Data;

public sealed class FeloganRoom
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
