namespace Hospitality.Api.Felogan;

public sealed class CatalogItemRequest
{
    public string Name { get; set; } = "";
}

public sealed record CatalogItemDto(Guid Id, string Name, DateTimeOffset? ArchivedAt);

public sealed record CatalogListDto(IReadOnlyList<CatalogItemDto> Items);

public sealed class MenuItemRequest
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public decimal SellPrice { get; set; }
    public decimal Cost { get; set; }
}

public sealed record MenuItemDto(
    Guid Id,
    string Name,
    string Kind,
    decimal SellPrice,
    decimal Cost,
    DateTimeOffset? ArchivedAt);

public sealed record MenuItemListDto(IReadOnlyList<MenuItemDto> Items);

public sealed class ServingLineRequest
{
    public Guid MenuItemId { get; set; }
    public int Qty { get; set; }
}

public sealed class CreateServingRequest
{
    public DateOnly? ServedOn { get; set; }
    public Guid RoomId { get; set; }
    public Guid CookId { get; set; }
    public string? Note { get; set; }
    public IReadOnlyList<ServingLineRequest> Lines { get; set; } = [];
    public Guid? ClientRequestId { get; set; }
}

public sealed class UpdateServingRequest
{
    public DateOnly? ServedOn { get; set; }
    public Guid RoomId { get; set; }
    public Guid CookId { get; set; }
    public string? Note { get; set; }
    public IReadOnlyList<ServingLineRequest> Lines { get; set; } = [];
}

public sealed record ServingCatalogItemDto(Guid Id, string Name);

public sealed record ServingLineDto(
    Guid MenuItemId,
    string Name,
    string Kind,
    int Qty,
    decimal SellPrice,
    decimal Cost,
    decimal SellTotal);

public sealed record ServingTotalsDto(int Qty, decimal Sell);

public sealed record ServingDto(
    Guid Id,
    DateOnly ServedOn,
    ServingCatalogItemDto Room,
    ServingCatalogItemDto Cook,
    string? Note,
    IReadOnlyList<ServingLineDto> Lines,
    ServingTotalsDto Totals,
    DateTimeOffset CreatedAt,
    Guid ClientRequestId);

public sealed record ServingListTotalsDto(int Count, int Qty, decimal Sell);

public sealed record ServingListDto(
    DateOnly Date,
    string Range,
    IReadOnlyList<ServingDto> Items,
    ServingListTotalsDto Totals);
