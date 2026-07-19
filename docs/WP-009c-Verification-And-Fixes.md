# WP-009c — Real Build/Test Verification & Fixes

**Status:** Complete
**Context:** WP-009b was delivered and reviewed under a sandbox with no NuGet access,
so nothing had ever been compiled or tested against real packages. This pass had
real network access and ran the solution for real for the first time.

---

## 1. Critical item verified: tenant isolation

`AppDbContextTenantIsolationTests` (the project's most security-critical control)
was executed for real against a live EF Core provider (SQLite in-memory), not
reasoned about. **All 4 tests pass:**

- `NoResolvableTenant_SeesNoRows_FailsClosedNotOpen`
- `IgnoreQueryFilters_CanStillSeeAllTenantsData_ForAdminDiagnosticUseOnly`
- `DifferentDbContextInstances_EachSeesOnlyOwnTenantData`
- `ThirdTenant_WithNoData_SeesEmptyList_NotOtherTenantsData`

This closes the open verification item from `WP-003-Tenant-Isolation-Decision.md`.

## 2. Build did not succeed on first attempt

Real NuGet/compiler access surfaced issues the offline sandbox could not have
caught. All four fixes below are mechanical (missing references/using
directives or a genuine framework-behaviour bug) — no business logic was
changed.

| # | Issue | File | Fix |
|---|---|---|---|
| 1 | `ILogger<T>` used without a reference to `Microsoft.Extensions.Logging.Abstractions` | `src/APFlow.Application/APFlow.Application.csproj` | Added package reference |
| 2 | Missing `using Azure;` (`WaitUntil`) and `using Microsoft.Extensions.Logging;` (`LogWarning`) | `src/APFlow.Integrations/DocumentIntelligence/DocumentIntelligenceOperations.cs` | Added two `using` directives |
| 3 | Missing `using Microsoft.AspNetCore.Authorization.Infrastructure;` | `tests/APFlow.Api.Tests/Extensions/AuthorizationExtensionsTests.cs` | Added `using` directive |
| 4 | **Functional bug**: `HttpResponse.WriteAsJsonAsync(problemDetails)` silently overwrites `Response.ContentType` with its own default (`application/json; charset=utf-8`), discarding the `application/problem+json` set immediately beforehand. Error responses were not RFC 7807 compliant. | `src/APFlow.Api/Middleware/ExceptionHandlingMiddleware.cs` | Pass `contentType: "application/problem+json"` explicitly into `WriteAsJsonAsync` instead of setting `Response.ContentType` separately |

Item 4 was caught by 3 pre-existing tests in `ExceptionHandlingMiddlewareTests`
that failed until the fix was applied — the tests were correct, the shipped
code was not.

## 3. Result after fixes

- `dotnet restore` — succeeds, all 11 projects.
- `dotnet build` — succeeds, **0 errors**.
- `dotnet test` (full solution) — **140/140 pass, 0 failures.**

| Test project | Result |
|---|---|
| APFlow.Domain.Tests | 11/11 passed |
| APFlow.Api.Tests | 18/18 passed |
| APFlow.Infrastructure.Tests | 43/43 passed |
| APFlow.Integrations.Tests | 45/45 passed |
| APFlow.Application.Tests | 23/23 passed |

## 4. Known open item — not fixed in this pass (out of scope)

A clean (`--no-incremental`) rebuild shows **20 warnings**, not yet actioned:

- **18× `CS1591`** (missing XML doc comments) on `InvoiceRepository`,
  `SupplierRepository`, and the three EF `IEntityTypeConfiguration` classes
  (`InvoiceConfiguration`, `InvoiceNoteConfiguration`, `SupplierConfiguration`)
  in `APFlow.Infrastructure`.
- **2× `xUnit1031`** (blocking task operations in test methods) in
  `tests/APFlow.Api.Tests/Extensions/AuthenticationExtensionsTests.cs`.

Per `04_Definition_of_Done.md` ("no compiler warnings"), WP-009 is not fully
clean until these are addressed. Recommend a small follow-up work package or
folding into WP-010's Definition of Done pass. Flagging rather than fixing
here, to keep this verification pass scoped to what was asked.

## 5. Environment used for this verification

- .NET SDK 9.0.316 (pinned via `global.json`: `9.0.100`, `rollForward: latestFeature`)
- `dotnet restore` / `dotnet build` / `dotnet test` run against the real
  NuGet feed (`api.nuget.org`) — confirmed reachable and functional.
