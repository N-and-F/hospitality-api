# Cook share percentage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or implement inline. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Global cook % on Cooks settings, snapshot on new servings, remove Sham from the profit split.

**Architecture:** `FeloganSettings` singleton + `FeloganServings.CookPercent`. `ItemSplit(sell, cost, cookPercent)` with `nf = profit − cook`. GET/PUT `/v1/felogan/settings`. Menu uses current %; servings use snapshot.

**Tech Stack:** .NET 10 ASP.NET Core, EF Core Npgsql / InMemory tests, static Felogan UI.

**Spec:** `docs/superpowers/specs/2026-09-22-felogan-cook-percent-design.md`

## Global Constraints

- Admin-only Felogan; Staff 403 on settings.
- `cookPercent` integer 0–100; default 40.
- POST stamps settings %; PUT serving keeps snapshot.
- Do not drop `PaidSham` column.
- Do not commit unless asked.

---

### Task 1: Settings API + snapshot + no Sham

**Files:**
- Create: `Hospitality.Api/Data/FeloganSettings.cs`
- Modify: `AppDbContext`, `FeloganServing`, DTOs, `FeloganService`, `FeloganEndpoints`, `FeloganSeed`, `FeloganTests`, EF migration
- Test: `Hospitality.Api.Tests/FeloganTests.cs`

- [x] Failing tests for default 40, PUT 35 history vs new serving, validation, staff 403; update split asserts (nf 38.90 / 77.80, no sham).
- [x] Entity + migration `FeloganCookPercent`.
- [x] `ItemSplit` without sham; settings GET/PUT; stamp `CookPercent` on create only.
- [x] `dotnet test --filter FullyQualifiedName~FeloganTests`

### Task 2: Cooks page UI

**Files:** `index.html`, `felogan.js`, `app.css`, `WebUiTests.cs`

- [x] Cook share form on cooks section; menu header `Cook ${cookPercent}%`; drop `paidSham` from PUT body.
- [x] Cache-bust `?v=cook-percent`.
- [x] `dotnet test --filter FullyQualifiedName~WebUiTests|FullyQualifiedName~HealthTests`
