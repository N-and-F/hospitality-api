# GCash Ledger API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Daily Transaction ledger API so the family’s mobile app can post GCash/load rows and read wallet balances.

**Architecture:** Pure wallet math + profit suggestion, EF entities for `WalletOpening` and `LedgerTransaction`, `LedgerService` for create/list/void, Minimal API under `/v1`. Auth unchanged.

**Tech Stack:** .NET 10, EF Core, existing JWT Identity, xUnit, WebApplicationFactory.

**Spec:** `docs/superpowers/specs/2026-09-14-gcash-ledger-design.md`

## Global Constraints

- Asia/Manila for `?date=` and listing.
- JSON camelCase (`cashOnHand`, `profitInGcash`, `clientRequestId`).
- Cash/GCash cannot go negative; TPC can.
- `clientRequestId` unique; same user replay returns original row.
- No mobile UI, no GCash vendor API, no sheet import.
- Do not commit unless the user asks.

## File map

- Create: `Hospitality.Api/Ledger/TransactionMethods.cs`
- Create: `Hospitality.Api/Ledger/WalletAmounts.cs`
- Create: `Hospitality.Api/Ledger/WalletMath.cs`
- Create: `Hospitality.Api/Ledger/ProfitSuggestion.cs`
- Create: `Hospitality.Api/Ledger/LedgerDtos.cs`
- Create: `Hospitality.Api/Ledger/LedgerService.cs`
- Create: `Hospitality.Api/Data/LedgerTransaction.cs`
- Create: `Hospitality.Api/Data/WalletOpening.cs`
- Create: `Hospitality.Api/Endpoints/LedgerEndpoints.cs`
- Create: `Hospitality.Api.Tests/WalletMathTests.cs`
- Create: `Hospitality.Api.Tests/ProfitSuggestionTests.cs`
- Create: `Hospitality.Api.Tests/LedgerTests.cs`
- Modify: `AppDbContext.cs`, `Program.cs`, `README.md`
- Create: EF migration `Ledger`

### Task 1: Wallet math + profit suggestion

- [ ] Unit tests for cash_in deltas and load profit bands
- [ ] Implement `WalletMath` / `ProfitSuggestion`

### Task 2: Persistence + HTTP

- [ ] API tests from spec (create, wallets, idempotency, insufficient, TPC negative, staff isolation, void, Manila date)
- [ ] Entities, `LedgerService`, endpoints, migration

---
