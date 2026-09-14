using Hospitality.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Api.Felogan;

public sealed class FeloganService(AppDbContext db)
{
    public async Task<CatalogListDto> ListRoomsAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var query = db.FeloganRooms.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(room => room.ArchivedAt == null);
        }

        var rooms = await query
            .OrderBy(room => room.SortOrder)
            .ThenBy(room => room.Name)
            .Select(room => new CatalogItemDto(room.Id, room.Name, room.ArchivedAt))
            .ToListAsync(cancellationToken);

        return new CatalogListDto(rooms);
    }

    public async Task<(int Status, string? Code, string? Message, CatalogItemDto? Value)> CreateRoomAsync(
        CatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateName(request.Name, out var name);
        if (validation is not null)
        {
            return (validation.Status, validation.Code, validation.Message, null);
        }

        var normalizedName = name.ToLower();
        var duplicate = await db.FeloganRooms.AnyAsync(
            room => room.ArchivedAt == null && room.Name.ToLower() == normalizedName,
            cancellationToken);
        if (duplicate)
        {
            return (409, "NAME_TAKEN", "An active room with that name already exists.", null);
        }

        var maxSortOrder = await db.FeloganRooms
            .Select(room => (int?)room.SortOrder)
            .MaxAsync(cancellationToken);
        var room = new FeloganRoom
        {
            Id = Guid.NewGuid(),
            Name = name,
            SortOrder = (maxSortOrder ?? -1) + 1
        };

        db.FeloganRooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(room));
    }

    public async Task<(int Status, string? Code, string? Message, CatalogItemDto? Value)> UpdateRoomAsync(
        Guid id,
        CatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var room = await db.FeloganRooms.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (room is null)
        {
            return (404, "ROOM_NOT_FOUND", "Room not found.", null);
        }

        var validation = ValidateName(request.Name, out var name);
        if (validation is not null)
        {
            return (validation.Status, validation.Code, validation.Message, null);
        }

        if (room.ArchivedAt is null)
        {
            var normalizedName = name.ToLower();
            var duplicate = await db.FeloganRooms.AnyAsync(
                candidate => candidate.Id != id
                    && candidate.ArchivedAt == null
                    && candidate.Name.ToLower() == normalizedName,
                cancellationToken);
            if (duplicate)
            {
                return (409, "NAME_TAKEN", "An active room with that name already exists.", null);
            }
        }

        room.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(room));
    }

    public async Task<(int Status, string? Code, string? Message, CatalogItemDto? Value)> ArchiveRoomAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var room = await db.FeloganRooms.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (room is null)
        {
            return (404, "ROOM_NOT_FOUND", "Room not found.", null);
        }

        var invalidState = ValidateArchiveState(room.ArchivedAt, archiving: true, "Room");
        if (invalidState is not null)
        {
            return (invalidState.Status, invalidState.Code, invalidState.Message, null);
        }

        room.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(room));
    }

    public async Task<(int Status, string? Code, string? Message, CatalogItemDto? Value)> UnarchiveRoomAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var room = await db.FeloganRooms.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (room is null)
        {
            return (404, "ROOM_NOT_FOUND", "Room not found.", null);
        }

        var invalidState = ValidateArchiveState(room.ArchivedAt, archiving: false, "Room");
        if (invalidState is not null)
        {
            return (invalidState.Status, invalidState.Code, invalidState.Message, null);
        }

        var normalizedName = room.Name.ToLower();
        var duplicate = await db.FeloganRooms.AnyAsync(
            candidate => candidate.Id != id
                && candidate.ArchivedAt == null
                && candidate.Name.ToLower() == normalizedName,
            cancellationToken);
        if (duplicate)
        {
            return (409, "NAME_TAKEN", "An active room with that name already exists.", null);
        }

        room.ArchivedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(room));
    }

    public async Task<CatalogListDto> ListCooksAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var query = db.FeloganCooks.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(cook => cook.ArchivedAt == null);
        }

        var cooks = await query
            .OrderBy(cook => cook.SortOrder)
            .ThenBy(cook => cook.Name)
            .Select(cook => new CatalogItemDto(cook.Id, cook.Name, cook.ArchivedAt))
            .ToListAsync(cancellationToken);

        return new CatalogListDto(cooks);
    }

    public async Task<(int Status, string? Code, string? Message, CatalogItemDto? Value)> CreateCookAsync(
        CatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateName(request.Name, out var name);
        if (validation is not null)
        {
            return (validation.Status, validation.Code, validation.Message, null);
        }

        var normalizedName = name.ToLower();
        var duplicate = await db.FeloganCooks.AnyAsync(
            cook => cook.ArchivedAt == null && cook.Name.ToLower() == normalizedName,
            cancellationToken);
        if (duplicate)
        {
            return (409, "NAME_TAKEN", "An active cook with that name already exists.", null);
        }

        var maxSortOrder = await db.FeloganCooks
            .Select(cook => (int?)cook.SortOrder)
            .MaxAsync(cancellationToken);
        var cook = new FeloganCook
        {
            Id = Guid.NewGuid(),
            Name = name,
            SortOrder = (maxSortOrder ?? -1) + 1
        };

        db.FeloganCooks.Add(cook);
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(cook));
    }

    public async Task<(int Status, string? Code, string? Message, CatalogItemDto? Value)> UpdateCookAsync(
        Guid id,
        CatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var cook = await db.FeloganCooks.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (cook is null)
        {
            return (404, "COOK_NOT_FOUND", "Cook not found.", null);
        }

        var validation = ValidateName(request.Name, out var name);
        if (validation is not null)
        {
            return (validation.Status, validation.Code, validation.Message, null);
        }

        if (cook.ArchivedAt is null)
        {
            var normalizedName = name.ToLower();
            var duplicate = await db.FeloganCooks.AnyAsync(
                candidate => candidate.Id != id
                    && candidate.ArchivedAt == null
                    && candidate.Name.ToLower() == normalizedName,
                cancellationToken);
            if (duplicate)
            {
                return (409, "NAME_TAKEN", "An active cook with that name already exists.", null);
            }
        }

        cook.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(cook));
    }

    public async Task<(int Status, string? Code, string? Message, CatalogItemDto? Value)> ArchiveCookAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cook = await db.FeloganCooks.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (cook is null)
        {
            return (404, "COOK_NOT_FOUND", "Cook not found.", null);
        }

        var invalidState = ValidateArchiveState(cook.ArchivedAt, archiving: true, "Cook");
        if (invalidState is not null)
        {
            return (invalidState.Status, invalidState.Code, invalidState.Message, null);
        }

        cook.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(cook));
    }

    public async Task<(int Status, string? Code, string? Message, CatalogItemDto? Value)> UnarchiveCookAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cook = await db.FeloganCooks.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (cook is null)
        {
            return (404, "COOK_NOT_FOUND", "Cook not found.", null);
        }

        var invalidState = ValidateArchiveState(cook.ArchivedAt, archiving: false, "Cook");
        if (invalidState is not null)
        {
            return (invalidState.Status, invalidState.Code, invalidState.Message, null);
        }

        var normalizedName = cook.Name.ToLower();
        var duplicate = await db.FeloganCooks.AnyAsync(
            candidate => candidate.Id != id
                && candidate.ArchivedAt == null
                && candidate.Name.ToLower() == normalizedName,
            cancellationToken);
        if (duplicate)
        {
            return (409, "NAME_TAKEN", "An active cook with that name already exists.", null);
        }

        cook.ArchivedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(cook));
    }

    public async Task<MenuItemListDto> ListMenuItemsAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var query = db.FeloganMenuItems.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(item => item.ArchivedAt == null);
        }

        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new MenuItemDto(
                item.Id,
                item.Name,
                item.Kind,
                item.SellPrice,
                item.Cost,
                item.ArchivedAt))
            .ToListAsync(cancellationToken);

        return new MenuItemListDto(items);
    }

    public async Task<(int Status, string? Code, string? Message, MenuItemDto? Value)> CreateMenuItemAsync(
        MenuItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateMenuItem(request, out var name);
        if (validation is not null)
        {
            return (validation.Status, validation.Code, validation.Message, null);
        }

        var normalizedName = name.ToLower();
        var duplicate = await db.FeloganMenuItems.AnyAsync(
            item => item.ArchivedAt == null && item.Name.ToLower() == normalizedName,
            cancellationToken);
        if (duplicate)
        {
            return (409, "NAME_TAKEN", "An active menu item with that name already exists.", null);
        }

        var maxSortOrder = await db.FeloganMenuItems
            .Select(item => (int?)item.SortOrder)
            .MaxAsync(cancellationToken);
        var item = new FeloganMenuItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = request.Kind,
            SellPrice = request.SellPrice,
            Cost = request.Cost,
            SortOrder = (maxSortOrder ?? -1) + 1
        };

        db.FeloganMenuItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(item));
    }

    public async Task<(int Status, string? Code, string? Message, MenuItemDto? Value)> UpdateMenuItemAsync(
        Guid id,
        MenuItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await db.FeloganMenuItems.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (item is null)
        {
            return (404, "MENU_ITEM_NOT_FOUND", "Menu item not found.", null);
        }

        var validation = ValidateMenuItem(request, out var name);
        if (validation is not null)
        {
            return (validation.Status, validation.Code, validation.Message, null);
        }

        if (item.ArchivedAt is null)
        {
            var normalizedName = name.ToLower();
            var duplicate = await db.FeloganMenuItems.AnyAsync(
                candidate => candidate.Id != id
                    && candidate.ArchivedAt == null
                    && candidate.Name.ToLower() == normalizedName,
                cancellationToken);
            if (duplicate)
            {
                return (409, "NAME_TAKEN", "An active menu item with that name already exists.", null);
            }
        }

        item.Name = name;
        item.Kind = request.Kind;
        item.SellPrice = request.SellPrice;
        item.Cost = request.Cost;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(item));
    }

    public async Task<(int Status, string? Code, string? Message, MenuItemDto? Value)> ArchiveMenuItemAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await db.FeloganMenuItems.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (item is null)
        {
            return (404, "MENU_ITEM_NOT_FOUND", "Menu item not found.", null);
        }

        var invalidState = ValidateArchiveState(item.ArchivedAt, archiving: true, "Menu item");
        if (invalidState is not null)
        {
            return (invalidState.Status, invalidState.Code, invalidState.Message, null);
        }

        item.ArchivedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(item));
    }

    public async Task<(int Status, string? Code, string? Message, MenuItemDto? Value)> UnarchiveMenuItemAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await db.FeloganMenuItems.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (item is null)
        {
            return (404, "MENU_ITEM_NOT_FOUND", "Menu item not found.", null);
        }

        var invalidState = ValidateArchiveState(item.ArchivedAt, archiving: false, "Menu item");
        if (invalidState is not null)
        {
            return (invalidState.Status, invalidState.Code, invalidState.Message, null);
        }

        var normalizedName = item.Name.ToLower();
        var duplicate = await db.FeloganMenuItems.AnyAsync(
            candidate => candidate.Id != id
                && candidate.ArchivedAt == null
                && candidate.Name.ToLower() == normalizedName,
            cancellationToken);
        if (duplicate)
        {
            return (409, "NAME_TAKEN", "An active menu item with that name already exists.", null);
        }

        item.ArchivedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(item));
    }

    private static CatalogFailure? ValidateName(string? requestedName, out string name)
    {
        name = requestedName?.Trim() ?? "";
        return name.Length is 0 or > 64
            ? new CatalogFailure(400, "VALIDATION_ERROR", "Name must be between 1 and 64 characters.")
            : null;
    }

    private static CatalogFailure? ValidateMenuItem(MenuItemRequest request, out string name)
    {
        var nameFailure = ValidateName(request.Name, out name);
        if (nameFailure is not null)
        {
            return nameFailure;
        }

        if (request.Kind is not ("plate" or "extra"))
        {
            return new CatalogFailure(400, "VALIDATION_ERROR", "Kind must be plate or extra.");
        }

        return request.SellPrice < 0
            || request.Cost < 0
            || HasExtraDecimals(request.SellPrice)
            || HasExtraDecimals(request.Cost)
                ? new CatalogFailure(
                    400,
                    "VALIDATION_ERROR",
                    "Sell price and cost must be non-negative with at most 2 decimal places.")
                : null;
    }

    private static CatalogFailure? ValidateArchiveState(
        DateTimeOffset? archivedAt,
        bool archiving,
        string itemName)
    {
        if (archiving && archivedAt is not null)
        {
            return new CatalogFailure(409, "ALREADY_ARCHIVED", $"{itemName} is already archived.");
        }

        return !archiving && archivedAt is null
            ? new CatalogFailure(409, "NOT_ARCHIVED", $"{itemName} is not archived.")
            : null;
    }

    private static bool HasExtraDecimals(decimal value) => decimal.Round(value, 2) != value;

    private static CatalogItemDto ToDto(FeloganRoom room) =>
        new(room.Id, room.Name, room.ArchivedAt);

    private static CatalogItemDto ToDto(FeloganCook cook) =>
        new(cook.Id, cook.Name, cook.ArchivedAt);

    private static MenuItemDto ToDto(FeloganMenuItem item) =>
        new(item.Id, item.Name, item.Kind, item.SellPrice, item.Cost, item.ArchivedAt);

    private sealed record CatalogFailure(int Status, string Code, string Message);
}
