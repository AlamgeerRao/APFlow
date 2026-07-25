# WP-058 — Restore Invoice List Endpoint & Minor Doc Fix: Report

**Status:** Complete. Task 1's framing needs a significant correction, flagged in
detail below - the technical work itself (wiring `IInvoiceQueryService` into
`InvoicesController`) is done regardless, since it's the same work either way.

## Framing correction: this is not a restoration

The objective and title both describe this as "restoring" a previously-existing
`GET /api/invoices` endpoint. **There is no evidence anywhere in this codebase
or its documentation that such an endpoint ever existed.** Checked directly:

- `docs/WP-052-Pipeline-And-Api-Hardening-Decisions.md` Part D states explicitly:
  "**First real API controllers in this codebase.** `builder.Services.AddControllers()`
  and `app.MapControllers()` were already wired (WP-001) but nothing had ever
  been registered to them - `InvoicesController` is the first." WP-052 built
  exactly two endpoints (`GET /{id}`, `GET /{id}/download}`) - a list endpoint
  was not among them.
- `docs/WP-015-Invoice-Queue-Decisions.md` (the frontend work package that needed
  exactly this endpoint) says: "since WP-011's real endpoint contract wasn't
  available, this delivery implements [a fixture client instead]" and lists
  "exact endpoint route, field names/casing, param names, and default/max page
  size" under **Needs confirmation** - i.e. the frontend team building against
  this capability never had a real endpoint to call, and had to guess.
- WP-011 itself ("Invoice Repository & Query Services") is titled and scoped as
  repository/service-layer work - `IInvoiceQueryService`/`InvoiceQueryService`/
  `IInvoiceRepository.QueryAsync` - not an HTTP endpoint. Nothing in this
  codebase's history shows it, or any other work package, ever adding a
  bare-`GET /api/invoices` controller action.

So: `GET /api/invoices` is a **new** endpoint, not a restored one. There is
consequently no original HTTP contract to "match" for task 1's own "tests
confirming the restored endpoint matches WP-011's original contract" -
`IInvoiceQueryService.SearchAsync`'s actual signature (`InvoiceQueryParameters`
in, `Result<PagedResult<InvoiceListItemDto>>` out) is the only real, existing
contract available, and that is what the new endpoint wraps and what the new
tests verify against. WP-015's proposed shape
(`?search=&status=&sortBy=&sortDirection=&page=&pageSize=` /
`{ items, totalCount, page, pageSize }` with fields like `amount`/`currencyCode`/
`invoiceNumber`) was read and considered, but not used verbatim: it predates any
knowledge of this codebase's real field names, was explicitly marked "not
binding," and copying it would mean inventing new field names for data
`InvoiceListItemDto`/`InvoiceQueryParameters` already name differently
(`GrossTotal` not `amount`, `Currency` not `currencyCode`,
`SupplierInvoiceNumber` not `invoiceNumber`, no generic multi-field `search`
param - `InvoiceQueryParameters.InvoiceNumber` only substring-matches the
invoice number) - exactly the "third, incompatible naming scheme" every prior
WP-052+ endpoint in this controller has deliberately avoided.

**If a real contract negotiation with the frontend is wanted before this ships**
(the way, e.g., WP-054 cited an explicit "full contract proposal" from a
frontend review), that hasn't happened here - this is a straightforward,
literal reading of task 1's instructions ("wiring the existing... service...
no service-layer changes needed, this is controller-only"), not a negotiated
contract. Flagging so the Chief Technical Architect can redirect if the intent
was actually to formalize WP-015's proposal instead.

## Task 1 — `GET /api/invoices`

Added to `InvoicesController`, calling `IInvoiceQueryService.SearchAsync`
directly - no service-layer changes, per the task's own instruction. Query
parameters bind individually via `[FromQuery]`, named to match
`InvoiceQueryParameters`' own property names exactly (`status`, `supplierId`,
`invoiceDateFrom`, `invoiceDateTo`, `invoiceNumber`, `page`, `pageSize`,
`sortBy`, `sortDescending`) - the same "reuse the existing DTO's names, don't
invent a new naming scheme" convention every WP-054+ endpoint in this
controller follows. Binding is case-insensitive (ASP.NET Core default), so
`?Status=` and `?status=` both work.

Response: `PagedResult<InvoiceListItemDto>` returned directly, no additional
wrapper - `Items`/`TotalCount`/`Page`/`PageSize` (plus a computed `TotalPages`)
is already the complete shape a list view needs.

Validation errors (`InvoiceQuery.InvalidPage`, `InvoiceQuery.InvalidPageSize`,
`InvoiceQuery.InvalidDateRange` - all `IInvoiceQueryService`'s own, WP-011) fall
through the existing `ErrorProblem` helper's default case to `400 Bad Request`
with the real code in the `code` extension - no new error-mapping logic needed.

## Task 2 — Stale comment fix

`InvoiceStatusCodes.cs`'s block comment above `CheckedReadyToApprove`/
`NeedsReviewFebina` (and both constants' own doc comments, which repeated the
same claim - fixed together rather than leaving them contradicting the updated
block comment right above them) said GB Skips' transitions were "NOT yet
confirmed... Do not use these as if they were reachable via any enforced
transition yet," citing WP-050. WP-053 confirmed and enabled all of them.
Updated to state that plainly and point at
`docs/WP-053-Transition-Enforcement-Decisions.md` instead.

## Files modified

- `src/APFlow.Api/Controllers/InvoicesController.cs` - new `GetAll` endpoint;
  new `IInvoiceQueryService` constructor dependency; class doc comment updated
- `src/APFlow.Domain/Common/Constants/InvoiceStatusCodes.cs` - stale WP-050-era
  comment corrected (task 2)
- `tests/APFlow.Api.Tests/Controllers/InvoicesControllerTests.cs` - new
  `FakeInvoiceQueryService`; `CreateController` updated for the new dependency;
  four new tests
- `README.md` - added WP-058 row; resolved two now-stale `DUPLICATE_SUSPECTED`
  mentions in the open-decisions list (06's Revision History, supplied since the
  WP-057 report was written, confirms this is fully resolved, not still open)

## Tests

Since there is no prior contract to "match" (see framing correction above),
these instead verify the endpoint faithfully wraps `IInvoiceQueryService`'s real
contract:

- `GetAll_ReturnsPagedResultFromQueryService` - the service's `PagedResult`
  passes through to the HTTP response unchanged.
- `GetAll_DefaultQueryParameters_MatchInvoiceQueryParametersDefaults` - when no
  query string values are supplied, the parameters actually passed to
  `SearchAsync` equal a plain `new InvoiceQueryParameters()`'s defaults exactly
  (page 1, page size 25, sort by `CreatedAtUtc` descending) - the controller
  cannot silently diverge from WP-011's own defaults.
- `GetAll_PassesFilterParametersThroughUnchanged` - every filter/sort/paging
  query parameter reaches `SearchAsync` unmodified.
- `GetAll_InvalidPage_ReturnsBadRequestWithCode` - a validation failure from the
  service surfaces as `400` with the real `InvoiceQuery.InvalidPage` code.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings, whole solution.
- `dotnet test` across all 5 test projects - **322/322 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Application.Tests` 147,
  `APFlow.Api.Tests` 44 (+4 new), `APFlow.Infrastructure.Tests` 75,
  `APFlow.Integrations.Tests` 45.
