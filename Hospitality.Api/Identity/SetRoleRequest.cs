namespace Hospitality.Api.Identity;

public sealed class SetRoleRequest
{
    public string Role { get; set; } = "";
    public Guid? WalletId { get; set; }
}
