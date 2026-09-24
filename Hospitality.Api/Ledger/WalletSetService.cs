using Hospitality.Api.Data;
using Hospitality.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Api.Ledger;

public sealed class WalletSetService(AppDbContext db, UserManager<IdentityUser> users)
{
    public async Task<IReadOnlyList<WalletSetDto>> ListAsync(
        string userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (isAdmin)
        {
            return await db.WalletSets.AsNoTracking()
                .OrderBy(wallet => wallet.Name)
                .Select(wallet => new WalletSetDto(wallet.Id, wallet.Name, wallet.IsMain))
                .ToListAsync(cancellationToken);
        }

        var assignedId = await db.StaffWalletAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == userId)
            .Select(assignment => assignment.WalletId)
            .FirstOrDefaultAsync(cancellationToken);
        if (assignedId == Guid.Empty)
        {
            return [];
        }

        return await db.WalletSets.AsNoTracking()
            .Where(wallet => wallet.Id == assignedId)
            .Select(wallet => new WalletSetDto(wallet.Id, wallet.Name, wallet.IsMain))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> GetMainIdAsync(CancellationToken cancellationToken = default)
    {
        var id = await db.WalletSets.AsNoTracking()
            .Where(wallet => wallet.IsMain)
            .Select(wallet => wallet.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (id == Guid.Empty)
        {
            throw new InvalidOperationException("Main wallet is not seeded.");
        }

        return id;
    }

    public async Task EnsureMainAsync(CancellationToken cancellationToken = default)
    {
        var main = await db.WalletSets.FirstOrDefaultAsync(wallet => wallet.IsMain, cancellationToken);
        if (main is null)
        {
            main = new WalletSet
            {
                Id = Guid.NewGuid(),
                Name = WalletNames.Main,
                IsMain = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.WalletSets.Add(main);
            await db.SaveChangesAsync(cancellationToken);
        }

        var hasOpening = await db.WalletOpenings.AnyAsync(opening => opening.WalletId == main.Id, cancellationToken);
        if (!hasOpening)
        {
            var existing = await db.WalletOpenings.OrderBy(opening => opening.SetAt).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null && existing.WalletId == Guid.Empty)
            {
                existing.WalletId = main.Id;
            }
            else if (existing is null)
            {
                db.WalletOpenings.Add(new WalletOpening
                {
                    Id = Guid.NewGuid(),
                    WalletId = main.Id,
                    SetAt = DateTimeOffset.UtcNow
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var staff = await users.GetUsersInRoleAsync(Roles.Staff);
        var assigned = await db.StaffWalletAssignments
            .Select(assignment => assignment.UserId)
            .ToListAsync(cancellationToken);
        var assignedSet = assigned.ToHashSet(StringComparer.Ordinal);
        foreach (var user in staff)
        {
            if (assignedSet.Contains(user.Id))
            {
                continue;
            }

            db.StaffWalletAssignments.Add(new StaffWalletAssignment
            {
                UserId = user.Id,
                WalletId = main.Id
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(int Status, string? Code, string? Message, Guid WalletId)> ResolveAsync(
        string userId,
        bool isAdmin,
        Guid? requestedWalletId,
        CancellationToken cancellationToken = default)
    {
        if (isAdmin)
        {
            var id = requestedWalletId ?? await GetMainIdAsync(cancellationToken);
            var exists = await db.WalletSets.AnyAsync(wallet => wallet.Id == id, cancellationToken);
            return exists
                ? (200, null, null, id)
                : (404, "WALLET_NOT_FOUND", "That wallet was not found.", Guid.Empty);
        }

        var assigned = await db.StaffWalletAssignments.AsNoTracking()
            .FirstOrDefaultAsync(assignment => assignment.UserId == userId, cancellationToken);
        if (assigned is null)
        {
            return (409, "WALLET_NOT_ASSIGNED", "This account has no wallet assigned.", Guid.Empty);
        }

        if (requestedWalletId is { } requested && requested != assigned.WalletId)
        {
            return (403, "FORBIDDEN", "You do not have permission to perform this action.", Guid.Empty);
        }

        return (200, null, null, assigned.WalletId);
    }

    public async Task<(int Status, string? Code, string? Message)> AssignStaffAsync(
        string userId,
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.WalletSets.AnyAsync(wallet => wallet.Id == walletId, cancellationToken);
        if (!exists)
        {
            return (404, "WALLET_NOT_FOUND", "That wallet was not found.");
        }

        var assignment = await db.StaffWalletAssignments
            .FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        if (assignment is null)
        {
            db.StaffWalletAssignments.Add(new StaffWalletAssignment
            {
                UserId = userId,
                WalletId = walletId
            });
        }
        else
        {
            assignment.WalletId = walletId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null);
    }

    public async Task ClearAssignmentAsync(string userId, CancellationToken cancellationToken = default)
    {
        var assignment = await db.StaffWalletAssignments
            .FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        if (assignment is null)
        {
            return;
        }

        db.StaffWalletAssignments.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(string? Id, string? Name)> GetAssignedWalletAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.StaffWalletAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == userId)
            .Join(
                db.WalletSets.AsNoTracking(),
                assignment => assignment.WalletId,
                wallet => wallet.Id,
                (assignment, wallet) => new { assignment.WalletId, wallet.Name })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? (null, null) : (row.WalletId.ToString(), row.Name);
    }

    public async Task<(int Status, string? Code, string? Message, WalletSetDto? Value)> CreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateName(name, out var trimmed);
        if (validation is not null)
        {
            return (validation.Value.Status, validation.Value.Code, validation.Value.Message, null);
        }

        var taken = await NameTakenAsync(trimmed, excludeId: null, cancellationToken);
        if (taken)
        {
            return (409, "NAME_TAKEN", "That wallet name is already in use.", null);
        }

        var wallet = new WalletSet
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            IsMain = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.WalletSets.Add(wallet);
        db.WalletOpenings.Add(new WalletOpening
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            SetAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, new WalletSetDto(wallet.Id, wallet.Name, wallet.IsMain));
    }

    public async Task<(int Status, string? Code, string? Message, WalletSetDto? Value)> RenameAsync(
        Guid id,
        string name,
        CancellationToken cancellationToken = default)
    {
        var wallet = await db.WalletSets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (wallet is null)
        {
            return (404, "WALLET_NOT_FOUND", "That wallet was not found.", null);
        }

        var validation = ValidateName(name, out var trimmed);
        if (validation is not null)
        {
            return (validation.Value.Status, validation.Value.Code, validation.Value.Message, null);
        }

        var taken = await NameTakenAsync(trimmed, excludeId: id, cancellationToken);
        if (taken)
        {
            return (409, "NAME_TAKEN", "That wallet name is already in use.", null);
        }

        wallet.Name = trimmed;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, new WalletSetDto(wallet.Id, wallet.Name, wallet.IsMain));
    }

    public async Task<(int Status, string? Code, string? Message)> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var wallet = await db.WalletSets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (wallet is null)
        {
            return (404, "WALLET_NOT_FOUND", "That wallet was not found.");
        }

        if (wallet.IsMain)
        {
            return (400, "CANNOT_DELETE_MAIN", "The Main wallet cannot be deleted.");
        }

        var mainId = await GetMainIdAsync(cancellationToken);
        var assignments = await db.StaffWalletAssignments
            .Where(assignment => assignment.WalletId == id)
            .ToListAsync(cancellationToken);
        foreach (var assignment in assignments)
        {
            assignment.WalletId = mainId;
        }

        db.LedgerTransactions.RemoveRange(
            db.LedgerTransactions.Where(row => row.WalletId == id));
        db.WalletOpenings.RemoveRange(
            db.WalletOpenings.Where(opening => opening.WalletId == id));
        db.WalletSets.Remove(wallet);
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null);
    }

    private async Task<bool> NameTakenAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var existing = await db.WalletSets.AsNoTracking()
            .Where(wallet => excludeId == null || wallet.Id != excludeId)
            .Select(wallet => wallet.Name)
            .ToListAsync(cancellationToken);
        return existing.Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
    }

    private static (int Status, string Code, string Message)? ValidateName(string? name, out string trimmed)
    {
        trimmed = name?.Trim() ?? "";
        if (trimmed.Length is < 1 or > 32)
        {
            return (400, "VALIDATION_ERROR", "Name must be 1–32 characters.");
        }

        return null;
    }
}
