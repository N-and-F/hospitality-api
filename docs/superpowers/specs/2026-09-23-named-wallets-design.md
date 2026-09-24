# Named wallets — design

Date: 2026-09-23  
Status: approved

## Goal

Split the Counter ledger into **named wallet sets**. Each set has its own Cash / GCash / TPC balances and its own transaction history. Admin creates, renames, deletes, and switches sets. Each Staff account uses exactly one set, chosen by Admin at create time and changeable later.

Felogan is unchanged and shared. There is **no** money transfer between wallets.

This replaces the current rule that the whole business has one opening and one history.

## Who

| Role | Wallet access |
|------|----------------|
| Admin | All wallets. Header dropdown switches which set Counter uses. Not assigned to a wallet. |
| Staff | Exactly one assigned wallet. No dropdown. Cannot see or post to any other set. |

## A wallet set

A **wallet** is a labeled pool, not a physical till: e.g. Main, Spare. It is not a person and not a location.

Fields: `id`, `name` (required, trim, 1–32 characters). Active names are unique **case-insensitively**.

**Main** is seeded and cannot be deleted. New wallets start at Cash / GCash / TPC `0` with empty history.

## Migration of existing data

On first migrate:

1. Insert wallet **Main**.
2. Attach the current `WalletOpenings` row to Main (or create opening `0/0/0` if none).
3. Stamp every existing `LedgerTransactions` row with Main.
4. Assign every existing **Staff** user to Main. Admins stay unassigned.

## Data model

`Wallets`: Id, Name, CreatedAt, `IsMain` (exactly one row true — the seeded Main). Rename is allowed; **delete** is refused when `IsMain` is true, not by name.

`WalletOpenings`: one row per wallet (`WalletId` unique). Same cash/gcash/tpc columns as today.

`LedgerTransactions`: required `WalletId`. Indexes stay (`OccurredAt`, `InChargeUserId`, unique `ClientRequestId` **globally** — a replay always returns the original row, even if the staff member was reassigned later). Listing and balance math filter by `WalletId`. Method deltas (including independent GCash/TPC top-ups) are unchanged.

`StaffWalletAssignments`: `UserId` (PK, Identity user) → `WalletId`. Present **only** for Staff. Admins have no row.

Reassigning a person does not move old transactions. Deletes of a wallet (below) do.

## Resolve active wallet

Ledger routes take optional query `walletId` (and the same field on POST/PUT bodies is not required; query is enough).

| Caller | `walletId` omitted | `walletId` present |
|--------|--------------------|--------------------|
| Staff | Assigned wallet | Must equal assigned; otherwise `403` `FORBIDDEN` |
| Admin | **Main** | That wallet, or `404` `WALLET_NOT_FOUND` |
| Staff with no assignment | `409` `WALLET_NOT_ASSIGNED` | same |

Insufficient funds, profit suggestion, and Manila `date`/`range` stay per the ledger spec, scoped to the active wallet.

## HTTP

Auth: Bearer JWT. JSON camelCase. Existing error envelope.

### Catalog (Admin, except GET)

| Method | Path | Roles | Notes |
|--------|------|-------|--------|
| GET | `/v1/wallet-sets` | Staff, Admin | Staff: one item (assigned). Admin: all, name asc. |
| POST | `/v1/wallet-sets` | Admin | `{ "name" }` → created set, opening 0/0/0 |
| PUT | `/v1/wallet-sets/{id}` | Admin | `{ "name" }` rename |
| DELETE | `/v1/wallet-sets/{id}` | Admin | See delete rules |

### Ledger (existing paths, now scoped)

| Method | Path | Roles | Notes |
|--------|------|-------|--------|
| GET | `/v1/wallets?walletId=` | Staff, Admin | Balances for the resolved wallet |
| PUT | `/v1/wallets/opening?walletId=` | Admin | Set opening for that wallet only |
| GET | `/v1/transactions?walletId=&date=&range=` | Staff, Admin | History for that wallet only. No combined all-wallets view. |
| POST | `/v1/transactions?walletId=` | Staff, Admin | Create on that wallet |
| PUT / DELETE / void | `/v1/transactions/{id}?walletId=` | as today | Row must belong to the resolved wallet; else `404` |

