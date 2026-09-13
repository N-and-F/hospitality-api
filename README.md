# hospitality-api

ASP.NET Core API for the Bantayan guesthouse counter: auth, staff, and the GCash / load Daily Transaction ledger. The mobile app is separate.

## Stack

- .NET 10
- PostgreSQL on [Neon](https://neon.tech) (free plan is $0/month, not a 30-day trial)
- ASP.NET Identity + JWT
- Deploy the API to [Render](https://render.com) (free web service)

Keep the database on Neon. Do not use Render Postgres.

## Local run

```bash
dotnet test
dotnet run --project Hospitality.Api
```

- Counter app: `http://localhost:8080/` (or the port in `.env`)
- Health: `GET /health`
- Swagger: `/docs`

Without `DATABASE_URL`, Development uses an in-memory store. Seeded admin:

- username: `admin`
- password: `AdminPass1!`

## Auth

| Method | Path | Who |
|--------|------|-----|
| POST | `/v1/auth/register` | public — creates **Staff** (`email`, `username`, `password`) |
| POST | `/v1/auth/login` | public — `username` + `password` |
| GET | `/v1/users/current` | Admin or Staff |
| GET | `/v1/staff` | Admin |
| POST | `/v1/staff` | Admin — create Staff |

Login is username + password only, not email.

## Ledger (GCash IN/OUT + load)

Staff (or Admin) with a Bearer token:

| Method | Path | Who |
|--------|------|-----|
| GET | `/v1/wallets` | Staff, Admin |
| PUT | `/v1/wallets/opening` | Admin — set starting cash / GCash / TPC |
| GET | `/v1/transaction-methods` | Staff, Admin |
| GET | `/v1/transactions?date=YYYY-MM-DD` | Staff and Admin see all rows. Date is Asia/Manila |
| POST | `/v1/transactions` | Staff, Admin — require `method`, `amount`, `clientRequestId` |
| POST | `/v1/transactions/{id}/void` | Admin |

`POST /v1/transactions` example:

```json
{
  "method": "cash_in",
  "amount": 500,
  "profit": 10,
  "profitInGcash": false,
  "note": "",
  "clientRequestId": "8b1c0c2e-2a2a-4d3e-9f11-2c6d1e8a1111"
}
```

If `profit` is omitted, the API suggests the usual fee. `clientRequestId` must be a new UUID per tap so a retry does not double-post.

Methods: `cash_in`, `cash_out`, `cash_out_with_charge`, `load_via_gcash`, `load_via_tpc`, `top_up_gcash`, `top_up_tpc`, `add_cash_capital`, `expenses`, `bank_cash_deposit`.

The same process also serves a simple phone-friendly counter at `/` (login, wallets, today’s rows, new transaction). Deploy once on Render; no second repo.

## Neon + Render

1. Create a Neon project and copy the **pooled** connection string into `DATABASE_URL`.
2. Generate a JWT signing key at least 32 characters.
3. Set `ADMIN_EMAIL`, `ADMIN_USERNAME`, and `ADMIN_PASSWORD` for the first Admin (only used if no Admin exists yet).
4. On Render, create a **Web Service** from this repo’s Dockerfile. Set the same env vars. Render provides `PORT`.

Both free tiers sleep when idle. The first request after idle can be slow. GCash webhooks later will need an always-on API.

## Env

See `.env.example`. Never commit real secrets.
