# Hospitality API Starter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Runnable ASP.NET Core API with health, username/password Identity (Staff self-register, seeded Admin), JWT, and Admin-only staff list/create, deployable to Render against Neon.

**Architecture:** Minimal APIs in `Hospitality.Api` compose Identity + EF Core (Npgsql in run, InMemory in tests), JWT Bearer, and versioned `/v1` endpoints. Errors always use `{ error: { code, message, correlation_id } }`. No GCash/Felogan domain.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core + Npgsql, ASP.NET Identity, JWT Bearer, Swagger, xUnit, WebApplicationFactory, Docker.

**Spec:** `docs/superpowers/specs/2026-09-11-hospitality-api-starter-design.md`

## Global Constraints

- Target `net10.0`. Solution name `Hospitality.Api.sln` at repo root.
- JSON: camelCase except `access_token`, `token_type`, `correlation_id` (explicit `JsonPropertyName`).
- Roles: exactly `Admin` and `Staff`. Public register → Staff. JWT `role` claim. Login by username only.
- Username: 3–32 chars, `^[a-zA-Z0-9_]+$`, case-insensitive unique. Duplicate email `EMAIL_TAKEN`, duplicate username `USERNAME_TAKEN`.
- Env: `DATABASE_URL`, `JWT_SIGNING_KEY` (≥32 bytes), `ADMIN_EMAIL`, `ADMIN_USERNAME`, `ADMIN_PASSWORD`, optional `PORT` default 8080.
- Do not commit secrets. Do not add GCash modules, refresh tokens, CI, or Render Postgres.
- User did not request git commits; skip commit steps unless asked.

## File map

- Create: `Hospitality.Api/Hospitality.Api.csproj`
- Create: `Hospitality.Api/Program.cs`
- Create: `Hospitality.Api/appsettings.json`
- Create: `Hospitality.Api/appsettings.Development.json`
- Create: `Hospitality.Api/Common/ErrorBody.cs` — `ErrorEnvelope`, `ErrorBody`
- Create: `Hospitality.Api/Common/HttpContextExtensions.cs` — correlation id
- Create: `Hospitality.Api/Common/ApiErrorResult.cs` — typed error IResult
- Create: `Hospitality.Api/Common/ExceptionHandler.cs`
- Create: `Hospitality.Api/Data/AppDbContext.cs`
- Create: `Hospitality.Api/Identity/Roles.cs`
- Create: `Hospitality.Api/Identity/UsernameRules.cs`
- Create: `Hospitality.Api/Identity/RegisterRequest.cs`
- Create: `Hospitality.Api/Identity/LoginRequest.cs`
- Create: `Hospitality.Api/Identity/TokenResponse.cs`
- Create: `Hospitality.Api/Identity/UserPublicDto.cs`
- Create: `Hospitality.Api/Identity/StaffListResponse.cs`
- Create: `Hospitality.Api/Auth/JwtTokenService.cs`
- Create: `Hospitality.Api/Auth/JwtBearerErrorEvents.cs`
- Create: `Hospitality.Api/Identity/IdentitySeedHostedService.cs`
- Create: `Hospitality.Api/Identity/UserService.cs` — register/login/create staff helpers
- Create: `Hospitality.Api/Endpoints/HealthEndpoints.cs`
- Create: `Hospitality.Api/Endpoints/AuthEndpoints.cs`
- Create: `Hospitality.Api/Endpoints/CurrentUserEndpoints.cs`
- Create: `Hospitality.Api/Endpoints/StaffEndpoints.cs`
- Create: `Hospitality.Api.Tests/Hospitality.Api.Tests.csproj`
- Create: `Hospitality.Api.Tests/ApiFactory.cs`
- Create: `Hospitality.Api.Tests/HealthTests.cs`
- Create: `Hospitality.Api.Tests/AuthTests.cs`
- Create: `Hospitality.Api.Tests/CurrentUserTests.cs`
- Create: `Hospitality.Api.Tests/StaffTests.cs`
- Create: `Dockerfile`, `.dockerignore`, `.gitignore`, `.env.example`, `README.md`
- Modify: existing `README.md`

---

### Task 1: Solution + health + error envelope

**Files:** solution, gitignore, Api project, test project, Common/*, HealthEndpoints, Program (partial), HealthTests, ApiFactory skeleton.

**Produces:** `GET /health` → 200 `{ "status": "ok" }`; unhandled exceptions → 500 `INTERNAL_ERROR`.

- [ ] Scaffold `net10.0` webapi + xunit, add `Microsoft.AspNetCore.Mvc.Testing` and `InternalsVisibleTo` / `public partial class Program`.
- [ ] Write `HealthTests` that GET `/health` and assert 200 + `status=ok`.
- [ ] Implement correlation middleware, exception handler, `MapGet("/health")`, Swagger `/docs`.
- [ ] `dotnet test` — health passes.

### Task 2: Identity, JWT, register, login

**Produces:** `POST /v1/auth/register` (email, username, password → token, Staff), `POST /v1/auth/login` (username, password). Identity + InMemory in tests; Npgsql + `DATABASE_URL` in app.

- [ ] Write AuthTests: register returns `access_token`/`token_type`; duplicate email `EMAIL_TAKEN`; duplicate username `USERNAME_TAKEN`; login username works; wrong password `INVALID_CREDENTIALS`.
- [ ] Implement Identity, JWT HMAC, validation rules, UserService, AuthEndpoints, JWT challenge/forbidden JSON.
- [ ] `dotnet test` — AuthTests pass.

### Task 3: /v1/users/current

**Produces:** Bearer required; body `{ id, email, username, roles }`.

- [ ] Write CurrentUserTests: no token 401; after register 200 Staff roles.
- [ ] Implement CurrentUserEndpoints.
- [ ] `dotnet test` — CurrentUserTests pass.

### Task 4: Roles, admin seed, staff endpoints

**Produces:** Seed Admin + Staff roles; first Admin from env; `GET/POST /v1/staff` Admin-only.

- [ ] Write StaffTests: Staff GET `/v1/staff` 403; Admin GET 200; Admin POST creates Staff visible in list.
- [ ] Implement seed hosted service, StaffEndpoints, `[Authorize(Roles = "Admin")]`.
- [ ] `dotnet test` — all pass.

### Task 5: Neon wiring, Docker, README

**Produces:** Npgsql from `DATABASE_URL`, migrate on startup when relational, Dockerfile, `.env.example`, README for Neon + Render.

- [ ] Connection string helper, `MigrateAsync` if relational, Docker multi-stage `sdk:10.0` / `aspnet:10.0`, listen `0.0.0.0:$PORT`.
- [ ] `dotnet test` still green.

## Spec coverage

| Spec item | Task |
|-----------|------|
| GET /health | 1 |
| Error JSON + correlation_id | 1, 2 (JWT events) |
| Register/login contract + username rules | 2 |
| JWT + /v1/users/current | 2–3 |
| Admin/Staff roles, seed, /v1/staff | 4 |
| Neon DATABASE_URL, Docker, README | 5 |
| Tests listed in spec | 1–4 |
