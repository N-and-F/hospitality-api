# Felogan (slice 1) — design

Date: 2026-09-15  
Status: approved

## Goal

Replace the guesthouse **FELOGAN** Google Sheet serving log with an Admin-only app in this same site. After login, Admin chooses **Felogan** or **Guesthouse Counter**. Staff still go straight to the Counter and cannot open Felogan.

This slice records **one room breakfast** (date, room, cook, menu quantities) and lets Admin maintain **variable** rooms, cooks, and menu. Later slices (not this spec) add daily settlement, inventory purchases, remaining packs, and Paid Cook / Paid Sham.

Existing auth, staff, and GCash ledger are unchanged except for the Admin chooser and a **Switch app** control.

## Later slices (not this spec)

The workbook is covered in this order. Step 1 is this document; later specs:

1. Chooser + menu + serving log (this document)
2. Settlement — `docs/superpowers/specs/2026-09-16-felogan-settlement-design.md`
3. Inventory — `docs/superpowers/specs/2026-09-16-felogan-inventory-design.md`
4. Costing display — `docs/superpowers/specs/2026-09-16-felogan-costing-design.md`

## Who

| Role | After login | Felogan API |
|------|-------------|-------------|
| Staff | Counter only. No chooser, no Switch, no Felogan nav. | `403` `FORBIDDEN` |
| Admin | Chooser every **login form submit**. Pick Felogan or Counter. | All `/v1/felogan/*` |

## Login, chooser, shells

Login stays at `/`. The subtitle is **Sign in** (not “Sign in to the counter”).

**Chooser (Admin only):** two large cards — **Felogan** and **Guesthouse Counter**. Shown after every successful login form submit, including after Sign out then Sign in. Not shown on a mid-session **refresh**: if the JWT is still in `sessionStorage` and the Admin was already in an app, stay there (`sessionStorage` key `hospitality.app` = `felogan` \| `counter`).

**Switch app:** Admin sidebar in both shells. Returns to the chooser **without** clearing the token. Clearing `hospitality.app` until they pick again. Staff never see it.

**Felogan shell:** same cream/teal phone layout. Brand **Felogan**. No wallet cards. Nav: New serving, Serving history, Catalog. Sign out + Switch app.

**Counter shell:** unchanged once chosen (wallets, transactions, Admin settings for accounts/wallets). Switch app added for Admin only.

The chooser is UI-only. No “current app” API.

## Serving model

One **serving** = one room breakfast:

- `servedOn` — Asia/Manila calendar date (default today)
- `roomId` — required; **create** must use an **active** room. **Update** may keep the serving’s current room even if it was archived later; switching to a different room still requires that room to be active.
- `cookId` — same rule as room
- `note` — optional, max 200 characters
- `lines` — one or more `{ menuItemId, qty }` with `qty` an integer **> 0**
- Same room may have multiple servings on the same day (no unique constraint on room + date)

Items with qty 0 are omitted; a serving with zero lines is rejected.

Each line **snapshots** `name`, `kind`, `sellPrice`, and `cost` from the catalog at create time. A later menu price edit does not change old servings.

On **update**: lines matched by `menuItemId` keep their existing snapshot prices and may keep an item that was archived after the serving was created; new `menuItemId`s must be **active** and snapshot from the catalog now; dropped lines are deleted. Qty may change on a kept line without changing snapshot prices.

List timezone and `?date=` / `range=day|week|month` match the ledger: Manila; week is Monday–Sunday; newest first (`servedOn` desc, then `createdAt` desc).

Slice 1 **shows** sell price × qty and serving sell total. It **stores** cost on the snapshot for slice 2. It does **not** show remaining packs, cash received, cook/N&F split, or Paid Cook / Paid Sham.

## Catalogs (variable lists)

Rooms, cooks, and menu items are **database rows**, not code enums. Seed data is a first fill only.

Admin can **add**, **rename**, **change menu kind/prices**, **archive**, and **unarchive**. Archive hides a row from new servings; existing servings keep the snapshot (and still resolve the FK). Hard-delete of catalog rows is not allowed.

Active names are unique **case-insensitively** within each catalog. Archiving frees the name.

Seed runs only when that catalog table has **zero rows**. It never overwrites Admin edits (including renamed seed rows).

### Seed rooms

F1, F2, F3, F4, N2, 301, 302, 303, 304, 305, 306

### Seed cooks

Lisa, Melly

Admin may add others (including a name like N/A) later.

### Seed menu

Kind `plate` or `extra` only. Prices in PHP. Cost is stored now from the FELOGAN tab (not the older “N & F Silog Breakfast” tab).

