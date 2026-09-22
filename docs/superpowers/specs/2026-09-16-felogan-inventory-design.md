# Felogan (slice 3) — inventory

Date: 2026-09-16  
Status: approved  
Branch: `feat/felogan-slice-1` (separate commit after slice 2)

**Prior specs:** slice 1 serving log; slice 2 settlement  
**Sheet:** Inventory tab (purchases) + Breakdown v1 column Remaining Packs

## Goal

Log **stock purchases by date** and show **remaining packs** per catalog item. Remaining may go negative (Egg is −7 on the sheet).

Do **not** explode a silog into egg + rice + coffee. A serving decrements remaining only for the **lines on that ticket**.

## Catalog: `kind` includes `stock`

Allowed `kind` values: `"plate"` \| `"extra"` \| `"stock"`.

- **plate** and **extra** — New serving qty grid (unchanged grouping).  
- **stock** — Paper cups, fork, spoon, canton, and similar. Shown on Stock and as purchase targets. **Hidden** on New serving qty inputs. Sell/cost may be 0.

Active names still unique case-insensitively across **all** menu/stock rows (one catalog table).

### Seed remaining + stock rows

Empty-table menu seed (slice 1) is extended **only when the menu table is empty**. Include `remainingPacks` from Breakdown v1 column B:

| Name | Kind | Remaining |
|------|------|-----------|
| Danggit Silog | plate | 1 |
| Longsilog | plate | 7 |
| Tocilog | plate | 3 |
| Hotsilog | plate | 28 |
| Hamsilog | plate | 12 |
| Cornsilog | plate | 5 |
| Danggit | extra | 1 |
| Longganisa | extra | 7 |
| Tocino | extra | 3 |
| Hotdog | extra | 28 |
| Ham | extra | 12 |
| Corned beef | extra | 5 |
| Egg | extra | -7 |
| Rice | extra | 0 |
| Coffee stick | extra | 0 |
| Paper cups | stock | 0 |
| Fork | stock | 0 |
| Spoon | stock | 0 |
| Canton | stock | 0 |

If the menu table **already has rows** (slice 1 already seeded), do **not** overwrite remaining or insert stock rows automatically. Admin can add stock items and type remaining via catalog, or log purchases.

Catalog GET/PUT: `remainingPacks` is **read-only** on the item DTO (derived). Admin does not type remaining except through purchases and servings. (No “set remaining” endpoint.)

**Starting remaining** is stored as `OpeningPacks` on the menu row, set only at empty-table seed (or 0 for Admin-created rows). Derived remaining is not a separate mutable column.

## Remaining formula

For each menu/stock item:

`remainingPacks` = `openingPacks` + sum(purchase qty) − sum(serving line qty for this `menuItemId`)

Purchases and serving lines for archived items still count (history).

Serving **create**: subtract new line qtys.  
Serving **update**: apply the delta per `menuItemId`.  
Serving **delete**: add line qtys back.

Low remaining does **not** block saving a serving.

## HTTP

All Admin-only.

| Method | Path | Notes |
|--------|------|--------|
| GET | `/v1/felogan/purchases?date=&range=` | Same Manila `date`/`range` as servings. Default today / `day`. |
| POST | `/v1/felogan/purchases` | See body |
| DELETE | `/v1/felogan/purchases/{id}` | Hard delete; remaining goes back up |

GET menu-items (and `?includeArchived=true`) includes `remainingPacks` and `openingPacks`.

POST purchase:

```json
{
  "purchasedOn": "2026-09-06",
  "menuItemId": "...",
  "qty": 5,
  "note": "",
  "clientRequestId": "..."
}
```

- `purchasedOn` default today Manila.  
- `qty` integer > 0.  
- `menuItemId` must exist (archived allowed so you can record a late purchase; prefer active in the UI). Unknown id → `404` `MENU_ITEM_NOT_FOUND`.  
- `clientRequestId` required, same replay rules as servings.  
- `note` optional, max 200.

Purchase response:

```json
{
  "id": "...",
  "purchasedOn": "2026-09-06",
  "item": { "id": "...", "name": "Danggit Silog" },
  "qty": 5,
  "note": "",
  "createdAt": "...",
  "clientRequestId": "..."
}
```

List: `{ "date", "range", "items", "totals": { "count", "qty" } }`.

## Data

`FeloganMenuItems`: `OpeningPacks` (int, default 0).

`FeloganPurchases`: Id, PurchasedOn (date), MenuItemId, Qty (int), Note, CreatedByUserId, CreatedAt, ClientRequestId (unique). Index on PurchasedOn. FK to menu item Restrict.

Migration.

## UI

Felogan nav adds **Stock** (`data-felogan-nav="stock"`).

- Remaining table: name, kind, remaining (can be negative).  
- Purchase form: date, item (all kinds, active), qty, note.  
- Purchase history: day/week/month like servings. Delete with existing delete modal.

New serving qty labels may show remaining next to the name; still allow save at 0 or negative.

Catalog add-menu includes kind `stock`. Stock rows appear in Catalog so Admin can rename/archive.

## Tests

- Empty-table seed: Egg `remainingPacks` −7, Hotsilog 28, Paper cups 0.  
- POST purchase qty 5 on Tocilog → remaining increases by 5.  
- POST serving 2 Tocilog → remaining decreases by 2; DELETE serving restores them.  
- PUT serving qty 2 → 3 Tocilog → remaining decreases by 1 more.  
- Staff GET purchases → 403.  
- `kind: "stock"` item is in GET menu `?includeArchived=true` but serving form must not list it (assert in `felogan.js` / WebUiTests string or API: serving create with only a stock line is allowed by API if qty>0 — **UI omits them**. API allows stock lines if someone POSTs them; remaining still decrements. Prefer API allow; UI hide.)

## Out of scope

Recipe explosion, store (One Stop / Prince), expenses, unit conversions, Google Sheet import.

## Done when

Admin can log a purchase, see remaining move with purchases and servings, stock-only items stay off the breakfast form, and tests cover the remaining math.
