# GCash / load ledger API — design

Date: 2026-09-14  
Status: approved

## Goal

Replace the guesthouse **Daily Transaction** Google Sheet with an API the family’s mobile app can call. Staff encode GCash IN/OUT, load, and wallet top-ups live at the counter. The API stores rows, computes wallet balances, and suggests a service fee. This repo does **not** include the mobile UI.

Existing auth (`/v1/auth/*`, `/v1/users/current`, Admin/Staff) is unchanged.

## Wallets

Three PHP running balances, one pair of numbers for the whole business (not per staff):

| Key | Sheet name |
|-----|------------|
| `cashOnHand` | Cash on Hand |
| `gcash` | Gcash Wallet |
| `tpc` | TPC Wallet |

Balances are derived from an opening snapshot plus the sum of posted (non-voided) transaction deltas. Opening snapshot is seeded once (from current sheet totals or zeros) and only Admin can set it.

Timezone for “today” and `?date=` is **Asia/Manila**.

## Methods and deltas

`amount` > 0. `profit` ≥ 0. `profitInGcash` is ignored when profit is 0.

Let `feeCash = profit` when profit > 0 and `profitInGcash` is false, else 0.  
Let `feeGcash = profit` when profit > 0 and `profitInGcash` is true, else 0.

| `method` | Cash | GCash | TPC |
|----------|------|-------|-----|
| `cash_in` | +amount + feeCash | −amount + feeGcash | 0 |
| `cash_out` | −amount + feeCash | +amount + feeGcash | 0 |
| `cash_out_with_charge` | −amount + feeCash | +amount + feeGcash | 0 |
| `load_via_gcash` | +amount + feeCash | −amount + feeGcash | 0 |
| `load_via_tpc` | +amount + feeCash | +feeGcash | −amount |
| `top_up_gcash` | −amount | +amount | 0 |
| `top_up_tpc` | −amount | 0 | +amount |
| `add_cash_capital` | +amount | 0 | 0 |
| `expenses` | −amount | 0 | 0 |
| `bank_cash_deposit` | −amount | 0 | 0 |

Top-up / capital / expenses / bank: API forces `profit` to 0.

### Insufficient funds

Reject with `INSUFFICIENT_WALLET` if the post would make **cash on hand** or **GCash** negative. **TPC may go negative** (the sheet already does).

## Profit suggestion

Returned by `GET /v1/transaction-methods` and also as `suggestedProfit` on create if the client omits `profit`.

- `load_via_gcash`, `load_via_tpc`: ₱5 if amount < 100, else ₱10  
- `cash_in`, `cash_out`, `cash_out_with_charge`: ₱5 if amount < 100; else 2% of amount rounded to the nearest ₱10, minimum ₱10  
- All other methods: ₱0  

Client may send a different `profit`. Suggestion never overrides an explicit value.

## HTTP

All amounts are JSON numbers (PHP, 2 decimal places max). Auth: Bearer JWT.

| Method | Path | Roles | Notes |
|--------|------|-------|--------|
| GET | `/v1/wallets` | Staff, Admin | Current balances |
| GET | `/v1/transaction-methods` | Staff, Admin | Method list + suggestion rules (human labels) |
| GET | `/v1/transactions?date=YYYY-MM-DD` | Staff, Admin | Manila calendar day. Staff: own rows only. Admin: all |
| POST | `/v1/transactions` | Staff, Admin | Create + return row and wallets |
| POST | `/v1/transactions/{id}/void` | Admin | Soft-void; reverses deltas |
| PUT | `/v1/wallets/opening` | Admin | Set opening balances (once / rare correction) |

No Staff edit or delete. No public register of transactions.

### POST body

```json
{
  "method": "cash_in",
  "amount": 500.00,
  "profit": 10.00,
  "profitInGcash": false,
  "note": "",
  "occurredAt": "2026-09-14T13:05:00+08:00",
  "clientRequestId": "8b1c0c2e-2a2a-4d3e-9f11-2c6d1e8a1111"
}
```

- `method`, `amount`, `clientRequestId` required.  
- `profit` optional (use suggestion).  
- `profitInGcash` default false.  
- `note` optional, max 200 chars.  
- `occurredAt` optional; default now. Must be a valid instant; listed under Manila `date`.  
- `clientRequestId` UUID. Unique. Same id + same user → `200` with the original row (no second post). Same id + different user → `409` `CLIENT_REQUEST_CONFLICT`.

### Transaction row

```json
{
  "id": "...",
  "occurredAt": "2026-09-14T13:05:00+08:00",
  "method": "cash_in",
  "amount": 500.00,
  "profit": 10.00,
  "profitInGcash": false,
  "note": "",
  "inCharge": { "id": "...", "username": "mayeth" },
  "deltas": { "cashOnHand": 510.00, "gcash": -500.00, "tpc": 0 },
  "balancesAfter": { "cashOnHand": 40345.05, "gcash": 13959.35, "tpc": -602.20 },
  "voidedAt": null,
  "clientRequestId": "..."
}
```

### List response

```json
{
  "date": "2026-09-14",
  "items": [ "..." ],
  "totals": {
    "count": 12,
    "profit": 145.00,
    "byMethod": { "cash_in": { "count": 5, "amount": 2500.00, "profit": 50.00 } }
  },
  "wallets": { "cashOnHand": 40345.05, "gcash": 13959.35, "tpc": -602.20 }
}
```

Voided rows are omitted from list and totals unless `?includeVoided=true` (Admin).

### Wallets response

```json
{ "cashOnHand": 40345.05, "gcash": 13959.35, "tpc": -602.20 }
```

## Errors

Same envelope as the starter (`error.code`, `error.message`, `error.correlation_id`).

| Code | Status | When |
|------|--------|------|
| `VALIDATION_ERROR` | 400 | Bad method, amount ≤ 0, profit < 0, extra decimals, missing `clientRequestId` |
| `INSUFFICIENT_WALLET` | 409 | Cash or GCash would go negative |
| `CLIENT_REQUEST_CONFLICT` | 409 | `clientRequestId` reused by another user |
| `TRANSACTION_NOT_FOUND` | 404 | Void unknown id |
| `ALREADY_VOIDED` | 409 | Void twice |
| `FORBIDDEN` | 403 | Staff tries to void or set opening |

## Data

- `Transactions` table + `WalletOpenings` (single current opening, or dated openings if we need history — v1: one current opening row).  
- Indexes: `OccurredAt`, `InChargeUserId`, unique `ClientRequestId`.  
- EF migration. Neon. No Google Sheets.

## Tests

- Create cash-in updates wallets as in the table; GET wallets matches.  
- Suggested profit used when `profit` omitted; explicit profit wins.  
- Duplicate `clientRequestId` same user returns the same id.  
- Cash-out that would make cash negative → `INSUFFICIENT_WALLET`.  
- TPC load may make `tpc` negative.  
- Staff GET list does not include other staff’s rows; Admin does.  
- Staff void → 403; Admin void restores wallets.  
- `?date=` uses Manila, not UTC.

## Out of scope

- Mobile app  
- GCash / GXI / InstaPay APIs  
- Hotel bookings  
- Overview capital calendar  
- Sheet import (later Admin job)  
- Refresh tokens, role promotion  

## Done when

`dotnet test` covers the cases above; Staff can post a cash-in and see wallets move; your mobile app can be built against this contract without another backend.
