using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Hospitality.Api.Auth;
using Hospitality.Api.Ledger;
using Microsoft.AspNetCore.Identity;

namespace Hospitality.Api.Identity;

public sealed class UserService(
    UserManager<IdentityUser> userManager,
    JwtTokenService tokens,
    WalletSetService wallets)
{
    public async Task<(int Status, string? Code, string? Message, TokenResponse? Token)> RegisterAsync(
        RegisterRequest request)
    {
        var validation = ValidateAccount(request, requireEmail: true);
        if (validation is not null)
        {
            return validation.Value;
        }

        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            return (StatusCodes.Status409Conflict, "EMAIL_TAKEN", "That email is already registered.", null);
        }

        if (await userManager.FindByNameAsync(request.Username.Trim()) is not null)
        {
            return (StatusCodes.Status409Conflict, "USERNAME_TAKEN", "That username is already taken.", null);
        }

        var created = await CreateStaffUserAsync(request, Roles.Staff);
        if (!created.Succeeded)
        {
            return MapIdentityErrors(created);
        }

        var user = await userManager.FindByNameAsync(request.Username.Trim())
            ?? throw new InvalidOperationException("User was created but could not be reloaded.");
        var assigned = await wallets.AssignStaffAsync(user.Id, await wallets.GetMainIdAsync());
        if (assigned.Code is not null)
        {
            return (assigned.Status, assigned.Code, assigned.Message, null);
        }

        var roles = await userManager.GetRolesAsync(user);
        return (StatusCodes.Status200OK, null, null, new TokenResponse(tokens.Create(user, roles)));
    }

    public async Task<(int Status, string? Code, string? Message, TokenResponse? Token)> LoginAsync(
        LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Username and password are required.", null);
        }

        var user = await userManager.FindByNameAsync(request.Username.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return (StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", "Invalid username or password.", null);
        }

        var roles = await userManager.GetRolesAsync(user);
        return (StatusCodes.Status200OK, null, null, new TokenResponse(tokens.Create(user, roles)));
    }

    public async Task<(int Status, string? Code, string? Message, UserPublicDto? User)> CreateStaffAsync(
        RegisterRequest request)
    {
        var validation = ValidateAccount(request, requireEmail: false);
        if (validation is not null)
        {
            return (validation.Value.Status, validation.Value.Code, validation.Value.Message, null);
        }

        if (HasEmail(request.Email) && await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            return (StatusCodes.Status409Conflict, "EMAIL_TAKEN", "That email is already registered.", null);
        }

        if (await userManager.FindByNameAsync(request.Username.Trim()) is not null)
        {
            return (StatusCodes.Status409Conflict, "USERNAME_TAKEN", "That username is already taken.", null);
        }

        var role = ParseRole(request.Role, optional: true);
        if (role is null)
        {
            return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Role must be Admin or Staff.", null);
        }

        if (role == Roles.Staff && (request.WalletId is null || request.WalletId == Guid.Empty))
        {
            return (StatusCodes.Status400BadRequest, "WALLET_REQUIRED", "A wallet is required for Staff accounts.", null);
        }

        var created = await CreateStaffUserAsync(request, role);
        if (!created.Succeeded)
        {
            var mapped = MapIdentityErrors(created);
            return (mapped.Status, mapped.Code, mapped.Message, null);
        }

        var user = await userManager.FindByNameAsync(request.Username.Trim())
            ?? throw new InvalidOperationException("User was created but could not be reloaded.");
        if (role == Roles.Staff)
        {
            var assigned = await wallets.AssignStaffAsync(user.Id, request.WalletId!.Value);
            if (assigned.Code is not null)
            {
                return (assigned.Status, assigned.Code, assigned.Message, null);
            }
        }

        return (StatusCodes.Status200OK, null, null, await ToPublicDtoAsync(user));
    }

    public async Task<IReadOnlyList<UserPublicDto>> ListStaffAsync()
    {
        var users = userManager.Users.OrderBy(u => u.UserName).ToList();
        var items = new List<UserPublicDto>(users.Count);
        foreach (var user in users)
        {
            items.Add(await ToPublicDtoAsync(user));
        }

        return items;
    }

    public async Task<(int Status, string? Code, string? Message, UserPublicDto? User)> SetRoleAsync(
        string targetUserId,
        string requestedRole,
        string actorUserId,
        Guid? walletId)
    {
        var role = ParseRole(requestedRole, optional: false);
        if (role is null)
        {
            return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Role must be Admin or Staff.", null);
        }

        var user = await userManager.FindByIdAsync(targetUserId);
        if (user is null)
        {
            return (StatusCodes.Status404NotFound, "USER_NOT_FOUND", "That user was not found.", null);
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Contains(role))
        {
            return (StatusCodes.Status200OK, null, null, await ToPublicDtoAsync(user));
        }

        if (role == Roles.Staff && currentRoles.Contains(Roles.Admin))
        {
            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1)
            {
                return (StatusCodes.Status400BadRequest, "LAST_ADMIN", "The last Admin cannot be demoted.", null);
            }
        }

        if (string.Equals(actorUserId, user.Id, StringComparison.Ordinal))
        {
            return (StatusCodes.Status400BadRequest, "CANNOT_CHANGE_OWN_ROLE", "You cannot change your own role.", null);
        }

        if (role == Roles.Staff && (walletId is null || walletId == Guid.Empty))
        {
            return (StatusCodes.Status400BadRequest, "WALLET_REQUIRED", "A wallet is required for Staff accounts.", null);
        }

        if (currentRoles.Count > 0)
        {
            var removed = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removed.Succeeded)
            {
                var mapped = MapIdentityErrors(removed);
                return (mapped.Status, mapped.Code, mapped.Message, null);
            }
        }

        var added = await userManager.AddToRoleAsync(user, role);
        if (!added.Succeeded)
        {
            var mapped = MapIdentityErrors(added);
            return (mapped.Status, mapped.Code, mapped.Message, null);
        }

        if (role == Roles.Admin)
        {
            await wallets.ClearAssignmentAsync(user.Id);
        }
        else
        {
            var assigned = await wallets.AssignStaffAsync(user.Id, walletId!.Value);
            if (assigned.Code is not null)
            {
                return (assigned.Status, assigned.Code, assigned.Message, null);
            }
        }

        return (StatusCodes.Status200OK, null, null, await ToPublicDtoAsync(user));
    }

    public async Task<(int Status, string? Code, string? Message, UserPublicDto? User)> SetWalletAsync(
        string targetUserId,
        Guid walletId)
    {
        var user = await userManager.FindByIdAsync(targetUserId);
        if (user is null)
        {
            return (StatusCodes.Status404NotFound, "USER_NOT_FOUND", "That user was not found.", null);
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Staff))
        {
            return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Only Staff can be assigned a wallet.", null);
        }

        var assigned = await wallets.AssignStaffAsync(user.Id, walletId);
        if (assigned.Code is not null)
        {
            return (assigned.Status, assigned.Code, assigned.Message, null);
        }

        return (StatusCodes.Status200OK, null, null, await ToPublicDtoAsync(user));
    }

    public async Task<(int Status, string? Code, string? Message)> DeleteAsync(string targetUserId, string actorUserId)
    {
        if (string.Equals(actorUserId, targetUserId, StringComparison.Ordinal))
        {
            return (StatusCodes.Status400BadRequest, "CANNOT_DELETE_OWN_ACCOUNT", "You cannot delete your own account.");
        }

        var user = await userManager.FindByIdAsync(targetUserId);
        if (user is null)
        {
            return (StatusCodes.Status404NotFound, "USER_NOT_FOUND", "That user was not found.");
        }

        if (await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1)
            {
                return (StatusCodes.Status400BadRequest, "LAST_ADMIN", "The last Admin cannot be deleted.");
            }
        }

        var deleted = await userManager.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            var mapped = MapIdentityErrors(deleted);
            return (mapped.Status, mapped.Code, mapped.Message);
        }

        return (StatusCodes.Status200OK, null, null);
    }

    public async Task<UserPublicDto?> GetByIdAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : await ToPublicDtoAsync(user);
    }

    public async Task<UserPublicDto> ToPublicDtoAsync(IdentityUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var assignment = await wallets.GetAssignedWalletAsync(user.Id);
        return new UserPublicDto(
            user.Id,
            user.Email ?? "",
            user.UserName ?? "",
            [.. roles],
            assignment.Id,
            assignment.Name);
    }

    private async Task<IdentityResult> CreateStaffUserAsync(RegisterRequest request, string roleName)
    {
        var user = new IdentityUser
        {
            UserName = request.Username.Trim(),
            Email = HasEmail(request.Email) ? request.Email.Trim() : null,
            EmailConfirmed = true
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return created;
        }

        var role = await userManager.AddToRoleAsync(user, roleName);
        if (!role.Succeeded)
        {
            return role;
        }

        return IdentityResult.Success;
    }

    private static string? ParseRole(string? role, bool optional)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return optional ? Roles.Staff : null;
        }

        if (string.Equals(role.Trim(), Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Roles.Admin;
        }

        if (string.Equals(role.Trim(), Roles.Staff, StringComparison.OrdinalIgnoreCase))
        {
            return Roles.Staff;
        }

        return null;
    }

    private static (int Status, string? Code, string? Message, TokenResponse? Token)? ValidateAccount(
        RegisterRequest request,
        bool requireEmail)
    {
        if (requireEmail && !HasEmail(request.Email))
        {
            return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "A valid email is required.", null);
        }

        if (HasEmail(request.Email) && !IsEmail(request.Email.Trim()))
        {
            return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "A valid email is required.", null);
        }

        if (!UsernameRules.IsValid(request.Username.Trim()))
        {
            return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR",
                "Username must be 3–32 characters and contain only letters, digits, or underscore.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Password is required.", null);
        }

        return null;
    }

    private static (int Status, string? Code, string? Message, TokenResponse? Token) MapIdentityErrors(
        IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is "DuplicateEmail"))
        {
            return (StatusCodes.Status409Conflict, "EMAIL_TAKEN", "That email is already registered.", null);
        }

        if (result.Errors.Any(e => e.Code is "DuplicateUserName"))
        {
            return (StatusCodes.Status409Conflict, "USERNAME_TAKEN", "That username is already taken.", null);
        }

        var message = string.Join(" ", result.Errors.Select(e => e.Description));
        return (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", message, null);
    }

    private static bool HasEmail(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool IsEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return new EmailAddressAttribute().IsValid(value);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
