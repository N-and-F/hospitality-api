namespace Hospitality.Api.Data;

public sealed class StaffWalletAssignment
{
    public required string UserId { get; set; }
    public Guid WalletId { get; set; }
}
