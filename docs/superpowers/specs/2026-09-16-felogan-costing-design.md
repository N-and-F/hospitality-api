# Felogan (slice 4) — costing display

Date: 2026-09-16  
Status: approved  
Branch: `feat/felogan-slice-1` (separate commit after slice 3)

**Prior specs:** slice 1 catalogs; slice 2 serving split  
**Sheet:** COSTING tab money columns only (not store prices)

## Goal

Show **per catalog item** the same rates as a serving, for quantity 1:

- `profit` = round(`sellPrice − cost`, 2)  
- `cook` = round(`profit × 0.40`, 2)  
- `sham` = round(`profit × 0.10`, 2)  
- `nf` = `profit − cook − sham`  
- `house` = `nf + cost`

Admin still **edits** only `name`, `kind`, `sellPrice`, `cost` (and archive). The split is computed. No One Stop / Prince / Others. No pack + rice + egg + coffee formula.

Serving-level totals stay on slice 2 responses. Do not duplicate that logic in a second serving endpoint. Catalog unit shares are **not** required to equal `qty ×` those shares on a serving (rounding).

## HTTP

GET `/v1/felogan/menu-items` (and includeArchived) each item includes:

```json
{
  "id": "...",
  "name": "Tocilog",
  "kind": "plate",
  "sellPrice": 120.00,
  "cost": 55.17,
  "profit": 64.83,
  "sham": 6.48,
  "cook": 25.93,
  "nf": 32.42,
  "house": 87.59,
  "archivedAt": null
}
```

(Plus `remainingPacks` / `openingPacks` if slice 3 already shipped.)

PUT/POST body unchanged. Response of create/update includes the same computed fields.

Kind `stock` with sell 0 cost 0 → all split zeros.

Rounding: same as slice 2 (`AwayFromZero`, `nf` is the remainder).

## UI

Catalog menu list: columns Name, Kind, Sell, Cost, Profit, Cook 40%. Cost remains editable on the add/edit form. Serving screens do not grow extra costing columns beyond slice 2.

## Tests

- GET Tocilog: sell 120, cost 55.17, profit 64.83, cook 25.93, sham 6.48, nf 32.42, house 87.59.  
- PUT cost to 60.00 → profit 60.00 and shares recompute; **existing servings unchanged** (snapshots).  
- Staff GET menu still 403.

## Out of scope

Component costing, store price lists, changing slice 2 serving formulas, expenses.

## Done when

Catalog shows the per-item split from sell/cost, tests lock Tocilog numbers, and serving snapshots still ignore later catalog edits.