| Name | Kind | Sell | Cost |
|------|------|------|------|
| Danggit Silog | plate | 140.00 | 61.83 |
| Longsilog | plate | 120.00 | 48.50 |
| Tocilog | plate | 120.00 | 55.17 |
| Hotsilog | plate | 110.00 | 42.79 |
| Hamsilog | plate | 120.00 | 53.50 |
| Cornsilog | plate | 130.00 | 48.00 |
| Danggit | extra | 50.00 | 33.33 |
| Longganisa | extra | 30.00 | 20.00 |
| Tocino | extra | 30.00 | 26.67 |
| Hotdog | extra | 30.00 | 14.29 |
| Ham | extra | 30.00 | 25.00 |
| Corned beef | extra | 30.00 | 19.50 |
| Egg | extra | 15.00 | 8.50 |
| Rice | extra | 15.00 | 10.00 |
| Coffee stick | extra | 7.00 | 5.00 |

`sortOrder` follows this seed order. New rows append to the end. GET lists order by `sortOrder`, then name.

## HTTP

All `/v1/felogan/*` require Bearer JWT and role **Admin**. Staff → `403` `FORBIDDEN`. Amounts are JSON numbers, max 2 decimal places. Auth and error envelope unchanged.

### Catalogs

Same pattern for `rooms`, `cooks`, and `menu-items`.

| Method | Path | Notes |
|--------|------|--------|
| GET | `/v1/felogan/rooms` | Active only, unless `?includeArchived=true` |
| POST | `/v1/felogan/rooms` | `{ "name" }` |
| PUT | `/v1/felogan/rooms/{id}` | `{ "name" }` (archived rows may be renamed) |
| POST | `/v1/felogan/rooms/{id}/archive` | Sets `archivedAt` |
| POST | `/v1/felogan/rooms/{id}/unarchive` | Clears `archivedAt`; name must still be unique among active |

Cooks: same, body `{ "name" }`.

Menu items:

```json
{ "name": "Longsilog", "kind": "plate", "sellPrice": 120.00, "cost": 48.50 }
```

PUT may change name, kind, sellPrice, and cost. Price changes apply to **future** servings only.

Duplicate active name → `409` `NAME_TAKEN`.

Catalog GET/POST/PUT/archive/unarchive item:

```json
{ "id": "...", "name": "303", "archivedAt": null }
```

List: `{ "items": [ ... ] }`. Menu items also include `kind`, `sellPrice`, and `cost`. GET orders by `sortOrder`, then name.

### Servings

| Method | Path |
|--------|------|
| GET | `/v1/felogan/servings?date=YYYY-MM-DD&range=day\|week\|month` |
| POST | `/v1/felogan/servings` |
| PUT | `/v1/felogan/servings/{id}` |
| DELETE | `/v1/felogan/servings/{id}` |

`date` defaults to today Manila. `range` defaults to `day`.

### POST / PUT serving body

```json
{
  "servedOn": "2026-09-06",
  "roomId": "...",
  "cookId": "...",
  "note": "",
  "lines": [{ "menuItemId": "...", "qty": 2 }],
  "clientRequestId": "8b1c0c2e-2a2a-4d3e-9f11-2c6d1e8a1111"
}
```

- `servedOn`, `roomId`, `cookId`, `lines`, `clientRequestId` required on POST.
- PUT: same fields except `clientRequestId` is ignored (idempotency is create-only).
- `note` optional.
- `clientRequestId` UUID. Unique. Same id + same user → `200` with the original serving (no second insert). Same id + different user → `409` `CLIENT_REQUEST_CONFLICT`.

### Serving response

```json
{
  "id": "...",
  "servedOn": "2026-09-06",
  "room": { "id": "...", "name": "303" },
  "cook": { "id": "...", "name": "Lisa" },
  "note": "",
  "lines": [
    {
      "menuItemId": "...",
      "name": "Tocilog",
      "kind": "plate",
      "qty": 2,
      "sellPrice": 120.00,
      "cost": 55.17,
      "sellTotal": 240.00
    }
  ],
  "totals": { "qty": 2, "sell": 240.00 },
  "createdAt": "2026-09-06T07:15:00+08:00",
  "clientRequestId": "..."
}
```

`cost` is in the JSON for later slices; the Felogan UI in this slice does not display it.

### List response

```json
{
  "date": "2026-09-06",
  "range": "day",
  "items": [ "..." ],
  "totals": { "count": 3, "qty": 8, "sell": 980.00 }
}
```

## Errors

Same envelope as the rest of the API (`error.code`, `error.message`, `error.correlation_id`).

| Code | Status | When |
|------|--------|------|
| `VALIDATION_ERROR` | 400 | Missing room/cook, empty lines, qty ≤ 0, bad kind, extra decimals, missing `clientRequestId` on POST |
| `FORBIDDEN` | 403 | Staff on any Felogan route |
| `ROOM_NOT_FOUND` | 404 | Unknown room, or archived room used on **create** or as a **new** room on update |
| `COOK_NOT_FOUND` | 404 | Same rule as room |
| `MENU_ITEM_NOT_FOUND` | 404 | Unknown menu item, or archived item used as a **new** line |
| `SERVING_NOT_FOUND` | 404 | Unknown serving id |
| `NAME_TAKEN` | 409 | Active catalog name already exists |
| `CLIENT_REQUEST_CONFLICT` | 409 | `clientRequestId` reused by another user |
| `ALREADY_ARCHIVED` | 409 | Archive twice |
| `NOT_ARCHIVED` | 409 | Unarchive when already active |

