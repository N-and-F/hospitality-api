using Hospitality.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Api.Felogan;

public sealed class FeloganService(AppDbContext db)
{
    private static readonly Dictionary<string, string> SilogSudAnByPlate = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Danggit Silog"] = "Danggit",
        ["Longsilog"] = "Longganisa",
        ["Tocilog"] = "Tocino",
        ["Tosilog"] = "Tocino",
        ["Hotsilog"] = "Hotdog",
        ["Hamsilog"] = "Ham",
        ["Cornsilog"] = "Corned beef"
    };
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.Room,
            name,
            currentId: null,
            "room",
            cancellationToken);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.Room,
            name,
            room.Id,
            "room",
            cancellationToken,
            room.ArchivedAt is null);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.Room,
            room.Name,
            room.Id,
            "room",
            cancellationToken);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.Cook,
            name,
            currentId: null,
            "cook",
            cancellationToken);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.Cook,
            name,
            cook.Id,
            "cook",
            cancellationToken,
            cook.ArchivedAt is null);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.Cook,
            cook.Name,
            cook.Id,
            "cook",
            cancellationToken);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
        }

        cook.ArchivedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(cook));
    }

    public async Task<FeloganSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        new((await SettingsAsync(cancellationToken)).CookPercent);

    public async Task<(int Status, string? Code, string? Message, FeloganSettingsDto? Value)> UpdateSettingsAsync(
        FeloganSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CookPercent is not int cookPercent || cookPercent is < 0 or > 100)
        {
            return (400, "VALIDATION_ERROR", "Cook percent must be an integer from 0 to 100.", null);
        }

        var settings = await SettingsAsync(cancellationToken);
        settings.CookPercent = cookPercent;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, new FeloganSettingsDto(settings.CookPercent));
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
            .ToListAsync(cancellationToken);
        var remaining = await RemainingByItemIdAsync(items, cancellationToken);
        var cookPercent = await CookPercentAsync(cancellationToken);

        return new MenuItemListDto(items.Select(item => ToDto(item, remaining[item.Id], cookPercent)).ToArray());
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.MenuItem,
            name,
            currentId: null,
            "menu item",
            cancellationToken);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
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
            OpeningPacks = 0,
            SortOrder = (maxSortOrder ?? -1) + 1
        };

        db.FeloganMenuItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(item, 0, await CookPercentAsync(cancellationToken)));
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.MenuItem,
            name,
            item.Id,
            "menu item",
            cancellationToken,
            item.ArchivedAt is null);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
        }

        item.Name = name;
        item.Kind = request.Kind;
        item.SellPrice = request.SellPrice;
        item.Cost = request.Cost;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, await ToDtoAsync(item, cancellationToken));
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
        return (200, null, null, await ToDtoAsync(item, cancellationToken));
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

        var uniqueness = await ValidateUniqueActiveNameAsync(
            CatalogKind.MenuItem,
            item.Name,
            item.Id,
            "menu item",
            cancellationToken);
        if (uniqueness is not null)
        {
            return (uniqueness.Status, uniqueness.Code, uniqueness.Message, null);
        }

        item.ArchivedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, await ToDtoAsync(item, cancellationToken));
    }

    public async Task<(int Status, string? Code, string? Message, ServingDto? Value)> CreateServingAsync(
        CreateServingRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId is null || request.ClientRequestId == Guid.Empty)
        {
            return (400, "VALIDATION_ERROR", "clientRequestId is required.", null);
        }

        var existing = await db.FeloganServings
            .AsNoTracking()
            .Include(serving => serving.Room)
            .Include(serving => serving.Cook)
            .Include(serving => serving.Lines)
            .FirstOrDefaultAsync(
                serving => serving.ClientRequestId == request.ClientRequestId,
                cancellationToken);
        if (existing is not null)
        {
            return existing.CreatedByUserId == userId
                ? (200, null, null, ToDto(existing))
                : (409, "CLIENT_REQUEST_CONFLICT", "That clientRequestId was already used.", null);
        }

        if (request.Note is { Length: > 200 })
        {
            return (400, "VALIDATION_ERROR", "Note must be 200 characters or fewer.", null);
        }

        var settlement = ReadSettlement(
            request.AdditionalProfit,
            request.ExcessPaid,
            request.PaidCook,
            "no");
        if (settlement.Failure is not null)
        {
            return (settlement.Failure.Status, settlement.Failure.Code, settlement.Failure.Message, null);
        }

        var requestedLines = request.Lines?.Where(line => line.Qty != 0).ToArray() ?? [];
        if (requestedLines.Length == 0)
        {
            return (400, "VALIDATION_ERROR", "At least one serving line with a non-zero quantity is required.", null);
        }

        if (requestedLines.Any(line => line.Qty < 0))
        {
            return (400, "VALIDATION_ERROR", "Line quantities cannot be negative.", null);
        }

        if (requestedLines.Select(line => line.MenuItemId).Distinct().Count() != requestedLines.Length)
        {
            return (400, "VALIDATION_ERROR", "Menu item IDs must be unique.", null);
        }

        var room = await db.FeloganRooms.FirstOrDefaultAsync(
            candidate => candidate.Id == request.RoomId && candidate.ArchivedAt == null,
            cancellationToken);
        if (room is null)
        {
            return (404, "ROOM_NOT_FOUND", "Room not found.", null);
        }

        var cook = await db.FeloganCooks.FirstOrDefaultAsync(
            candidate => candidate.Id == request.CookId && candidate.ArchivedAt == null,
            cancellationToken);
        if (cook is null)
        {
            return (404, "COOK_NOT_FOUND", "Cook not found.", null);
        }

        var menuItemIds = requestedLines.Select(line => line.MenuItemId).Distinct().ToArray();
        var menuItems = await db.FeloganMenuItems
            .AsNoTracking()
            .Where(item => menuItemIds.Contains(item.Id) && item.ArchivedAt == null)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (menuItems.Count != menuItemIds.Length)
        {
            return (404, "MENU_ITEM_NOT_FOUND", "Menu item not found.", null);
        }

        var serving = new FeloganServing
        {
            Id = Guid.NewGuid(),
            ServedOn = request.ServedOn is null ? ManilaToday() : request.ServedOn.Value,
            RoomId = room.Id,
            CookId = cook.Id,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            AdditionalProfit = settlement.AdditionalProfit,
            ExcessPaid = settlement.ExcessPaid,
            PaidCook = settlement.PaidCook,
            PaidSham = "na",
            CookPercent = await CookPercentAsync(cancellationToken),
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            ClientRequestId = request.ClientRequestId.Value,
            Room = room,
            Cook = cook,
            Lines = requestedLines.Select(line =>
            {
                var item = menuItems[line.MenuItemId];
                return new FeloganServingLine
                {
                    Id = Guid.NewGuid(),
                    MenuItemId = item.Id,
                    SnapshotName = item.Name,
                    SnapshotKind = item.Kind,
                    SnapshotSellPrice = item.SellPrice,
                    SnapshotCost = item.Cost,
                    Qty = line.Qty
                };
            }).ToArray()
        };

        db.FeloganServings.Add(serving);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var replay = await db.FeloganServings
                .AsNoTracking()
                .Include(candidate => candidate.Room)
                .Include(candidate => candidate.Cook)
                .Include(candidate => candidate.Lines)
                .FirstOrDefaultAsync(
                    candidate => candidate.ClientRequestId == request.ClientRequestId,
                    cancellationToken);
            if (replay is not null && replay.CreatedByUserId == userId)
            {
                return (200, null, null, ToDto(replay));
            }

            return (409, "CLIENT_REQUEST_CONFLICT", "That clientRequestId was already used.", null);
        }

        return (200, null, null, ToDto(serving));
    }

    public async Task<(int Status, string? Code, string? Message, ServingDto? Value)> UpdateServingAsync(
        Guid id,
        UpdateServingRequest request,
        CancellationToken cancellationToken = default)
    {
        var serving = await db.FeloganServings
            .Include(candidate => candidate.Room)
            .Include(candidate => candidate.Cook)
            .Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (serving is null)
        {
            return (404, "SERVING_NOT_FOUND", "Serving not found.", null);
        }

        if (request.Note is { Length: > 200 })
        {
            return (400, "VALIDATION_ERROR", "Note must be 200 characters or fewer.", null);
        }

        var settlement = ReadSettlement(
            request.AdditionalProfit,
            request.ExcessPaid,
            request.PaidCook,
            serving.PaidCook);
        if (settlement.Failure is not null)
        {
            return (settlement.Failure.Status, settlement.Failure.Code, settlement.Failure.Message, null);
        }

        var requestedLines = request.Lines?.Where(line => line.Qty != 0).ToArray() ?? [];
        if (requestedLines.Length == 0)
        {
            return (400, "VALIDATION_ERROR", "At least one serving line with a non-zero quantity is required.", null);
        }

        if (requestedLines.Any(line => line.Qty < 0))
        {
            return (400, "VALIDATION_ERROR", "Line quantities cannot be negative.", null);
        }

        if (requestedLines.Select(line => line.MenuItemId).Distinct().Count() != requestedLines.Length)
        {
            return (400, "VALIDATION_ERROR", "Menu item IDs must be unique.", null);
        }

        var room = await db.FeloganRooms.FirstOrDefaultAsync(
            candidate => candidate.Id == request.RoomId
                && (candidate.ArchivedAt == null || candidate.Id == serving.RoomId),
            cancellationToken);
        if (room is null)
        {
            return (404, "ROOM_NOT_FOUND", "Room not found.", null);
        }

        var cook = await db.FeloganCooks.FirstOrDefaultAsync(
            candidate => candidate.Id == request.CookId
                && (candidate.ArchivedAt == null || candidate.Id == serving.CookId),
            cancellationToken);
        if (cook is null)
        {
            return (404, "COOK_NOT_FOUND", "Cook not found.", null);
        }

        var existingLines = serving.Lines.ToArray();
        var snapshots = existingLines
            .GroupBy(line => line.MenuItemId)
            .ToDictionary(group => group.Key, group => group.First());
        var newMenuItemIds = requestedLines
            .Select(line => line.MenuItemId)
            .Where(menuItemId => !snapshots.ContainsKey(menuItemId))
            .Distinct()
            .ToArray();
        var newMenuItems = await db.FeloganMenuItems
            .AsNoTracking()
            .Where(item => newMenuItemIds.Contains(item.Id) && item.ArchivedAt == null)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (newMenuItems.Count != newMenuItemIds.Length)
        {
            return (404, "MENU_ITEM_NOT_FOUND", "Menu item not found.", null);
        }

        serving.ServedOn = request.ServedOn is null ? ManilaToday() : request.ServedOn.Value;
        serving.RoomId = room.Id;
        serving.CookId = cook.Id;
        serving.Room = room;
        serving.Cook = cook;
        serving.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        serving.AdditionalProfit = settlement.AdditionalProfit;
        serving.ExcessPaid = settlement.ExcessPaid;
        serving.PaidCook = settlement.PaidCook;

        var availableExistingLines = existingLines
            .GroupBy(line => line.MenuItemId)
            .ToDictionary(group => group.Key, group => new Queue<FeloganServingLine>(group));
        var retainedLineIds = new HashSet<Guid>();
        foreach (var line in requestedLines)
        {
            if (availableExistingLines.TryGetValue(line.MenuItemId, out var matchingLines)
                && matchingLines.TryDequeue(out var existingLine))
            {
                existingLine.Qty = line.Qty;
                retainedLineIds.Add(existingLine.Id);
                continue;
            }

            var item = newMenuItems[line.MenuItemId];
            var newLine = new FeloganServingLine
            {
                Id = Guid.NewGuid(),
                MenuItemId = item.Id,
                SnapshotName = item.Name,
                SnapshotKind = item.Kind,
                SnapshotSellPrice = item.SellPrice,
                SnapshotCost = item.Cost,
                Qty = line.Qty
            };

            serving.Lines.Add(newLine);
        }

        foreach (var removedLine in existingLines.Where(line => !retainedLineIds.Contains(line.Id)))
        {
            serving.Lines.Remove(removedLine);
            db.FeloganServingLines.Remove(removedLine);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, ToDto(serving));
    }

    public async Task<(int Status, string? Code, string? Message, Guid? Id)> DeleteServingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var serving = await db.FeloganServings
            .Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == id,
                cancellationToken);
        if (serving is null)
        {
            return (404, "SERVING_NOT_FOUND", "Serving not found.", null);
        }

        db.FeloganServingLines.RemoveRange(serving.Lines);
        db.FeloganServings.Remove(serving);
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, serving.Id);
    }

    public async Task<(int Status, string? Code, string? Message, PurchaseDto? Value)> CreatePurchaseAsync(
        CreatePurchaseRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId is null || request.ClientRequestId == Guid.Empty)
        {
            return (400, "VALIDATION_ERROR", "clientRequestId is required.", null);
        }

        var existing = await db.FeloganPurchases
            .AsNoTracking()
            .Include(purchase => purchase.MenuItem)
            .FirstOrDefaultAsync(
                purchase => purchase.ClientRequestId == request.ClientRequestId,
                cancellationToken);
        if (existing is not null)
        {
            return existing.CreatedByUserId == userId
                ? (200, null, null, ToDto(existing))
                : (409, "CLIENT_REQUEST_CONFLICT", "That clientRequestId was already used.", null);
        }

        if (request.Note is { Length: > 200 })
        {
            return (400, "VALIDATION_ERROR", "Note must be 200 characters or fewer.", null);
        }

        if (request.Qty <= 0)
        {
            return (400, "VALIDATION_ERROR", "Quantity must be greater than zero.", null);
        }

        var menuItem = await db.FeloganMenuItems.FirstOrDefaultAsync(
            candidate => candidate.Id == request.MenuItemId,
            cancellationToken);
        if (menuItem is null)
        {
            return (404, "MENU_ITEM_NOT_FOUND", "Menu item not found.", null);
        }

        var purchase = new FeloganPurchase
        {
            Id = Guid.NewGuid(),
            PurchasedOn = request.PurchasedOn is null ? ManilaToday() : request.PurchasedOn.Value,
            MenuItemId = menuItem.Id,
            Qty = request.Qty,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            ClientRequestId = request.ClientRequestId.Value,
            MenuItem = menuItem
        };

        db.FeloganPurchases.Add(purchase);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var replay = await db.FeloganPurchases
                .AsNoTracking()
                .Include(candidate => candidate.MenuItem)
                .FirstOrDefaultAsync(
                    candidate => candidate.ClientRequestId == request.ClientRequestId,
                    cancellationToken);
            if (replay is not null && replay.CreatedByUserId == userId)
            {
                return (200, null, null, ToDto(replay));
            }

            return (409, "CLIENT_REQUEST_CONFLICT", "That clientRequestId was already used.", null);
        }

        return (200, null, null, ToDto(purchase));
    }

    public async Task<(int Status, string? Code, string? Message, Guid? Id)> DeletePurchaseAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var purchase = await db.FeloganPurchases.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (purchase is null)
        {
            return (404, "PURCHASE_NOT_FOUND", "Purchase not found.", null);
        }

        db.FeloganPurchases.Remove(purchase);
        await db.SaveChangesAsync(cancellationToken);
        return (200, null, null, purchase.Id);
    }

    public async Task<(int Status, string? Code, string? Message, PurchaseListDto? Value)> ListPurchasesAsync(
        string? date,
        string? range,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(date, out var day))
        {
            day = ManilaToday();
        }

        var period = (range ?? "day").Trim().ToLowerInvariant();
        if (period is not ("day" or "week" or "month"))
        {
            return (400, "VALIDATION_ERROR", "Range must be day, week, or month.", null);
        }

        var (from, exclusiveEnd) = PeriodBounds(day, period);
        var purchases = await db.FeloganPurchases
            .AsNoTracking()
            .Include(purchase => purchase.MenuItem)
            .Where(purchase => purchase.PurchasedOn >= from && purchase.PurchasedOn < exclusiveEnd)
            .OrderByDescending(purchase => purchase.PurchasedOn)
            .ThenByDescending(purchase => purchase.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = purchases.Select(ToDto).ToArray();
        return (200, null, null, new PurchaseListDto(
            day,
            period,
            items,
            new PurchaseListTotalsDto(items.Length, items.Sum(item => item.Qty))));
    }

    public async Task<(int Status, string? Code, string? Message, ServingListDto? Value)> ListServingsAsync(
        string? date,
        string? range,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(date, out var day))
        {
            day = ManilaToday();
        }

        var period = (range ?? "day").Trim().ToLowerInvariant();
        if (period is not ("day" or "week" or "month"))
        {
            return (400, "VALIDATION_ERROR", "Range must be day, week, or month.", null);
        }

        var (from, exclusiveEnd) = PeriodBounds(day, period);
        var servings = await db.FeloganServings
            .AsNoTracking()
            .Include(serving => serving.Room)
            .Include(serving => serving.Cook)
            .Include(serving => serving.Lines)
            .Where(serving => serving.ServedOn >= from && serving.ServedOn < exclusiveEnd)
            .OrderByDescending(serving => serving.ServedOn)
            .ThenByDescending(serving => serving.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = servings.Select(ToDto).ToArray();
        return (200, null, null, new ServingListDto(
            day,
            period,
            items,
            new ServingListTotalsDto(
                items.Length,
                items.Sum(item => item.Totals.Qty),
                items.Sum(item => item.Totals.Sell),
                items.Sum(item => item.Totals.Cost),
                items.Sum(item => item.Totals.Profit),
                items.Sum(item => item.Totals.Cook),
                items.Sum(item => item.Totals.Nf),
                items.Sum(item => item.Totals.House))));
    }

    private static CatalogFailure? ValidateName(string? requestedName, out string name)
    {
        name = requestedName?.Trim() ?? "";
        return name.Length is 0 or > 64
            ? new CatalogFailure(400, "VALIDATION_ERROR", "Name must be between 1 and 64 characters.")
            : null;
    }

    private async Task<CatalogFailure?> ValidateUniqueActiveNameAsync(
        CatalogKind catalog,
        string name,
        Guid? currentId,
        string itemName,
        CancellationToken cancellationToken,
        bool shouldValidate = true)
    {
        if (!shouldValidate)
        {
            return null;
        }

        var normalizedName = name.ToLower();
        var duplicate = catalog switch
        {
            CatalogKind.Room => await db.FeloganRooms.AnyAsync(
                item => item.Id != currentId
                    && item.ArchivedAt == null
                    && item.Name.ToLower() == normalizedName,
                cancellationToken),
            CatalogKind.Cook => await db.FeloganCooks.AnyAsync(
                item => item.Id != currentId
                    && item.ArchivedAt == null
                    && item.Name.ToLower() == normalizedName,
                cancellationToken),
            CatalogKind.MenuItem => await db.FeloganMenuItems.AnyAsync(
                item => item.Id != currentId
                    && item.ArchivedAt == null
                    && item.Name.ToLower() == normalizedName,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalog))
        };

        return duplicate
            ? new CatalogFailure(
                409,
                "NAME_TAKEN",
                $"An active {itemName} with that name already exists.")
            : null;
    }

    private static CatalogFailure? ValidateMenuItem(MenuItemRequest request, out string name)
    {
        var nameFailure = ValidateName(request.Name, out name);
        if (nameFailure is not null)
        {
            return nameFailure;
        }

        if (request.Kind is not ("plate" or "extra" or "stock"))
        {
            return new CatalogFailure(400, "VALIDATION_ERROR", "Kind must be plate, extra, or stock.");
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

    private static MenuItemDto ToDto(FeloganMenuItem item, int remainingPacks, int cookPercent)
    {
        var (profit, cook, nf, house) = ItemSplit(item.SellPrice, item.Cost, cookPercent);
        return new(
            item.Id,
            item.Name,
            item.Kind,
            item.SellPrice,
            item.Cost,
            item.OpeningPacks,
            remainingPacks,
            profit,
            cook,
            nf,
            house,
            item.ArchivedAt);
    }

    private static (decimal Profit, decimal Cook, decimal Nf, decimal House) ItemSplit(
        decimal sell,
        decimal cost,
        int cookPercent)
    {
        var profit = decimal.Round(sell - cost, 2, MidpointRounding.AwayFromZero);
        var cook = decimal.Round(profit * cookPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var nf = profit - cook;
        return (profit, cook, nf, nf + cost);
    }

    private async Task<int> CookPercentAsync(CancellationToken cancellationToken) =>
        (await SettingsAsync(cancellationToken)).CookPercent;

    private async Task<FeloganSettings> SettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.FeloganSettings.SingleOrDefaultAsync(cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new FeloganSettings { Id = Guid.NewGuid(), CookPercent = 40 };
        db.FeloganSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<MenuItemDto> ToDtoAsync(FeloganMenuItem item, CancellationToken cancellationToken)
    {
        var remaining = await RemainingByItemIdAsync([item], cancellationToken);
        return ToDto(item, remaining[item.Id], await CookPercentAsync(cancellationToken));
    }

    private async Task<Dictionary<Guid, int>> RemainingByItemIdAsync(
        IReadOnlyCollection<FeloganMenuItem> items,
        CancellationToken cancellationToken)
    {
        var ids = items.Select(item => item.Id).ToArray();
        var purchasedRows = await db.FeloganPurchases
            .AsNoTracking()
            .Where(purchase => ids.Contains(purchase.MenuItemId))
            .Select(purchase => new { purchase.MenuItemId, purchase.Qty })
            .ToListAsync(cancellationToken);
        var catalog = await db.FeloganMenuItems
            .AsNoTracking()
            .Select(item => new { item.Id, item.Name, item.Kind })
            .ToListAsync(cancellationToken);
        var extraIdByName = catalog
            .Where(item => item.Kind == "extra")
            .ToDictionary(item => item.Name, item => item.Id, StringComparer.OrdinalIgnoreCase);
        extraIdByName.TryGetValue("Egg", out var eggId);
        var sudAnByPlateId = new Dictionary<Guid, Guid>();
        foreach (var plate in catalog.Where(item => item.Kind == "plate"))
        {
            if (SilogSudAnByPlate.TryGetValue(plate.Name, out var extraName)
                && extraIdByName.TryGetValue(extraName, out var extraId))
            {
                sudAnByPlateId[plate.Id] = extraId;
            }
        }

        var servedRows = await db.FeloganServingLines
            .AsNoTracking()
            .Select(line => new { line.MenuItemId, line.Qty })
            .ToListAsync(cancellationToken);
        var purchased = purchasedRows
            .GroupBy(row => row.MenuItemId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Qty));
        var served = servedRows
            .GroupBy(row => row.MenuItemId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Qty));
        var sudAnFromSilog = new Dictionary<Guid, int>();
        var eggFromSilog = 0;
        foreach (var row in servedRows)
        {
            if (!sudAnByPlateId.TryGetValue(row.MenuItemId, out var extraId))
            {
                continue;
            }

            sudAnFromSilog[extraId] = sudAnFromSilog.GetValueOrDefault(extraId) + row.Qty;
            eggFromSilog += row.Qty;
        }

        return items.ToDictionary(
            item => item.Id,
            item =>
            {
                var consumed = served.GetValueOrDefault(item.Id);
                if (item.Kind == "extra")
                {
                    consumed += sudAnFromSilog.GetValueOrDefault(item.Id);
                    if (item.Id == eggId)
                    {
                        consumed += eggFromSilog;
                    }
                }

                return item.OpeningPacks
                    + purchased.GetValueOrDefault(item.Id)
                    - consumed;
            });
    }

    private static PurchaseDto ToDto(FeloganPurchase purchase) =>
        new(
            purchase.Id,
            purchase.PurchasedOn,
            new ServingCatalogItemDto(purchase.MenuItemId, purchase.MenuItem.Name),
            purchase.Qty,
            purchase.Note,
            purchase.CreatedAt,
            purchase.ClientRequestId);

    private static ServingDto ToDto(FeloganServing serving)
    {
        var lines = serving.Lines.Select(line => new ServingLineDto(
            line.MenuItemId,
            line.SnapshotName,
            line.SnapshotKind,
            line.Qty,
            line.SnapshotSellPrice,
            line.SnapshotCost,
            line.Qty * line.SnapshotSellPrice)).ToArray();
        var (cost, profit, cook, nf, house) = SettlementFrom(serving.Lines, serving.CookPercent);
        return new ServingDto(
            serving.Id,
            serving.ServedOn,
            new ServingCatalogItemDto(serving.RoomId, serving.Room.Name),
            new ServingCatalogItemDto(serving.CookId, serving.Cook.Name),
            serving.Note,
            lines,
            new ServingTotalsDto(
                lines.Sum(line => line.Qty),
                lines.Sum(line => line.SellTotal),
                cost,
                profit,
                cook,
                nf,
                house),
            serving.AdditionalProfit,
            serving.ExcessPaid,
            serving.PaidCook,
            serving.CookPercent,
            serving.CreatedAt,
            serving.ClientRequestId);
    }

    private static (decimal Cost, decimal Profit, decimal Cook, decimal Nf, decimal House) SettlementFrom(
        IEnumerable<FeloganServingLine> lines,
        int cookPercent)
    {
        var sell = lines.Sum(line => line.Qty * line.SnapshotSellPrice);
        var cost = decimal.Round(lines.Sum(line => line.Qty * line.SnapshotCost), 2, MidpointRounding.AwayFromZero);
        var (profit, cook, nf, house) = ItemSplit(sell, cost, cookPercent);
        return (cost, profit, cook, nf, house);
    }

    private static SettlementRead ReadSettlement(
        decimal additionalProfit,
        decimal excessPaid,
        string? paidCook,
        string defaultPaidCook)
    {
        if (additionalProfit < 0
            || excessPaid < 0
            || HasExtraDecimals(additionalProfit)
            || HasExtraDecimals(excessPaid))
        {
            return new SettlementRead(
                new CatalogFailure(
                    400,
                    "VALIDATION_ERROR",
                    "Additional profit and excess paid must be non-negative with at most 2 decimal places."),
                0,
                0,
                defaultPaidCook);
        }

        if (paidCook is { Length: > 0 } && !IsPaidFlag(paidCook))
        {
            return new SettlementRead(
                new CatalogFailure(400, "VALIDATION_ERROR", "Paid cook must be yes, na, or no."),
                additionalProfit,
                excessPaid,
                defaultPaidCook);
        }

        return new SettlementRead(
            null,
            additionalProfit,
            excessPaid,
            PaidOrDefault(paidCook, defaultPaidCook));
    }

    private static bool IsPaidFlag(string value) =>
        value.Trim().ToLowerInvariant() is "yes" or "na" or "no";

    private static string PaidOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private static DateOnly ManilaToday()
    {
        var manilaNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Ledger.LedgerService.Manila);
        return DateOnly.FromDateTime(manilaNow.DateTime);
    }

    private static (DateOnly From, DateOnly ExclusiveEnd) PeriodBounds(DateOnly day, string range) =>
        range switch
        {
            "week" => WeekBounds(day),
            "month" => (
                new DateOnly(day.Year, day.Month, 1),
                new DateOnly(day.Year, day.Month, 1).AddMonths(1)),
            _ => (day, day.AddDays(1))
        };

    private static (DateOnly From, DateOnly ExclusiveEnd) WeekBounds(DateOnly day)
    {
        var offset = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = day.AddDays(-offset);
        return (monday, monday.AddDays(7));
    }

    private enum CatalogKind
    {
        Room,
        Cook,
        MenuItem
    }

    private sealed record CatalogFailure(int Status, string Code, string Message);

    private sealed record SettlementRead(
        CatalogFailure? Failure,
        decimal AdditionalProfit,
        decimal ExcessPaid,
        string PaidCook);
}
