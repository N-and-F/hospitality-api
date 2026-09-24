# Named Wallets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the Counter ledger into named wallet sets (Main, Spare, …), each with its own Cash / GCash / TPC and history, with Admin switching and Staff locked to one assigned set.

**Architecture:** `WalletSet` catalog (table `Wallets`) is the ledger tenant. `WalletId` on openings and transactions; `StaffWalletAssignments` for Staff only. `WalletSetService.ResolveAsync` picks the set; existing ledger math is unchanged, only filtered. Seed **Main** and migrate current rows onto it.

**Tech Stack:** .NET 10 ASP.NET Core Minimal APIs, EF Core Npgsql / InMemory tests, static Counter UI (`wwwroot`).

**Spec:** `docs/superpowers/specs/2026-09-23-named-wallets-design.md`

## Global Constraints

- Felogan stays shared; do not add `walletId` to Felogan routes.
- No transfers between wallets.
- Main cannot be deleted (`CANNOT_DELETE_MAIN`).
- Staff: exactly one wallet; Admin: none assigned, omit `walletId` → Main.
- Unique `LedgerTransactions.ClientRequestId` stays **global**.
- JSON camelCase; existing `{ error: { code, message } }` envelope.
- Do not commit unless asked.

## File map

| File | Responsibility |
|------|----------------|
| `Hospitality.Api/Data/WalletSet.cs` | Named pool: Id, Name, CreatedAt. Table `Wallets`. |
| `Hospitality.Api/Data/StaffWalletAssignment.cs` | Staff `UserId` → `WalletId`. |
| `Hospitality.Api/Data/WalletOpening.cs` | Add required `WalletId` (unique). |
| `Hospitality.Api/Data/LedgerTransaction.cs` | Add required `WalletId`. |
| `Hospitality.Api/Ledger/WalletNames.cs` | `Main = "Main"`. |
| `Hospitality.Api/Ledger/WalletSetDtos.cs` | Catalog DTOs. |
| `Hospitality.Api/Ledger/WalletSetService.cs` | List/create/rename/delete, Resolve, Assign, seed helper. |
| `Hospitality.Api/Ledger/WalletSetSeedHostedService.cs` | Ensure Main + opening; assign any Staff missing a wallet. |
| `Hospitality.Api/Endpoints/WalletSetEndpoints.cs` | `/v1/wallet-sets`. |
| `Hospitality.Api/Ledger/LedgerService.cs` | All reads/writes take `Guid walletId`. |
| `Hospitality.Api/Endpoints/LedgerEndpoints.cs` | Query `walletId`, resolve, then ledger. |
| `Hospitality.Api/Identity/UserService.cs` | Register → Main; create Staff requires wallet; role/wallet PATCH. |
| `Hospitality.Api/wwwroot/index.html`, `app.js`, `app.css` | Switcher, settings, assignment UI. |
| `Hospitality.Api.Tests/WalletSetTests.cs` | New API tests. |
| Existing `LedgerTests` / `StaffTests` / `CurrentUserTests` / `WebUiTests` | Keep green; add walletId where Staff create requires it. |

**Locked types (later tasks use these names):**

```csharp
public static class WalletNames { public const string Main = "Main"; }

public sealed record WalletSetDto(Guid Id, string Name);
public sealed record WalletSetListResponse(IReadOnlyList<WalletSetDto> Items);
public sealed class CreateWalletSetRequest { public string Name { get; set; } = ""; }
public sealed class RenameWalletSetRequest { public string Name { get; set; } = ""; }
public sealed class SetWalletRequest { public Guid WalletId { get; set; } }

public sealed record UserPublicDto(
    string Id, string Email, string Username, string[] Roles,
    string? AssignedWalletId, string? AssignedWalletName);

// WalletSetService
Task<IReadOnlyList<WalletSetDto>> ListAsync(string userId, bool isAdmin, CancellationToken ct = default);
Task<(int Status, string? Code, string? Message, WalletSetDto? Value)> CreateAsync(string name, CancellationToken ct = default);
Task<(int Status, string? Code, string? Message, WalletSetDto? Value)> RenameAsync(Guid id, string name, CancellationToken ct = default);
Task<(int Status, string? Code, string? Message)> DeleteAsync(Guid id, CancellationToken ct = default);
Task<(int Status, string? Code, string? Message, Guid WalletId)> ResolveAsync(string userId, bool isAdmin, Guid? requestedWalletId, CancellationToken ct = default);
Task<(int Status, string? Code, string? Message)> AssignStaffAsync(string userId, Guid walletId, CancellationToken ct = default);
Task ClearAssignmentAsync(string userId, CancellationToken ct = default);
Task<Guid> GetMainIdAsync(CancellationToken ct = default);
```

