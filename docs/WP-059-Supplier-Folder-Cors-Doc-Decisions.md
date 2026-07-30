# WP-059 — Supplier/Folder Endpoints, CORS Policy, Doc Fix: Report

**Status:** Complete. Part C required no action - already resolved by WP-058.

## Before starting: the working copy had drifted significantly

My prior working copy only reflected state through WP-053. Since then, WP-054
through WP-058 had all landed - `InvoicesController` had been extended twice
(status transitions, notes, extraction confidence, the list endpoint),
`ICurrentUserService` gained a required `DisplayName` member, and `DUPLICATE_SUSPECTED`
was actually removed from the database. Rather than reconstruct any of this from
memory, a fresh working copy was taken directly from the real repository (an
uploaded `Development.zip`) before writing anything, and every file this task
touches was read from that real copy first.

## Part C — already done, not touched

Checked `src/APFlow.Domain/Common/Constants/InvoiceStatusCodes.cs` directly: the
comment WP-059 describes as stale ("still says GB Skips' transitions are
unconfirmed") already reads correctly - it was fixed by WP-058 ("Also fixes the
stale WP-050-era comment...", per `docs/Backlog.md`'s closed-items log). No change
was made; editing an already-correct file to "fix" it again would only risk
introducing a regression.

## Part A — Supplier/Folder endpoints

Implements `docs/WP-019-Supplier-Folder-Views-Decisions.md` §1's proposed contract:
`GET /api/invoices/folders`, `GET /api/invoices/suppliers`, `GET /api/invoices/grouped`.

**New `ISupplierFolderQueryService`** composes the existing, already-tested
`IWorkflowQueryService` (folder list = current tenant's non-terminal statuses) and
`IInvoiceQueryService` (invoice search/counts) - no new repository method, no
business rule invented; this interface only aggregates what those two already
expose.

**Field names: backend wins, per this task's own instruction.** The frontend
fixture (`supplierFolderClient.ts`) renames several `InvoiceListItemDto` fields for
its own convenience (`SupplierInvoiceNumber`→`invoiceNumber`, `GrossTotal`→`amount`,
`Currency`→`currencyCode`). `SupplierGroupDto.Invoices` reuses `InvoiceListItemDto`
exactly as-is (the same shape `GET /api/invoices` already returns), not the
fixture's renamed fields - the same reconciliation rule already applied when
WP-015/016 were reconciled. The wrapper DTOs themselves (`FolderSummaryDto`,
`SupplierGroupDto`, `SupplierGroupedInvoicesDto`) needed no real reconciliation -
WP-019's own proposed field names (`statusCode`/`statusLabel`/`count`/`supplierName`/
`groups`/`totalSuppliers`/`page`/`pageSize`) already match this codebase's
established camelCase-via-default-serialization convention closely enough that
there was no naming conflict to resolve there.

**A real correctness fix, caught before writing any tests, not after.** The
naive design - fetch one page of matching invoices (capped at
`InvoiceQueryParameters.MaxPageSize`, 100) and derive supplier names/groups from
it - would have silently dropped data whenever more than 100 invoices matched a
filter: a supplier whose only invoices happened to sort past the cap would vanish
entirely from the filter dropdown, or from a supplier-grouped page, rather than
merely being slow. This is a correctness bug, not a performance one, so it was not
accepted as a "fine for MVP" trade-off the way e.g. `IInvoiceRepository.GetBySupplierAsync`'s
full-table-scan limitation was (`docs/Backlog.md`). `SupplierFolderQueryService`
instead loops internally across pages (at `MaxPageSize` increments) until the
complete matching set is fetched, before grouping/deriving names - `IInvoiceQueryService.SearchAsync`
itself still enforces the 100-row cap per individual call (by design, to protect
against an unbounded single request), so this loop is the correct way to get a
complete result for aggregation without bypassing that protection. Two dedicated
tests prove this specifically: a supplier's invoices split across the internal
fetch-page boundary are neither dropped nor split into two separate groups.

**The supplier-name filter (`GET /api/invoices/grouped?supplier=`) is applied
in-memory, not pushed down as a new `IInvoiceQueryService` parameter.** It filters
by exact supplier NAME (matching the frontend's actual need - narrowing an
already-grouped view to one group), whereas `InvoiceQueryParameters` only supports
a `SupplierId` (GUID) filter. Adding a name-based filter to the shared,
already-established invoice-search contract for this one caller would have been a
bigger change than this task's implicit "no service-layer changes to
`IInvoiceQueryService`" scope.

**Folder counts use one request per non-terminal status** (`PageSize: 1`, reading
only `PagedResult.TotalCount`, not the rows), rather than a single `GROUP BY`
query - a reasoned simple default reusing the existing, already-tested `SearchAsync`
rather than a new repository-level aggregation. Flagged here as a candidate
Backlog item if invoice volume ever makes this (currently ~11 requests per folder
view) a real cost, matching this codebase's established pattern for accepted-for-now
simplifications.

## Part B — CORS policy

Named policy (`ApFlowWebClient`), origins read from `Cors:AllowedOrigins`
configuration - no hardcoded origin list in code. The decision logic is:

1. Explicit origins configured -> allow exactly those, in every environment
   (including Development - explicit configuration always wins).
2. No origins configured, environment is Development -> permissive fallback
   (`AllowAnyOrigin`), so a developer's frontend on any port works without first
   editing config.
3. No origins configured, any other environment -> **fails closed**: the policy
   permits no cross-origin requests at all, rather than silently falling back to
   permissive. Matches this codebase's established fail-closed philosophy
   elsewhere (WP-003's tenant filter, WP-012's idempotency check, WP-051/053's
   approval-policy checks).

`appsettings.Development.json` sets both required origins (the WP-021 deployed dev
Web App Service URL and `http://localhost:5173`, verified against the frontend's
actual `vite.config.ts` port) - since this project's only deployed environment is
itself named "Development" (`main.bicep` locks `environmentName` to `'dev'`, per
`docs/WP-021-Azure-Infrastructure-Decisions.md`), the same config file legitimately
serves both a developer's local machine and the actual deployed dev instance.

**No `AllowCredentials()`.** This API authenticates via a Bearer token in the
`Authorization` header (Entra External ID JWT), not cookies or TLS client
certificates - `AllowCredentials()` governs the latter and would be an unnecessary
permission grant (`02_Project_Standards.md` §4, least privilege) unrelated to how
this API is actually called.

**Middleware ordering:** `UseCors` is placed after `UseHttpsRedirection` and
before `UseAuthentication`/`UseAuthorization`, the standard ASP.NET Core sequence -
a CORS preflight (`OPTIONS`) request never carries an `Authorization` header, so it
must be handled before authentication middleware would otherwise reject it.

**Testing approach:** the policy-decision logic was extracted to a small, public,
directly-testable method (`ApiServiceCollectionExtensions.ConfigureCorsPolicy`)
rather than left inline in the `AddCors` lambda, specifically so it could be
unit-tested against a real `CorsPolicyBuilder`/built `CorsPolicy` object - proving
actual CORS behavior (which origins are allowed, whether credentials are
supported), not just that a lambda was registered. No `WebApplicationFactory`
infrastructure exists anywhere in this codebase yet, and introducing one for this
single concern was judged out of proportion to the task.

**Naming note:** the options class is `CorsPolicyOptions`, not `CorsOptions` -
`Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions` (the framework's own type,
used as the `AddCors(options => ...)` lambda parameter) already occupies that
name; a genuine collision, not a style preference.

## Files created

- `src/APFlow.Application/DTOs/SupplierFolderDto.cs`
- `src/APFlow.Application/Interfaces/ISupplierFolderQueryService.cs`
- `src/APFlow.Application/Features/SupplierFolders/SupplierFolderQueryService.cs`
- `src/APFlow.Api/Configuration/CorsPolicyOptions.cs`
- `tests/APFlow.Application.Tests/Features/FakeWorkflowQueryService.cs`, `FakeInvoiceQueryService.cs`
- `tests/APFlow.Application.Tests/Features/SupplierFolders/SupplierFolderQueryServiceTests.cs`
- `tests/APFlow.Api.Tests/Extensions/CorsPolicyTests.cs`
- `docs/WP-059-Supplier-Folder-Cors-Doc-Decisions.md`

## Files modified

- `src/APFlow.Application/DependencyInjection.cs` - registers `ISupplierFolderQueryService`
- `src/APFlow.Api/Controllers/InvoicesController.cs` - three new endpoints
- `src/APFlow.Api/Extensions/ApiServiceCollectionExtensions.cs` - `AddApiServices` gains an `IHostEnvironment` parameter; CORS policy registration; new `ConfigureCorsPolicy`/`UseApiCors`
- `src/APFlow.Api/Program.cs` - passes `builder.Environment` to `AddApiServices`; adds `app.UseApiCors()`; removes the now-resolved "no CORS policy" note
- `src/APFlow.Api/appsettings.json` - empty `Cors:AllowedOrigins`
- `src/APFlow.Api/appsettings.Development.json` - the two required origins
- `tests/APFlow.Api.Tests/Controllers/InvoicesControllerTests.cs` - new `FakeSupplierFolderQueryService`; `CreateController` gains an optional parameter; five new endpoint tests

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings.
- `dotnet test` across all 5 test projects - **350/350 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Api.Tests` 55 (44 carried over + 5 Part A
  controller tests + 6 Part B CORS tests), `APFlow.Infrastructure.Tests` 76,
  `APFlow.Integrations.Tests` 45, `APFlow.Application.Tests` 163 (149 carried
  over + 14 new `SupplierFolderQueryService` tests).