`GET /v1/transaction-methods` is unchanged (not per wallet).

### Staff assignment

`UserPublicDto` adds `assignedWalletId` (`string?`) and `assignedWalletName` (`string?`). Null for Admin.

`POST /v1/staff` (Admin): **Staff** requires `walletId`. **Admin** role must omit it (or it is ignored). Public `POST /v1/auth/register` (Staff) assigns **Main**.

`PATCH /v1/users/{id}/wallet` Admin-only: `{ "walletId" }`. Target must be Staff. `404` if user or wallet missing.

`PATCH /v1/users/{id}/role`: body `{ "role", "walletId"? }`. Promoting Staff → Admin **clears** assignment (`walletId` ignored). Demoting Admin → Staff **requires** `walletId`; missing → `400` `WALLET_REQUIRED`.

`GET /v1/users/current` includes the assignment fields so the Counter header can show the name.

## Delete wallet (Admin)

Never delete Main → `400` `CANNOT_DELETE_MAIN`.

Deleting any other wallet in one transaction:

1. Reassign every Staff on that wallet to **Main**.
2. Hard-delete that wallet’s transactions and opening.
3. Delete the wallet row.

If the Admin UI had that wallet selected, it falls back to Main.

## Counter UI

**Admin header:** wallet `<select>` (same chrome idea as Switch app). Changing it reloads balances + history. Remember last id in `sessionStorage` (`hospitality.walletId`). If the stored id was deleted, use Main.

**Staff header:** text of assigned wallet name, no select.

**Settings → Wallets** (Admin): list, add, rename, delete (not Main). Same list/table pattern as Felogan rooms.

**Add account:** Staff shows a required wallet dropdown. Admin role hides it.

**Accounts & roles:** show wallet name; Staff row can change wallet (existing toast on save).

**Wallet Balances:** still Admin-only; edits the **active** (switched) wallet.

Toasts on wallet create/rename/delete and on staff assignment save, success and error, matching the rest of the apps.

## Errors

| Code | Status | When |
|------|--------|------|
| `WALLET_NOT_FOUND` | 404 | Unknown id |
| `WALLET_REQUIRED` | 400 | Staff create/demote without `walletId` |
| `WALLET_NOT_ASSIGNED` | 409 | Staff user has no assignment |
| `FORBIDDEN` | 403 | Staff `walletId` ≠ assigned; Staff hitting Admin catalog mutate |
| `NAME_TAKEN` | 409 | Duplicate active wallet name |
| `CANNOT_DELETE_MAIN` | 400 | DELETE Main |
| `INSUFFICIENT_WALLET` | 409 | Unchanged, but only that wallet’s cash/GCash |

## Tests (minimum)

- Migrate: existing opening + txns + Staff live on Main.
- Create Spare: balances 0; posting there does not change Main history or balances.
- Staff assigned Main: `403` if they pass Spare; list/create only Main.
- Admin `?walletId=` Spare sees only Spare.
- Reassign Staff Main → Spare: old rows stay on Main; new posts go to Spare.
- PUT opening on Spare leaves Main opening untouched.
- DELETE Spare: its Staff now Main; Spare rows gone; Main rows remain.
- DELETE Main → `CANNOT_DELETE_MAIN`.
- POST staff Staff without `walletId` → `400`.
- Register Staff → Main.

## Out of scope

- Transfers between wallets
- Felogan per wallet
- Combined Admin history across all wallets
- Staff using more than one wallet
- Soft-archive of wallets (delete is hard, with the Main fallback above)

## UI click-through (after API tests)

Admin: create Spare, switch, save a txn, switch back to Main, confirm histories differ. Assign a Staff to Spare, sign in as them, confirm they only see Spare. Delete Spare, confirm that Staff now has Main and Spare history is gone.