`ResolveAsync` on failure: `WalletId = Guid.Empty` and a code (`WALLET_NOT_FOUND`, `FORBIDDEN`, `WALLET_NOT_ASSIGNED`).

---

### Task 1: Schema, Main seed, list wallet-sets

**Files:**
- Create: `Hospitality.Api/Data/WalletSet.cs`, `StaffWalletAssignment.cs`, `Ledger/WalletNames.cs`, `Ledger/WalletSetDtos.cs`, `Ledger/WalletSetService.cs`, `Ledger/WalletSetSeedHostedService.cs`, `Endpoints/WalletSetEndpoints.cs`, `Hospitality.Api.Tests/WalletSetTests.cs`
- Modify: `AppDbContext.cs`, `WalletOpening.cs`, `LedgerTransaction.cs`, `Program.cs` (register service + hosted seed after Identity seed, `v1.MapWalletSetEndpoints()`)
- Create: EF migration `NamedWallets` (Npgsql backfill Main)
- Test: `WalletSetTests.cs`

**Interfaces:**
- Consumes: existing Identity seed, `AppDbContext`
- Produces: `WalletSet`, `StaffWalletAssignment`, `GET /v1/wallet-sets`, `WalletSetService.ListAsync` / `GetMainIdAsync`, seeded Main + opening

- [ ] **Step 1: Write the failing test**

```csharp
public sealed class WalletSetTests
{
    [Fact]
    public async Task Get_wallet_sets_includes_seeded_main()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var response = await client.GetAsync("/v1/wallet-sets");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToArray();
        Assert.Contains("Main", names);
    }
}
```

Copy `AuthorizeAdminAsync` from `LedgerTests` into this file (do not share a helper yet).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~Get_wallet_sets_includes_seeded_main --nologo`

Expected: FAIL (404 or missing route).

- [ ] **Step 3: Entities + DbContext**

```csharp
namespace Hospitality.Api.Data;

