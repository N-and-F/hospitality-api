namespace Hospitality.Api.Identity;

public sealed record UserPublicDto(
    string Id,
    string Email,
    string Username,
    string[] Roles,
    string? AssignedWalletId,
    string? AssignedWalletName);

public sealed record StaffListResponse(IReadOnlyList<UserPublicDto> Items);
