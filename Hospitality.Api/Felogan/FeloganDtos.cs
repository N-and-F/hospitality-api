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
    int OpeningPacks,
    int RemainingPacks,
    decimal Profit,
    decimal Sham,
    decimal Cook,
    decimal Nf,
    decimal House,
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
    public decimal AdditionalProfit { get; set; }
    public decimal ExcessPaid { get; set; }
    public string? PaidCook { get; set; }
    public string? PaidSham { get; set; }
    public Guid? ClientRequestId { get; set; }
}

public sealed class UpdateServingRequest
{
    public DateOnly? ServedOn { get; set; }
    public Guid RoomId { get; set; }
    public Guid CookId { get; set; }
    public string? Note { get; set; }
    public IReadOnlyList<ServingLineRequest> Lines { get; set; } = [];
    public decimal AdditionalProfit { get; set; }
    public decimal ExcessPaid { get; set; }
    public string? PaidCook { get; set; }
    public string? PaidSham { get; set; }
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

public sealed record ServingTotalsDto(
    int Qty,
    decimal Sell,
    decimal Cost,
    decimal Profit,
    decimal Sham,
    decimal Cook,
    decimal Nf,
    decimal House);

public sealed record ServingDto(
    Guid Id,
    DateOnly ServedOn,
    ServingCatalogItemDto Room,
    ServingCatalogItemDto Cook,
    string? Note,
    IReadOnlyList<ServingLineDto> Lines,
    ServingTotalsDto Totals,
    decimal AdditionalProfit,
    decimal ExcessPaid,
    string PaidCook,
    string PaidSham,
    DateTimeOffset CreatedAt,
    Guid ClientRequestId);

public sealed record ServingListTotalsDto(
    int Count,
    int Qty,
    decimal Sell,
    decimal Cost,
    decimal Profit,
    decimal Sham,
    decimal Cook,
    decimal Nf,
    decimal House);

public sealed record ServingListDto(
    DateOnly Date,
    string Range,
    IReadOnlyList<ServingDto> Items,
    ServingListTotalsDto Totals);

public sealed class CreatePurchaseRequest
{
    public DateOnly? PurchasedOn { get; set; }
    public Guid MenuItemId { get; set; }
    public int Qty { get; set; }
    public string? Note { get; set; }
    public Guid? ClientRequestId { get; set; }
}

public sealed record PurchaseDto(
    Guid Id,
    DateOnly PurchasedOn,
    ServingCatalogItemDto Item,
    int Qty,
    string? Note,
    DateTimeOffset CreatedAt,
    Guid ClientRequestId);

public sealed record PurchaseListTotalsDto(int Count, int Qty);

public sealed record PurchaseListDto(
    DateOnly Date,
    string Range,
    IReadOnlyList<PurchaseDto> Items,
    PurchaseListTotalsDto Totals);