public sealed class WalletSet
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class StaffWalletAssignment
{
    public required string UserId { get; set; }
    public Guid WalletId { get; set; }
}
```

Add `WalletId` to `WalletOpening` and `LedgerTransaction`.

`OnModelCreating`:
- `WalletSet` → table `Wallets`; `Name` max 32; unique index on `Name` (`citext` not required — enforce uniqueness in service case-insensitively).
- `WalletOpening.WalletId` unique + FK Restrict.
- `LedgerTransaction.WalletId` FK Restrict + index.
- `StaffWalletAssignment` PK `UserId`, FK to `Wallets` Restrict.

- [ ] **Step 4: Migration**

`dotnet ef migrations add NamedWallets --project Hospitality.Api`

Then edit the `Up` method so existing DBs work:

1. Create `Wallets`.
2. Insert Main (`gen_random_uuid()`, `'Main'`, `now()`).
3. Add nullable `WalletId` on openings and transactions.
4. `UPDATE` both to Main’s id.
5. Alter `WalletId` not null + FKs + unique on openings.
6. Create `StaffWalletAssignments`.
7. Insert one row per Staff user (`AspNetUserRoles` ∩ `AspNetRoles.Name = 'Staff'`) pointing at Main.

`Down` drops the new table/columns (acceptable).

In-memory tests use `EnsureCreated` (no SQL backfill) — seed service must insert Main if the table is empty.

- [ ] **Step 5: WalletSetService list + seed**

`ListAsync`: Admin → all name-asc. Staff → assigned row only (empty list if none).

`WalletSetSeedHostedService.StartAsync`:
- If no wallet named Main (ordinal ignore case), insert one.
- If that wallet has no opening, insert `0/0/0`.
- For every user in role Staff with no `StaffWalletAssignments` row, assign Main.

Register hosted service **after** `IdentitySeedHostedService`.

- [ ] **Step 6: GET endpoint**

```csharp
group.MapGet("/wallet-sets", async (WalletSetService sets, ClaimsPrincipal user, HttpContext http) =>
{
    var userId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return ApiResults.Unauthorized(http);
    var items = await sets.ListAsync(userId, user.IsInRole(Roles.Admin));
    return Results.Ok(new WalletSetListResponse(items));
}).RequireAuthorization();
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~Get_wallet_sets_includes_seeded_main --nologo`

Expected: PASS.

- [ ] **Step 8: Commit only if asked**

---

### Task 2: Resolve active wallet and scope the ledger

**Files:**
- Modify: `WalletSetService.cs` (`ResolveAsync`, `AssignStaffAsync`, `GetMainIdAsync`)
- Modify: `LedgerService.cs` — every current/opening/list/create/update/delete/void filters by `walletId`
- Modify: `LedgerEndpoints.cs` — `Guid? walletId` query on all ledger routes except `/transaction-methods`
- Modify: `UserService.RegisterAsync` — after creating Staff, `AssignStaffAsync` to Main
- Modify: `UserPublicDto` + `ToPublicDtoAsync` (nulls for Admin)
- Test: `WalletSetTests.cs`, `CurrentUserTests.cs`; existing `LedgerTests` must still pass with omitted `walletId`

**Interfaces:**
- Consumes: Task 1 schema + seed
- Produces: `ResolveAsync`, scoped `LedgerService` methods taking `Guid walletId`, register → Main

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Register_staff_is_assigned_main()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    var register = await client.PostAsJsonAsync("/v1/auth/register", new
    {
        email = "mainstaff@example.com",
        username = "mainstaff",
        password = "StaffPass1!"
    });
    register.EnsureSuccessStatusCode();
    using var tokenDoc = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", tokenDoc.RootElement.GetProperty("access_token").GetString());

    var me = await client.GetFromJsonAsync<JsonElement>("/v1/users/current");
    Assert.Equal("Main", me.GetProperty("assignedWalletName").GetString());
    Assert.False(string.IsNullOrWhiteSpace(me.GetProperty("assignedWalletId").GetString()));
}

[Fact]
public async Task Staff_cannot_use_another_wallet_id()
{
    await using var factory = new ApiFactory();
    var admin = factory.CreateClient();
    await AuthorizeAdminAsync(admin);
    var spare = await admin.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" });
    // If create is not in this task yet, skip this test until Task 3 — prefer:
    // staff GET /v1/wallets?walletId={random guid} → 403 FORBIDDEN
}
```

For this task, use a random Guid (no Spare yet):

```csharp
[Fact]
public async Task Staff_unknown_wallet_query_is_forbidden()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeStaffAsync(client, "scope@example.com", "scopestaff");
    var response = await client.GetAsync($"/v1/wallets?walletId={Guid.NewGuid()}");
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

Admin omitted `walletId` still returns current (Main) balances — existing `LedgerTests.Cash_in_updates_wallets` covers this once scoped.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~Register_staff_is_assigned_main|FullyQualifiedName~Staff_unknown_wallet_query_is_forbidden --nologo`

Expected: FAIL.

- [ ] **Step 3: Resolve + assign**

```csharp
public async Task<(int Status, string? Code, string? Message, Guid WalletId)> ResolveAsync(
    string userId, bool isAdmin, Guid? requestedWalletId, CancellationToken cancellationToken = default)
{
    if (isAdmin)
    {
        var id = requestedWalletId ?? await GetMainIdAsync(cancellationToken);
        var exists = await db.WalletSets.AnyAsync(w => w.Id == id, cancellationToken);
        return exists
            ? (200, null, null, id)
            : (404, "WALLET_NOT_FOUND", "That wallet was not found.", Guid.Empty);
    }

    var assigned = await db.StaffWalletAssignments.AsNoTracking()
        .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
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
```

`AssignStaffAsync`: 404 if wallet missing; upsert assignment (Staff only — caller checks role).

- [ ] **Step 4: Scope LedgerService**

Change signatures to take `Guid walletId` on `GetWalletsAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ListAsync`, `VoidAsync`, `SetOpeningAsync`.

`CurrentWalletsAsync(walletId)`: opening `Where(o => o.WalletId == walletId)`; posted txns `Where(t => t.WalletId == walletId && t.VoidedAt == null)`.

Create: set `row.WalletId = walletId`. If no opening exists, treat base as zero (seed should have created one; create opening 0/0/0 on first use of a new set in Task 3).

Update/Delete/Void: if `row.WalletId != walletId` → `404` `TRANSACTION_NOT_FOUND`.

