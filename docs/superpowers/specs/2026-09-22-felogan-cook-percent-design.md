# Felogan — cook share percentage

Date: 2026-09-22  
Status: approved  
Branch: current Felogan work (separate from prior UI polish)

**Prior specs:** slice 2 serving split; slice 4 catalog costing

## Goal

Admin sets a **global cook share %** on the Cooks settings page. New servings snapshot that rate. Changing the setting does **not** rewrite history. Sham is removed from the split: cook takes the configured share of profit, N&F takes the rest.

## Split

- `profit` = round(`sell − cost`, 2 AwayFromZero)
- `cook` = round(`profit × cookPercent / 100`, 2 AwayFromZero)
- `nf` = `profit − cook`
- `house` = `nf + cost`

No `sham` on menu, serving, or list totals. No `paidSham` on create/update/response. `PaidSham` column stays unused (no drop).

`cookPercent` is an integer **0–100**. Default **40**. At 40%, Tocilog (sell 120, cost 55.17) is profit 64.83, cook 25.93, nf **38.90**, house **94.07**. Two Tocilog: cook 51.86, nf **77.80**, house **188.14**.

Additional profit still does not change cook share.

## Persistence

- `FeloganSettings` — one row, `CookPercent` (int, default 40). Seed if missing.
- `FeloganServings.CookPercent` — copied from settings on **POST**. **PUT** keeps the snapshot. Existing rows migrate at 40.

## HTTP

Admin-only, same Felogan role gate.

`GET /v1/felogan/settings` → `{ "cookPercent": 40 }`

`PUT /v1/felogan/settings` body `{ "cookPercent": 35 }` → same. Invalid / non-integer / outside 0–100 → `400 VALIDATION_ERROR`. Staff → `403`.

Menu GET uses the **current** setting. Serving GET/list uses each row’s **snapshot**. Serving DTO includes `cookPercent`.

## UI

Cooks page: Cook share % number field above add-cook, Save share, green toast. Menu column header `Cook {n}%` from current setting.

## Tests

- GET settings defaults 40.
- PUT 35 → menu Tocilog cook uses 35%; serving created **before** the PUT still has 40% cook; serving created **after** uses 35%.
- PUT 101, −1 → 400.
- Staff GET/PUT settings 403.
- Existing split tests drop `sham` / `paidSham` and use the new nf/house numbers.

## Out of scope

Per-cook rates, per-serving override UI, dropping `PaidSham` column, making Sham adjustable.

## Done when

Admin can change cook % on Cooks, history stays put, Sham is gone from API split fields, and tests lock the numbers above.