Catalog GET by missing id is not required; list + archive/unarchive/PUT use the same `*_NOT_FOUND` codes.

## Data

EF Core entities on `AppDbContext`. Neon. New migration.

| Table | Columns |
|--------|---------|
| `FeloganRooms` | Id (Guid), Name, SortOrder (int), ArchivedAt (DateTimeOffset?) |
| `FeloganCooks` | Id, Name, SortOrder, ArchivedAt |
| `FeloganMenuItems` | Id, Name, Kind (string, max 16), SellPrice, Cost, SortOrder, ArchivedAt |
| `FeloganServings` | Id, ServedOn (date), RoomId, CookId, Note, CreatedByUserId, CreatedAt, ClientRequestId |
| `FeloganServingLines` | Id, ServingId, MenuItemId, SnapshotName, SnapshotKind, SnapshotSellPrice, SnapshotCost, Qty (int) |

Indexes:

- Unique `FeloganServings.ClientRequestId`
- `FeloganServings.ServedOn`
- Unique filtered (or application-enforced) active name per catalog: among rows with `ArchivedAt IS NULL`, `lower(Name)` unique
- Money columns precision `(18, 2)`
- Name max 64, note max 200, kind max 16

`MenuItemId` on lines remains so catalog edits still have a FK; display and totals always use snapshots.

Seed: hosted service (same pattern as Identity seed) or migration data — **empty-table insert only**.

## Web UI

Keep one origin. Split scripts so Counter and Felogan do not share one giant file:

- `index.html` — login, chooser, Counter shell, Felogan shell
- `app.js` — login, chooser, Switch, Counter
- `felogan.js` — Felogan screens
- Shared CSS (`app.css`)

**New serving:** date (default today Manila), room select (active), cook select (active), qty inputs grouped plates then extras (active items only), note, save.

**Serving history:** Daily / Weekly / Monthly + date picker; table columns Date, Room, Cook, Summary (e.g. `2× Tocilog, 1× Egg`), Sell total, Note (View modal if present), Edit, Delete. Confirm delete with the existing delete modal pattern.

**Catalog:** three lists (Rooms, Cooks, Menu) with add, edit, archive, unarchive (show archived when viewing that list). Menu edit includes kind, sell price, cost.

Counter Settings (My account, Wallet Balances, Add account, Accounts & roles) stay on the Counter. Felogan does not duplicate them.

## Tests

xUnit + `WebApplicationFactory` (same `ApiFactory`).

- Staff token on any `/v1/felogan/*` → 403 `FORBIDDEN`.
- Admin POST serving with two Tocilog → GET list `totals.qty` 2 and `totals.sell` 240 when price is 120.
- Admin changes Tocilog sell price to 200; previous serving `sellTotal` stays 240.
- Archive a room → GET rooms omits it; POST serving with that roomId → 404 `ROOM_NOT_FOUND`; `?includeArchived=true` still lists it.
- Unarchive restores it to active GET.
- Rename a seeded room; restart seed path does not rename it back (empty-table seed: second run with existing rows is a no-op).
- Duplicate active room name → 409 `NAME_TAKEN`.
- `?date=` + `range=week` uses Manila Monday–Sunday, not UTC.
- Duplicate `clientRequestId` same Admin returns the same serving id; different user → 409.
- PUT serving can change qty; snapshot prices on that line stay.
- DELETE serving → subsequent GET omits it; DELETE unknown id → 404.
- POST serving with no lines or qty 0 → 400 `VALIDATION_ERROR`.

## Layout

```
Hospitality.Api/
  Felogan/                 service + DTOs (mirror Ledger/)
  Endpoints/FeloganEndpoints.cs
  Data/Felogan*.cs         entities
  Data/Migrations/         new migration
  wwwroot/index.html       chooser + Felogan shell
  wwwroot/app.js           login, chooser, Counter
  wwwroot/felogan.js
Hospitality.Api.Tests/FeloganTests.cs
```

## Out of scope (this slice)

- Daily settlement, expenses, cash received, cook 40% / N&F 10% / 50%
- Inventory / remaining packs / store purchases
- Paid Cook / Paid Sham
- Google Sheet import
- Staff access to Felogan
- Changing Counter ledger behavior
- Hard-delete of rooms, cooks, or menu items

## Done when

Admin can sign in, choose Felogan or Guesthouse Counter, and switch without signing out. Staff never see Felogan. Admin can record, list, edit, and delete room breakfasts; add and archive rooms, cooks, and menu items. Seed lists are editable and are not fixed in code. `dotnet test` covers the cases above.