SetOpening: load opening by `WalletId`; posted deltas filtered by that wallet.

- [ ] **Step 5: LedgerEndpoints**

Add `Guid? walletId` to each handler. Resolve first; on error return `ApiResults.Error`. Pass resolved id into `LedgerService`.

`GET /wallets` and DELETE/void responses that return balances must call `GetWalletsAsync(resolvedId)`.

- [ ] **Step 6: Register assigns Main; DTO fields**

After successful Staff register, `await walletSets.AssignStaffAsync(user.Id, await walletSets.GetMainIdAsync())`.

`UserPublicDto` adds `AssignedWalletId`, `AssignedWalletName` (JSON `assignedWalletId`, `assignedWalletName`). Admin: both null.

Add `Assert.Equal("Main", … assignedWalletName)` to `Current_user_after_register_returns_staff_profile`.

- [ ] **Step 7: Run tests**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~LedgerTests|FullyQualifiedName~WalletSetTests|FullyQualifiedName~CurrentUserTests --nologo`

Expected: PASS (existing ledger tests omit `walletId` → Admin Main / Staff assigned Main).

- [ ] **Step 8: Commit only if asked**

---

### Task 3: Create / rename wallet-sets and prove isolation

**Files:**
- Modify: `WalletSetService.cs` (`CreateAsync`, `RenameAsync`), `WalletSetEndpoints.cs`
- Test: `WalletSetTests.cs`

**Interfaces:**
- Consumes: `ResolveAsync`, scoped ledger
- Produces: `POST/PUT /v1/wallet-sets`, new set opening 0/0/0

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Spare_wallet_is_independent_of_main()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeAdminAsync(client);
    await client.PutAsJsonAsync("/v1/wallets/opening", new { cashOnHand = 1000, gcash = 2000, tpc = 0 });

    var created = await client.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" });
    created.EnsureSuccessStatusCode();
    using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    var spareId = createdDoc.RootElement.GetProperty("id").GetGuid();

    await client.PostAsJsonAsync($"/v1/transactions?walletId={spareId}", new
    {
        method = "add_cash_capital",
        amount = 50,
        clientRequestId = Guid.NewGuid()
    });

    var spareWallets = await client.GetFromJsonAsync<JsonElement>($"/v1/wallets?walletId={spareId}");
    Assert.Equal(50, spareWallets.GetProperty("cashOnHand").GetDecimal());

    var mainWallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
    Assert.Equal(1000, mainWallets.GetProperty("cashOnHand").GetDecimal());
}

[Fact]
public async Task Duplicate_wallet_name_is_conflict()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeAdminAsync(client);
    var response = await client.PostAsJsonAsync("/v1/wallet-sets", new { name = "main" });
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
}

[Fact]
public async Task Staff_cannot_create_wallet_set()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeStaffAsync(client, "nowalletcreate@example.com", "nowalletcreate");
    var response = await client.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" });
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~Spare_wallet_is_independent_of_main --nologo`

Expected: FAIL (404 on POST).

- [ ] **Step 3: Create / rename**

Trim name; length 1–32 else `400` `VALIDATION_ERROR`. Duplicate ignore-case → `409` `NAME_TAKEN`.

Create: insert `WalletSet`, insert `WalletOpening` 0/0/0 for that id, return `WalletSetDto`.

Rename: 404 if missing; same uniqueness; Main **may** be renamed (spec does not forbid rename). Keep delete protection on the seeded row by storing Main’s **id** at seed time, not by name, **or** treat the original seeded id as undeletable. Spec says “Main cannot be deleted” — identify Main as the wallet seeded first: add `bool IsMain` on `WalletSet` default false, seed sets `IsMain = true`. **Do this in Task 1 if not already; if Main is identified only by name, renaming Main would make delete-protection miss. Add `IsMain` now.**

If Task 1 did not add `IsMain`, add it in this task with a small migration `WalletSetIsMain` (or extend NamedWallets if not applied yet). Unique: at most one `IsMain`.

PUT `/v1/wallet-sets/{id}` Admin-only. POST/PUT Admin-only.

