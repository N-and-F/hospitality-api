# Felogan Slice 2 Settlement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Breakdown v1 profit split and Paid Cook/Sham fields on each serving.

**Architecture:** Persist `AdditionalProfit`, `ExcessPaid`, `PaidCook`, `PaidSham` on `FeloganServings`. Compute `cost`/`profit`/`sham`/`cook`/`nf`/`house` in `ToDto` from line snapshots. Same `/v1/felogan/servings` routes.

**Tech Stack:** .NET 10, EF Core, xUnit `WebApplicationFactory`, `wwwroot`.

**Spec:** `docs/superpowers/specs/2026-09-16-felogan-settlement-design.md`

## Global Constraints

- Admin-only `/v1/felogan/*`. Staff `403` `FORBIDDEN`.
- Split from snapshot sell/cost; additionalProfit and excessPaid do not change the split.
- `cook` = round(profit × 0.40, 2 AwayFromZero); `sham` = round(profit × 0.10, 2); `nf` = profit − cook − sham; `house` = nf + cost.
- `paidCook` default `"no"`; `paidSham` default `"na"`; allowed `"yes"` \| `"na"` \| `"no"`.
- Money max 2 decimal places. No expenses. No remaining packs.
- Do not commit unless the user asked — this execution **does** commit once per slice as requested.
- Tests: `dotnet test Hospitality.Api.Tests`

## File map

- Modify: `Hospitality.Api/Data/FeloganServing.cs`
- Modify: `Hospitality.Api/Data/AppDbContext.cs`
- Modify: `Hospitality.Api/Felogan/FeloganDtos.cs`
- Modify: `Hospitality.Api/Felogan/FeloganService.cs`
- Modify: `Hospitality.Api.Tests/FeloganTests.cs`
- Modify: `Hospitality.Api/wwwroot/index.html`, `felogan.js`, `app.css`
- Create: EF migration `FeloganSettlement`

Locked types:

```csharp
public sealed record ServingTotalsDto(
    int Qty,
    decimal Sell,
    decimal Cost,
    decimal Profit,
    decimal Sham,
    decimal Cook,
    decimal Nf,
    decimal House);

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
```

Add to `CreateServingRequest` / `UpdateServingRequest`: `decimal AdditionalProfit`, `decimal ExcessPaid`, `string? PaidCook`, `string? PaidSham`.

Add to `ServingDto`: `decimal AdditionalProfit`, `decimal ExcessPaid`, `string PaidCook`, `string PaidSham`.

Static helper:

```csharp
private static (decimal Cost, decimal Profit, decimal Sham, decimal Cook, decimal Nf, decimal House) SettlementFrom(IEnumerable<FeloganServingLine> lines)
{
    var sell = lines.Sum(l => l.Qty * l.SnapshotSellPrice);
    var cost = decimal.Round(lines.Sum(l => l.Qty * l.SnapshotCost), 2, MidpointRounding.AwayFromZero);
    var profit = decimal.Round(sell - cost, 2, MidpointRounding.AwayFromZero);
    var cook = decimal.Round(profit * 0.40m, 2, MidpointRounding.AwayFromZero);
    var sham = decimal.Round(profit * 0.10m, 2, MidpointRounding.AwayFromZero);
    var nf = profit - cook - sham;
    return (cost, profit, sham, cook, nf, nf + cost);
}
```

---

### Task 1: Settlement API + migration

**Files:** entities, DTOs, service ToDto/create/update, migration, tests.

- [x] **Step 1: Write failing tests** in `FeloganTests.cs`:

```csharp
[Fact]
public async Task Admin_serving_totals_include_v1_profit_split()
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
    var totals = row.RootElement.GetProperty("totals");
    Assert.Equal(240.00m, totals.GetProperty("sell").GetDecimal());
    Assert.Equal(110.34m, totals.GetProperty("cost").GetDecimal());
    Assert.Equal(129.66m, totals.GetProperty("profit").GetDecimal());
    Assert.Equal(12.97m, totals.GetProperty("sham").GetDecimal());
    Assert.Equal(51.86m, totals.GetProperty("cook").GetDecimal());
    Assert.Equal(64.83m, totals.GetProperty("nf").GetDecimal());
    Assert.Equal(175.17m, totals.GetProperty("house").GetDecimal());
    Assert.Equal("no", row.RootElement.GetProperty("paidCook").GetString());
    Assert.Equal("na", row.RootElement.GetProperty("paidSham").GetString());
}

[Fact]
public async Task Additional_profit_does_not_change_cook_share()
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
        additionalProfit = 23m,
        excessPaid = 20m,
        clientRequestId = Guid.NewGuid()
    });
    created.EnsureSuccessStatusCode();
    using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    Assert.Equal(51.86m, row.RootElement.GetProperty("totals").GetProperty("cook").GetDecimal());
    Assert.Equal(23.00m, row.RootElement.GetProperty("additionalProfit").GetDecimal());
    Assert.Equal(20.00m, row.RootElement.GetProperty("excessPaid").GetDecimal());
}

[Fact]
public async Task Paid_cook_persists_and_rejects_unknown_value()
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
        lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
        paidCook = "yes",
        paidSham = "yes",
        clientRequestId = Guid.NewGuid()
    });
    created.EnsureSuccessStatusCode();
    using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    var id = row.RootElement.GetProperty("id").GetGuid();
    Assert.Equal("yes", row.RootElement.GetProperty("paidCook").GetString());

    var bad = await client.PutAsJsonAsync($"/v1/felogan/servings/{id}", new
    {
        servedOn = "2026-09-06",
        roomId,
        cookId,
        lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
        paidCook = "maybe"
    });
    Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    Assert.Equal("VALIDATION_ERROR", await ReadErrorCode(bad));
}
```

- [x] **Step 2:** `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~Admin_serving_totals_include_v1` — FAIL (missing `cost` on totals).

- [x] **Step 3:** Add entity columns, DTO fields, `SettlementFrom`, validate paid strings and money, migration `dotnet ef migrations add FeloganSettlement --project Hospitality.Api --startup-project Hospitality.Api`. List totals sum the new money fields.

- [x] **Step 4:** `dotnet test Hospitality.Api.Tests` — PASS.

- [x] **Step 5:** UI — serving form fields `#felogan-additional-profit`, `#felogan-excess-paid`, `#felogan-paid-cook`, `#felogan-paid-sham`; settlement readout `#felogan-settlement`; history columns Profit, Cook, Paid Cook. Cache-bust `?v=settlement`.

- [x] **Step 6:** Commit `feat(felogan): add serving settlement split`

## Spec coverage

| Spec | Task |
|------|------|
| Split formulas | 1 |
| additional/excess ignored in % | 1 |
| paidCook/paidSham | 1 |
| UI | 1 |
| No expenses | (no task) |
