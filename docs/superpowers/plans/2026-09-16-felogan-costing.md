# Felogan Slice 4 Costing Display Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show per-item profit and 40/10/50 split on catalog GET/UI from sell and cost.

**Architecture:** Same `SettlementFrom` rounding as slice 2 but qty 1 using catalog `SellPrice`/`Cost` in `MenuItemDto`. No new tables.

**Tech Stack:** same.

**Spec:** `docs/superpowers/specs/2026-09-16-felogan-costing-design.md`

## Global Constraints

- Reuse slice 2 rounding. Catalog unit shares need not equal qty × unit on a serving.
- PUT/POST menu body unchanged. Staff 403 unchanged.
- Tests: `dotnet test Hospitality.Api.Tests`

## File map

- Modify: `FeloganDtos.cs` `MenuItemDto`
- Modify: `FeloganService.cs` `ToDto(FeloganMenuItem)` and list projection
- Modify: `FeloganTests.cs`
- Modify: `felogan.js` catalog row details; cache-bust `?v=costing`

Extract shared:

```csharp
internal static (decimal Profit, decimal Sham, decimal Cook, decimal Nf, decimal House) ItemSplit(decimal sell, decimal cost)
{
    var profit = decimal.Round(sell - cost, 2, MidpointRounding.AwayFromZero);
    var cook = decimal.Round(profit * 0.40m, 2, MidpointRounding.AwayFromZero);
    var sham = decimal.Round(profit * 0.10m, 2, MidpointRounding.AwayFromZero);
    var nf = profit - cook - sham;
    return (profit, sham, cook, nf, nf + cost);
}
```

`MenuItemDto` adds `Profit`, `Sham`, `Cook`, `Nf`, `House`.

---

### Task 1: Catalog split API + UI

- [x] **Step 1: Failing test**

```csharp
[Fact]
public async Task Menu_item_includes_unit_costing_split()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeAdminAsync(client);
    var menu = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items");
    var tocilog = menu.GetProperty("items").EnumerateArray()
        .First(x => x.GetProperty("name").GetString() == "Tocilog");
    Assert.Equal(120.00m, tocilog.GetProperty("sellPrice").GetDecimal());
    Assert.Equal(55.17m, tocilog.GetProperty("cost").GetDecimal());
    Assert.Equal(64.83m, tocilog.GetProperty("profit").GetDecimal());
    Assert.Equal(6.48m, tocilog.GetProperty("sham").GetDecimal());
    Assert.Equal(25.93m, tocilog.GetProperty("cook").GetDecimal());
    Assert.Equal(32.42m, tocilog.GetProperty("nf").GetDecimal());
    Assert.Equal(87.59m, tocilog.GetProperty("house").GetDecimal());
}

[Fact]
public async Task Menu_cost_edit_does_not_change_existing_serving_split()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeAdminAsync(client);
    var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);
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
    var cookShare = row.RootElement.GetProperty("totals").GetProperty("cook").GetDecimal();
    var menuPut = await client.PutAsJsonAsync($"/v1/felogan/menu-items/{tocilogId}", new
    {
        name = "Tocilog",
        kind = "plate",
        sellPrice = 120m,
        cost = 60m
    });
    menuPut.EnsureSuccessStatusCode();
    var listed = await client.GetFromJsonAsync<JsonElement>(
        $"/v1/felogan/servings?date=2026-09-06");
    Assert.Equal(
        cookShare,
        listed.GetProperty("items")[0].GetProperty("totals").GetProperty("cook").GetDecimal());
}
```

- [x] **Step 2:** Run `Menu_item_includes_unit_costing_split` — FAIL.

- [x] **Step 3:** Fill `MenuItemDto` computed fields; catalog UI shows Profit and Cook 40%.

- [x] **Step 4:** Full test suite PASS.

- [x] **Step 5:** Commit `feat(felogan): show catalog costing split`

## Spec coverage

GET menu split numbers, catalog UI, snapshots unchanged.
