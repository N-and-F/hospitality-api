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
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 64)
        {
            return (400, "VALIDATION_ERROR", "Name must be between 1 and 64 characters.", null);
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

        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 64)
        {
            return (400, "VALIDATION_ERROR", "Name must be between 1 and 64 characters.", null);
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

        if (room.ArchivedAt is not null)
        {
            return (409, "ALREADY_ARCHIVED", "Room is already archived.", null);
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

        if (room.ArchivedAt is null)
        {
            return (409, "NOT_ARCHIVED", "Room is not archived.", null);
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

    private static CatalogItemDto ToDto(FeloganRoom room) =>
        new(room.Id, room.Name, room.ArchivedAt);
}