- [ ] **Step 4: Run tests**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~WalletSetTests|FullyQualifiedName~LedgerTests --nologo`

Expected: PASS.

- [ ] **Step 5: Commit only if asked**

---

### Task 4: Staff assignment APIs

**Files:**
- Modify: `RegisterRequest.cs` (`Guid? WalletId`), `SetRoleRequest.cs` (`Guid? WalletId`), `UserService.cs`, `CurrentUserEndpoints.cs` (new PATCH wallet + pass walletId into `SetRoleAsync`)
- Modify: `StaffTests.cs` — every `POST /v1/staff` that creates **Staff** must send `walletId` (Main id from GET `/v1/wallet-sets`)
- Test: `WalletSetTests.cs`

**Interfaces:**
- Consumes: `AssignStaffAsync`, `ClearAssignmentAsync`, `GetMainIdAsync`
- Produces: `POST /v1/staff` Staff requires `walletId`; `PATCH /v1/users/{id}/wallet`; role PATCH wallet rules

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Create_staff_without_wallet_is_validation_error()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeAdminAsync(client);
    var created = await client.PostAsJsonAsync("/v1/staff", new
    {
        username = "nowallet",
        password = "StaffPass1!"
    });
    Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    Assert.Equal("WALLET_REQUIRED", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
}

[Fact]
public async Task Reassign_staff_keeps_old_transactions_on_old_wallet()
{
    await using var factory = new ApiFactory();
    var admin = factory.CreateClient();
    await AuthorizeAdminAsync(admin);
    var spare = await admin.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" });
    spare.EnsureSuccessStatusCode();
    var spareId = (await spare.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    var mainId = await MainIdAsync(admin);

    var created = await admin.PostAsJsonAsync("/v1/staff", new
    {
        email = "mover@example.com",
        username = "mover",
        password = "StaffPass1!",
        walletId = mainId
    });
    var staffId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

    var staff = factory.CreateClient();
    await AuthorizeExistingAsync(staff, "mover", "StaffPass1!");
    await staff.PostAsJsonAsync("/v1/transactions", new
    {
        method = "add_cash_capital",
        amount = 10,
        clientRequestId = Guid.NewGuid()
    });

    await admin.PatchAsJsonAsync($"/v1/users/{staffId}/wallet", new { walletId = spareId });

    var mainList = await admin.GetFromJsonAsync<JsonElement>($"/v1/transactions?walletId={mainId}&range=month");
    Assert.Equal(10, mainList.GetProperty("items")[0].GetProperty("amount").GetDecimal());

    await staff.PostAsJsonAsync("/v1/transactions", new
    {
        method = "add_cash_capital",
        amount = 7,
        clientRequestId = Guid.NewGuid()
    });
    var spareList = await admin.GetFromJsonAsync<JsonElement>($"/v1/transactions?walletId={spareId}&range=month");
    Assert.Equal(7, spareList.GetProperty("items")[0].GetProperty("amount").GetDecimal());
}
```

Also: create Admin account still works **without** `walletId`; demote Admin → Staff without `walletId` → `WALLET_REQUIRED`; promote Staff → Admin clears `assignedWalletId`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~Create_staff_without_wallet_is_validation_error --nologo`

Expected: FAIL (currently 200).

- [ ] **Step 3: Implement assignment**

`CreateStaffAsync`: if role is Staff, `request.WalletId` missing/empty → `400` `WALLET_REQUIRED`; else `AssignStaffAsync`. If role is Admin, do not assign (ignore walletId).

`SetRoleAsync(..., Guid? walletId)`: to Admin → `ClearAssignmentAsync`. To Staff → require walletId then assign.

`PATCH /v1/users/{id}/wallet`: Admin; load user; must be Staff else `400` `VALIDATION_ERROR` (“Only Staff can be assigned a wallet.”); `AssignStaffAsync`.

- [ ] **Step 4: Fix existing StaffTests**

Helper:

```csharp
private static async Task<Guid> MainWalletIdAsync(HttpClient client)
{
    var sets = await client.GetFromJsonAsync<JsonElement>("/v1/wallet-sets");
    return sets.GetProperty("items").EnumerateArray()
        .Single(e => e.GetProperty("name").GetString() == "Main")
        .GetProperty("id").GetGuid();
}
```

Add `walletId = await MainWalletIdAsync(client)` to every Staff create that is not `role = "Admin"`.

- [ ] **Step 5: Run tests**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~StaffTests|FullyQualifiedName~WalletSetTests|FullyQualifiedName~LedgerTests --nologo`

Expected: PASS.

- [ ] **Step 6: Commit only if asked**

---

### Task 5: Delete wallet (reassign to Main, drop history)

