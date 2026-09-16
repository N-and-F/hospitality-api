# Felogan Slice 3 Inventory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stock purchases by date and remaining packs per catalog item.

**Architecture:** `OpeningPacks` on `FeloganMenuItem`. `FeloganPurchases` table. `remainingPacks` = opening + purchases − serving line qty. Kind `stock` hidden on serving qty grid.

**Tech Stack:** same as slice 2.

**Spec:** `docs/superpowers/specs/2026-09-16-felogan-inventory-design.md`

## Global Constraints

- Admin-only. Remaining may be negative. Servings do not block on low remaining.
- Empty-table menu seed sets opening/remaining from spec table including Paper cups, Fork, Spoon, Canton (`kind` `stock`). Already-seeded DBs are not overwritten.
- `qty` on purchases integer > 0. `clientRequestId` replay like servings.
- Kind allowed: `plate` \| `extra` \| `stock`.
- Tests: `dotnet test Hospitality.Api.Tests`

## File map

- Modify: `FeloganMenuItem.cs` (`OpeningPacks`)
- Create: `Hospitality.Api/Data/FeloganPurchase.cs`
- Modify: `AppDbContext`, `FeloganDtos`, `FeloganService`, `FeloganSeed`, `FeloganEndpoints`
- Modify: `FeloganTests.cs`, `wwwroot/*`
- Migration: `FeloganInventory`

```csharp
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
    string PurchasedOn,
    ServingCatalogItemDto Item,
    int Qty,
    string? Note,
    DateTimeOffset CreatedAt,
    Guid ClientRequestId);

public sealed record PurchaseListDto(
    string Date,
    string Range,
    IReadOnlyList<PurchaseDto> Items,
    PurchaseListTotalsDto Totals);

public sealed record PurchaseListTotalsDto(int Count, int Qty);
```

`MenuItemDto` adds `int OpeningPacks`, `int RemainingPacks`.

`FeloganService`:
- `RemainingPacks(item) = OpeningPacks + Sum(purchases.Qty) - Sum(servingLines.Qty)`
- `ListPurchasesAsync` / `CreatePurchaseAsync` / `DeletePurchaseAsync`
- `ValidateMenuItem` allows `stock`

---

### Task 1: Remaining + purchases API + Stock UI

- [x] **Step 1: Failing tests**

```csharp
[Fact]
public async Task Seed_sets_opening_remaining_including_negative_egg()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeAdminAsync(client);
    var menu = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items?includeArchived=true");
    var egg = menu.GetProperty("items").EnumerateArray()
        .First(x => x.GetProperty("name").GetString() == "Egg");
    Assert.Equal(-7, egg.GetProperty("remainingPacks").GetInt32());
    Assert.Equal(-7, egg.GetProperty("openingPacks").GetInt32());
    var cups = menu.GetProperty("items").EnumerateArray()
        .First(x => x.GetProperty("name").GetString() == "Paper cups");
    Assert.Equal("stock", cups.GetProperty("kind").GetString());
    Assert.Equal(0, cups.GetProperty("remainingPacks").GetInt32());
}

[Fact]
public async Task Purchase_and_serving_adjust_remaining()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeAdminAsync(client);
    var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);
    var before = (await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items"))
        .GetProperty("items").EnumerateArray()
        .First(x => x.GetProperty("id").GetGuid() == tocilogId)
        .GetProperty("remainingPacks").GetInt32();

    var purchase = await client.PostAsJsonAsync("/v1/felogan/purchases", new
    {
        purchasedOn = "2026-09-06",
        menuItemId = tocilogId,
        qty = 5,
        clientRequestId = Guid.NewGuid()
    });
    purchase.EnsureSuccessStatusCode();
    var afterBuy = (await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items"))
        .GetProperty("items").EnumerateArray()
        .First(x => x.GetProperty("id").GetGuid() == tocilogId)
        .GetProperty("remainingPacks").GetInt32();
    Assert.Equal(before + 5, afterBuy);

    var created = await client.PostAsJsonAsync("/v1/felogan/servings", new
    {
        servedOn = "2026-09-06",
        roomId,
        cookId,
        lines = new[] { new { menuItemId = tocilogId, qty = 2 } },
        clientRequestId = Guid.NewGuid()
    });
    created.EnsureSuccessStatusCode();
    using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    var servingId = row.RootElement.GetProperty("id").GetGuid();
    var afterServe = (await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items"))
        .GetProperty("items").EnumerateArray()
        .First(x => x.GetProperty("id").GetGuid() == tocilogId)
        .GetProperty("remainingPacks").GetInt32();
    Assert.Equal(before + 5 - 2, afterServe);

    (await client.DeleteAsync($"/v1/felogan/servings/{servingId}")).EnsureSuccessStatusCode();
    var afterDelete = (await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items"))
        .GetProperty("items").EnumerateArray()
        .First(x => x.GetProperty("id").GetGuid() == tocilogId)
        .GetProperty("remainingPacks").GetInt32();
    Assert.Equal(before + 5, afterDelete);
}

[Fact]
public async Task Staff_cannot_list_purchases()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeStaffAsync(client, "invstaff@example.com", "invstaff");
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/v1/felogan/purchases")).StatusCode);
}
```

- [x] **Step 2:** Run filter `Seed_sets_opening` — FAIL.

- [x] **Step 3:** Implement seed openings, remaining helper, purchases CRUD, `stock` kind, migration `FeloganInventory`. Serving qty grid filters `kind === "plate" || kind === "extra"`. Stock nav `#felogan-stock`. Cache-bust `?v=inventory`.

- [x] **Step 4:** Full `dotnet test Hospitality.Api.Tests` PASS.

- [x] **Step 5:** Commit `feat(felogan): add stock purchases and remaining packs`

## Spec coverage

Seed remaining table, purchases HTTP, remaining formula, stock kind hidden on serving form, Staff 403.
