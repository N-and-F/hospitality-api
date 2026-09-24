namespace Hospitality.Api.Identity;

public sealed class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Role { get; set; }
    public Guid? WalletId { get; set; }
}