**Files:**
- Modify: `WalletSetService.DeleteAsync`, `WalletSetEndpoints.cs`
- Test: `WalletSetTests.cs`

**Interfaces:**
- Consumes: `IsMain`, assignments, transactions, openings
- Produces: `DELETE /v1/wallet-sets/{id}`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task Cannot_delete_main()
{
    await using var factory = new ApiFactory();
    var client = factory.CreateClient();
    await AuthorizeAdminAsync(client);
    var mainId = await MainIdAsync(client);
    var response = await client.DeleteAsync($"/v1/wallet-sets/{mainId}");
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal("CANNOT_DELETE_MAIN", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
}

[Fact]
public async Task Delete_spare_reassigns_staff_and_drops_its_history()
{
    await using var factory = new ApiFactory();
    var admin = factory.CreateClient();
    await AuthorizeAdminAsync(admin);
    var spareId = (await (await admin.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" }))
        .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    var mainId = await MainIdAsync(admin);

    var created = await admin.PostAsJsonAsync("/v1/staff", new
    {
        username = "orphan",
        password = "StaffPass1!",
        walletId = spareId
    });
    var staffId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

    await admin.PostAsJsonAsync($"/v1/transactions?walletId={spareId}", new
    {
        method = "add_cash_capital",
        amount = 25,
        clientRequestId = Guid.NewGuid()
    });
    await admin.PostAsJsonAsync("/v1/transactions", new
    {
        method = "add_cash_capital",
        amount = 3,
        clientRequestId = Guid.NewGuid()
    });

    var deleted = await admin.DeleteAsync($"/v1/wallet-sets/{spareId}");
    deleted.EnsureSuccessStatusCode();

    var me = await admin.GetFromJsonAsync<JsonElement>($"/v1/staff");
    var orphan = me.GetProperty("items").EnumerateArray()
        .Single(e => e.GetProperty("username").GetString() == "orphan");
    Assert.Equal(mainId.ToString(), orphan.GetProperty("assignedWalletId").GetString());

    var gone = await admin.GetAsync($"/v1/wallets?walletId={spareId}");
    Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

    var mainList = await admin.GetFromJsonAsync<JsonElement>($"/v1/transactions?range=month");
    Assert.Contains(mainList.GetProperty("items").EnumerateArray(),
        e => e.GetProperty("amount").GetDecimal() == 3);
    Assert.DoesNotContain(mainList.GetProperty("items").EnumerateArray(),
        e => e.GetProperty("amount").GetDecimal() == 25);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~Cannot_delete_main --nologo`

Expected: FAIL (404).

- [ ] **Step 3: DeleteAsync**

If missing → 404. If `IsMain` → `400` `CANNOT_DELETE_MAIN`.

Else in one SaveChanges:
1. For each assignment with that `WalletId`, set `WalletId` to Main’s id.
2. `RemoveRange` transactions with that `WalletId`.
3. Remove opening for that `WalletId`.
4. Remove the `WalletSet`.

Staff GET `/v1/wallets` after delete uses Main (assignment updated).

- [ ] **Step 4: Run full API tests**

Run: `dotnet test Hospitality.Api.Tests --nologo`

Expected: PASS (UI tests may still fail until Task 6). If WebUiTests fail only on missing markup, that is Task 6.

- [ ] **Step 5: Commit only if asked**

---

### Task 6: Counter UI

**Files:**
- Modify: `Hospitality.Api/wwwroot/index.html`, `app.js`, `app.css`
- Modify: `Hospitality.Api.Tests/WebUiTests.cs`, `HealthTests.cs` if CSS assertions needed
- Cache-bust: `?v=named-wallets` on css/js

**Interfaces:**
- Consumes: `/v1/wallet-sets`, `?walletId=` on ledger paths, `assignedWalletId` on current user, PATCH wallet, POST staff `walletId`

- [ ] **Step 1: Write failing UI tests**

In `WebUiTests.Home_includes_chooser_felogan_shell_and_cache_busted_assets`:

```csharp
Assert.Contains("/app.js?v=named-wallets", html);
Assert.Contains("id=\"wallet-switch\"", html);
Assert.Contains("id=\"wallet-label\"", html);
Assert.Contains("data-settings-page=\"wallets\"", html);
Assert.Contains("id=\"account-wallet\"", html);
```

In scripts test:

```csharp
Assert.Contains("hospitality.walletId", app);
Assert.Contains("/v1/wallet-sets", app);
Assert.Contains("ledgerQuery", app);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Hospitality.Api.Tests --filter FullyQualifiedName~WebUiTests --nologo`

Expected: FAIL (old cache-bust / missing ids).

- [ ] **Step 3: HTML**

Counter sidebar brand: add `<select id="wallet-switch" hidden></select>` and `<p id="wallet-label" hidden></p>` under the brand name.

Settings menu (admin): **Wallets** item (`data-settings-page="wallets"`) with small “Named Cash / GCash / TPC sets”.

`data-settings-screen="wallets"`: heading, add form (`#wallet-set-name`, submit Add wallet), `#wallet-sets-error`, `#wallet-sets-list` (same item row as Felogan rooms: name + Edit + Delete; hide Delete on Main via `data-main`).

Add account: `<label id="account-wallet-field">Wallet <select id="account-wallet" required></select></label>` — hide when role is Admin.

Accounts list: show wallet name; Staff rows get `<select data-user-wallet>` like the role select.

- [ ] **Step 4: JS**

```javascript
const walletKey = "hospitality.walletId";
let walletSets = [];
let activeWalletId = "";

function isAdmin() { return (currentUser.roles || []).includes("Admin"); }

function ledgerQuery(path) {
  const url = new URL(path, window.location.origin);
  if (isAdmin() && activeWalletId) {
    url.searchParams.set("walletId", activeWalletId);
  }
  return url.pathname + url.search;
}
```

`loadApp`: `GET /v1/wallet-sets`, fill switcher. Admin: restore `sessionStorage` id if it exists in the list, else Main (`items.find(w => w.name === "Main")` or first). Staff: hide select, show `#wallet-label` with `currentUser.assignedWalletName`.

All `api("/v1/wallets")`, transactions GET/POST/PUT/DELETE, opening PUT use `ledgerQuery(...)`.

Switcher `change`: save sessionStorage, `refreshLedger()`.

Wallet settings: POST/PUT/DELETE `/v1/wallet-sets` with toasts (`Wallet saved.`, `Wallet updated.`, `Wallet deleted.`, errors). After delete, if `activeWalletId` was removed, switch to Main and refresh.

Create account: if Staff, send `walletId: account-wallet.value`.

Role change: if new role is Staff, include `walletId` from the row’s wallet select (keep current assignment if already Staff). If Admin, omit.

`change` on `[data-user-wallet]`: `PATCH /v1/users/${id}/wallet` + toast `Wallet assigned.` / error.

- [ ] **Step 5: CSS**

Reuse `.compact-form` / rooms list. Switcher: full width in sidebar under brand, same select styling as method. `#wallet-label` muted small text.

- [ ] **Step 6: Run tests**

Run: `dotnet test Hospitality.Api.Tests --nologo`

Expected: PASS, 0 failed.

- [ ] **Step 7: Manual click-through (dev server on 8080)**

1. Admin: Settings → Wallets → add Spare.
2. Header switch to Spare, save Add cash capital, switch to Main — histories differ.
3. Assign a Staff user to Spare, sign in as them — only Spare balances/history, no dropdown.
4. Delete Spare as Admin — that Staff now Main; Spare history gone.

- [ ] **Step 8: Commit only if asked**

---

## Spec coverage

| Spec requirement | Task |
|------------------|------|
| Wallets table, openings/txns `walletId`, Staff assignments | 1 |
| Seed Main, migrate existing, assign existing Staff | 1 (SQL + hosted seed) |
| Resolve rules (Staff assigned / Admin Main / 403 / 404 / 409) | 2 |
| Scoped GET/PUT wallets, transactions, opening | 2 |
| Register → Main; current user DTO fields | 2 |
| Catalog create/rename, isolation, NAME_TAKEN, Staff 403 mutate | 3 |
| `IsMain` so rename does not break delete guard | 3 |
| POST staff requires walletId; PATCH wallet; role + walletId | 4 |
| Delete Spare: reassign Main, drop history; cannot delete Main | 5 |
| Admin switcher, Staff label, settings, toasts | 6 |
| No Felogan-per-wallet, no transfers, no combined history | all (omitted) |

## Placeholder / type check

- `WalletSetService.ResolveAsync` and `UserPublicDto` names are used the same way in Tasks 2–6.
- `IsMain` is required so delete protection is not name-based.
- StaffTests / Felogan Admin-create-without-wallet stay valid (Admin omits `walletId`).
