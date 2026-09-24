namespace Hospitality.Api.Data;

public sealed class WalletSet
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsMain { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
