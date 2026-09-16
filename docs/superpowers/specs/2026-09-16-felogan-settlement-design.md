# Felogan (slice 2) — settlement

Date: 2026-09-16  
Status: approved  
Branch: `feat/felogan-slice-1` (separate commit from slices 3 and 4)

**Prior spec:** `docs/superpowers/specs/2026-09-15-felogan-design.md`  
**Sheet:** Breakdown v1 (not Old Breakdown)

## Goal

Show and save the Breakdown v1 money footer **on each serving**: profit split Sham 10% / COOK 40% / N&F 50%, house `50% + cost`, plus Additional Profit, Excess Paid, Paid Cook, and Paid Sham.

Admin-only. No expenses. No remaining packs (slice 3). No catalog costing columns (slice 4).

## Split (computed, never stored as source of truth)

Use snapshot line sell and cost already on the serving.

- `sell` = sum(`qty × snapshotSellPrice`) — already in slice 1 totals  
- `cost` = sum(`qty × snapshotCost`)  
- `profit` = round(`sell − cost`, 2)  
- `cook` = round(`profit × 0.40`, 2)  
- `sham` = round(`profit × 0.10`, 2)  
- `nf` = `profit − cook − sham` (so the three shares always sum to profit)  
- `house` = `nf + cost`

Rounding is 2 decimal places, away from zero (`MidpointRounding.AwayFromZero`). Money fields max 2 decimal places.

**Additional Profit** and **Excess Paid** are recorded amounts only. They do **not** change sell, cost, profit, or the 40/10/50 split.

Compute the split from **serving** sell/cost totals, not from `qty ×` the catalog’s per-item split (slice 4). Rounding once at serving totals can differ from `qty ×` rounded unit shares by ₱0.01.

## Admin fields on the serving

| Field | Type | Default | Notes |
|--------|------|---------|--------|
| `additionalProfit` | decimal | 0 | ≥ 0, ≤ 2 decimal places |
| `excessPaid` | decimal | 0 | ≥ 0, ≤ 2 decimal places |
| `paidCook` | string | `"no"` | `"yes"` \| `"na"` \| `"no"` |
| `paidSham` | string | `"na"` | `"yes"` \| `"na"` \| `"no"` (sheet is N/A on most columns) |

PUT may change these. Create: omit → defaults.

## HTTP

Same routes: `POST/PUT/GET /v1/felogan/servings`. Admin JWT. Staff → `403` `FORBIDDEN`.

POST/PUT body (slice 1 fields unchanged) plus:

```json
{
  "additionalProfit": 0,
  "excessPaid": 0,
  "paidCook": "no",
  "paidSham": "na"
}
```

Serving response `totals` becomes:

```json
{
  "qty": 2,
  "sell": 240.00,
  "cost": 110.34,
  "profit": 129.66,
  "sham": 12.97,
  "cook": 51.86,
  "nf": 64.83,
  "house": 175.17
}
```

Plus top-level `additionalProfit`, `excessPaid`, `paidCook`, `paidSham`.

List `totals` sums `qty`, `sell`, `cost`, `profit`, `sham`, `cook`, `nf`, `house` across items in the range (not additionalProfit / excessPaid unless we add them later — **do not** include those in list totals).

Bad `paidCook` / `paidSham` or extra decimals → `400` `VALIDATION_ERROR`. Negative additional/excess → `400` `VALIDATION_ERROR`.

## Data

On `FeloganServings`: `AdditionalProfit`, `ExcessPaid` (precision 18,2), `PaidCook` (string max 8), `PaidSham` (string max 8). Migration. No new table.

Computed split is not persisted; always derived from line snapshots.

## UI

New serving and Edit: the four Admin fields after the note. Show computed split after save and on Edit (read-only numbers).

History: keep Summary as stacked lines. Add columns or a compact money block: Sell, Profit, Cook, Paid Cook. Do not show per-line `cost` on the summary; cost may appear in the settlement block as a single serving cost total.

## Tests

xUnit, `ApiFactory`.

- Two Tocilog (sell 120, cost 55.17) → `totals.sell` 240.00, `cost` 110.34, `profit` 129.66, `sham` 12.97, `cook` 51.86, `nf` 64.83, `house` 175.17.
- Set `additionalProfit` 23 and `excessPaid` 20 → cook share **unchanged**.
- PUT `paidCook` `"yes"` persists; `"maybe"` → 400.
- Staff POST serving still 403.
- Menu price change after create does not change serving split (snapshots).

## Out of scope

Expenses, cash-received vs expected, remaining packs, inventory purchases, catalog % columns, Staff access, Counter ledger.

## Done when

Admin can record Paid Cook/Sham and extras on a ticket, see the v1 split from snapshots, and `dotnet test` covers the cases above.
