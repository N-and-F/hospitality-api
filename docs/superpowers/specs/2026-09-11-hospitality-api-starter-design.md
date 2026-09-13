# Hospitality API starter — design

Date: 2026-09-11  
Status: approved

## Goal

Ship a runnable ASP.NET Core API that can be deployed to Render’s free web service, backed by Neon Postgres. Day one proves health, authenticated identity, and a documented HTTP contract. GCash / Felogan domain modules are out of scope.

## Stack

- .NET 10 LTS
- ASP.NET Core Minimal APIs
- PostgreSQL on Neon (free plan)
- EF Core + Npgsql
- ASP.NET Core Identity (users in our database)
- JWT Bearer for API auth
- Swagger at `/docs`
- xUnit + `WebApplicationFactory`
- Docker image for Render

## Layout

```
Hospitality.Api/                 web host
  Program.cs                     composition root
  appsettings.json
  appsettings.Development.json
  Endpoints/HealthEndpoints.cs
  Endpoints/CurrentUserEndpoints.cs
  Endpoints/StaffEndpoints.cs    Admin-only staff list/create
  Auth/                          JWT issue + validation, current-user accessor
  Data/AppDbContext.cs           Identity + future domain tables
  Identity/                      register / login, role seed
  Common/                        exception handler, correlation id, error JSON

Hospitality.Api.Tests/
  HealthTests.cs
  CurrentUserTests.cs
  AuthTests.cs
  StaffTests.cs

Dockerfile
.dockerignore
.gitignore
.env.example
README.md
```

Solution file at repo root: `Hospitality.Api.sln`.

## HTTP contract

| Method | Path | Auth | Success body |
|--------|------|------|----------------|
| GET | `/health` | public | `{ "status": "ok" }` |
| POST | `/v1/auth/register` | public | `{ "access_token", "token_type": "Bearer" }` |
| POST | `/v1/auth/login` | public | `{ "access_token", "token_type": "Bearer" }` |
| GET | `/v1/users/current` | Bearer JWT (Admin or Staff) | `{ "id", "email", "username", "roles" }` |
| GET | `/v1/staff` | Bearer JWT, **Admin** | `{ "items": [ { "id", "email", "username", "roles" } ] }` |
| POST | `/v1/staff` | Bearer JWT, **Admin** | `{ "id", "email", "username", "roles" }` |
| GET | `/docs` | public | Swagger UI |

Register and login exist so `/v1/users/current` is testable without a third-party IdP.

Register body (`POST /v1/auth/register`):

```json
{ "email": "user@example.com", "username": "jane", "password": "..." }
```

Login body (`POST /v1/auth/login`):

```json
{ "username": "jane", "password": "..." }
```

Login is username + password only. Do not accept email as the login identifier.

Public register always assigns the **Staff** role. The request cannot choose Admin. Admins create additional staff via `POST /v1/staff` (same body as register: email, username, password). That call returns the created user, not a token.

`roles` is a JSON array of strings, e.g. `["Staff"]` or `["Admin"]`.

Validation:

- Register: email required, valid format; username required; password required, Identity default strength
- Username: 3–32 characters, `^[a-zA-Z0-9_]+$`, stored and matched case-insensitively
- Duplicate email on register → `409` with code `EMAIL_TAKEN`
- Duplicate username on register or staff create → `409` with code `USERNAME_TAKEN`
- Bad credentials on login (unknown username or wrong password) → `401` with code `INVALID_CREDENTIALS` (same response either way)
- Authenticated but not Admin on `/v1/staff` → `403` with code `FORBIDDEN`

## Auth

- Users stored via ASP.NET Core Identity in Neon (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`). `UserName` is the login key; `Email` is stored and unique but not used to sign in.
- Two roles only: `Admin` and `Staff`. Seed both role rows on startup. A user has exactly one of these in this starter (no multi-role users).
- Public register → `Staff`. `POST /v1/staff` (Admin) → `Staff`. Nobody can self-assign `Admin`.
- Bootstrap: on startup, if no user has `Admin`, create one from `ADMIN_EMAIL`, `ADMIN_USERNAME`, `ADMIN_PASSWORD`. If an Admin already exists, do not rotate that password from env.
- API issues a signed JWT (HS256) with `sub` = user id, `email`, `unique_name` (username), and `role` claims, expiry 60 minutes.
- Protected endpoints use JWT Bearer. Missing or invalid token → `401`. Wrong role → `403`.
- Signing key from env `JWT_SIGNING_KEY` (at least 32 bytes). Fail fast at startup if missing in non-Development. In non-Development, the three `ADMIN_*` values are required until at least one Admin exists.
- No refresh tokens, social login, email confirmation, or promote/demote endpoints in this starter.

## Data

- Connection string from `DATABASE_URL` (Neon). Npgsql accepts the Neon URI; if a convert-to-ADO form is needed, do it in startup, not in app code scattered around.
- Migrations run on startup in Development. Production/Render: apply migrations in the Docker entrypoint or a documented `dotnet ef database update` step before traffic — prefer entrypoint so a fresh Render deploy is usable.
- No GCash / Felogan tables yet. Identity schema only.
- Use Neon’s pooled connection string for the running API.

## Errors

All failures use:

```json
{
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "Human-readable description",
    "correlation_id": "req-..."
  }
}
```

Unhandled exceptions → `500` / `INTERNAL_ERROR` with the correlation id. Do not leak stack traces in production.

## Config

Required env:

- `DATABASE_URL` — Neon pooled Postgres URI
- `JWT_SIGNING_KEY` — HMAC secret
- `ADMIN_EMAIL`, `ADMIN_USERNAME`, `ADMIN_PASSWORD` — used only to seed the first Admin
- `PORT` — optional; default 8080 (Render sets `PORT`)

`.env.example` lists these. Never commit real secrets.

## Tests

- `GET /health` returns 200 and `{ "status": "ok" }` with no token.
- Register with email, username, and password, then `GET /v1/users/current` with the token returns 200 and the user’s id, email, username, and `roles: ["Staff"]`.
- Login with username and password returns a token; login with email as the identifier is not supported.
- `GET /v1/users/current` without a token returns 401.
- Login with a wrong password returns 401 / `INVALID_CREDENTIALS`.
- Staff token on `GET /v1/staff` returns 403 / `FORBIDDEN`.
- Admin token on `GET /v1/staff` returns 200; `POST /v1/staff` creates a Staff user listed in the subsequent GET.
- Tests use `WebApplicationFactory` and a throwaway database (EF InMemory is acceptable for this starter so CI/local does not require Neon). If InMemory fights Identity, use Testcontainers Postgres — only if InMemory is insufficient.

## Hosting

- Dockerfile: `dotnet publish` multi-stage, listen on `0.0.0.0:$PORT`.
- Render free web service from that image or from the Dockerfile in-repo.
- Database stays on Neon. Do not use Render Postgres.
- README: create a Neon project, copy the pooled URI, set Render env vars, note cold starts on both free tiers.

## Out of scope

- GCash / Felogan modules
- Refresh tokens, OAuth, email verification, role promotion/demotion
- File storage, realtime, RLS-as-product
- CI workflows
- Custom domain
- Hexagonal folder ceremony beyond the layout above

## Done when

`dotnet test` passes, `dotnet run` serves `/health` and `/docs`, a registered Staff user can call `/v1/users/current`, an Admin can list and create staff, and README is enough to point the app at a Neon database and deploy to Render.
