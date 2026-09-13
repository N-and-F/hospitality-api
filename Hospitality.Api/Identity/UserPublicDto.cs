namespace Hospitality.Api.Identity;

public sealed record UserPublicDto(string Id, string Email, string Username, string[] Roles);

public sealed record StaffListResponse(IReadOnlyList<UserPublicDto> Items);
