# WP-026 — Supplier Management: Report

**Status:** Done. Committed only — not pushed, not deployed, not live-verified
this round, per explicit session instruction.

**Role:** Backend Engineer. **Sprint:** Sprint 2. **Dependencies:** None (first
Sprint 2 WP built; several later Sprint 2 WPs, e.g. WP-042's Sage 50 export,
depend on the fields added here; WP-027's UI depends on this WP's API).

---

## Two-part history

This WP was first implemented in an earlier session, before `docs/Sprint2-Plan.md`
existed as a checked-in file — that earlier work proceeded from a
self-contained inline task brief (flagged in `docs/Backlog.md` at the time,
since the brief cited a `docs\Sprint2-Plan.md` that didn't exist yet) and built
four of the seven fields the plan actually specifies: `CreditLimit`,
`PaymentTermsDays`, `AccountingReference`, `Status`. That work sat committed
only inside a worktree, never merged to `main`.

This session found that worktree, verified its build/tests were still green
against current `main`, and **completed it against the now-current
`docs/Sprint2-Plan.md` §3 WP-026 spec** before merging: added the three
missing fields (`Code`, `Email`, `Phone`) and the Credit Limit permission
split (task 5), which the earlier round had not built at all. Everything
below describes the final, complete state.

---

## What was built

### 1. `Supplier` entity (`src/APFlow.Domain/Entities/Supplier.cs`)

Seven new fields, all additive to the existing `Name`:

| Field | Type | Notes |
|---|---|---|
| `Code` | `string?` | Nullable, max 32 chars. Short internal reference code, distinct from `AccountingReference` (which is specifically for the Sage 50 export match). |
| `Email` | `string?` | Nullable, max 256 chars. Plain optional contact string — no format validation invented beyond length, matching this WP's existing `AccountingReference` precedent. |
| `Phone` | `string?` | Nullable, max 32 chars. |
| `CreditLimit` | `decimal?` | Nullable — not every supplier has one. `HasPrecision(18, 2)`, matching `Invoice.NetAmount`/`Vat`/`GrossTotal`'s own money-field precision convention. |
| `PaymentTermsDays` | `int?` | Nullable. See "Payment terms representation" decision below. |
| `AccountingReference` | `string?` | Nullable, max 64 chars. Plain optional string for the eventual Sage 50 export (WP-042) — not over-designed ahead of that WP's real requirements. |
| `Status` | `string` | Required, max 20 chars, defaults to `SupplierStatusCodes.Active` (`"ACTIVE"`). See "Supplier status representation" decision below. |

### 2. `SupplierStatusCodes` (new — `src/APFlow.Domain/Common/Constants/SupplierStatusCodes.cs`)

Two constants, `Active`/`Inactive`, mirroring `InvoiceStatusCodes`'s own
pattern and doc-comment reasoning (a plain string on the entity, not an enum,
for future-proofing consistency — even though nothing currently makes
supplier status tenant-configurable the way invoice workflow status is).

### 3. EF Core migration `AddSupplierManagementFields`

Generated via `dotnet ef migrations add`, then hand-corrected: EF's default
`AddColumn` for a newly-required, non-nullable `Status` string column would
have defaulted every existing `Supplier` row to `""` (EF's usual
convention-based default for a non-nullable string). Changed the migration's
`defaultValue` to `"ACTIVE"` explicitly, so pre-existing supplier rows land on
a valid, meaningful status rather than an empty, unrecognized one. `Down()`
correctly drops all seven columns.

Regenerated once during this session (`dotnet ef migrations remove` +
re-`add`) after adding `Code`/`Email`/`Phone` — safe, since this migration had
never been applied anywhere (not merged, not deployed): one clean migration
for the whole WP rather than two.

### 4. `SupplierConfiguration` (`src/APFlow.Infrastructure/Persistence/Configurations/SupplierConfiguration.cs`)

`HasMaxLength(32)` for `Code`, `HasMaxLength(256)` for `Email`,
`HasMaxLength(32)` for `Phone`, `HasPrecision(18, 2)` for `CreditLimit`,
`HasMaxLength(64)` for `AccountingReference`, `IsRequired().HasMaxLength(20)`
for `Status` — matching `FieldLimits`' matching constants
(`src/APFlow.Application/Common/FieldLimits.cs`), per that file's own "must be
updated to match, or validation here will silently accept values the database
will still reject" rule.

### 5. `SaveSupplierRequest`/`SupplierDto` (`src/APFlow.Application/DTOs/SupplierDto.cs`)

Both extended with all seven new fields. `SaveSupplierRequest`'s new
parameters all have defaults (`null` for every optional field,
`Status = SupplierStatusCodes.Active`) so existing call sites/tests that only
pass `Name` keep compiling and behaving identically.

### 6. `SupplierService` (`src/APFlow.Application/Features/Suppliers/SupplierService.cs`)

`ValidateName` renamed to `Validate`, now checking every field of a
create/update request before touching the repository (same fail-fast
pattern the original name-only check used):

- `Supplier.InvalidName` — empty or over 256 chars (unchanged).
- `Supplier.InvalidCode` — over 32 chars.
- `Supplier.InvalidEmail` — over 256 chars.
- `Supplier.InvalidPhone` — over 32 chars.
- `Supplier.InvalidCreditLimit` — negative.
- `Supplier.InvalidPaymentTerms` — negative.
- `Supplier.InvalidAccountingReference` — over 64 chars.
- `Supplier.InvalidStatus` — not exactly `ACTIVE` or `INACTIVE`.

`CreateAsync`/`UpdateAsync` map every new field onto the `Supplier` entity;
`ToDto` returns all seven in `SupplierDto`.

**Task 5 — Credit Limit permission split (this session, previously missing
entirely).** `SupplierService` now takes `ICurrentUserService` as a
constructor dependency (same pattern as `InvoiceService`'s own
WP-051-era `ICurrentUserService` dependency). `CreateAsync` rejects with
`Supplier.CreditLimitForbidden` if the request sets a non-null `CreditLimit`
and the caller does not hold `Roles.FinanceManager`; `UpdateAsync` rejects the
same way if the request's `CreditLimit` differs from the existing entity's
current value (so re-submitting the *same* value — e.g. a reviewer editing
every other field via a form that round-trips the read-only Credit Limit
field — is not itself treated as a change). Every other field remains
editable by any authenticated caller with base access, exactly per spec.

Deliberately a **direct role check** (`_currentUserService.IsInRole(Roles.FinanceManager)`)
rather than routing through `IApprovalAuthorizationService`/`ApprovalPolicy` —
the spec explicitly calls this out, and that machinery is purpose-built for
gating invoice workflow status *transitions* against a configured policy row,
not an unrelated field-level permission on a different entity with no
transition graph of its own.

### 7. `SuppliersController` (new — `src/APFlow.Api/Controllers/SuppliersController.cs`)

The first HTTP surface suppliers have ever had. `ISupplierService`/
`SupplierService` (WP-009) were previously only called internally (duplicate
detection in `InvoiceProcessingService`, supplier-folder queries in
`SupplierFolderQueryService`) — no controller existed at all.

Standard REST CRUD at `api/suppliers`:

| Method | Route | Behavior |
|---|---|---|
| `GET` | `/api/suppliers` | All suppliers visible to the current tenant. |
| `GET` | `/api/suppliers/{id}` | One supplier, 404 if not found. |
| `POST` | `/api/suppliers` | Creates; `201 Created` + `Location` header pointing at `GetById`. `403` if the caller sets `CreditLimit` without `FINANCE_MANAGER`. |
| `PUT` | `/api/suppliers/{id}` | Full-field-replace update, 404 if not found. `403` if the caller changes `CreditLimit` without `FINANCE_MANAGER`. |
| `DELETE` | `/api/suppliers/{id}` | Soft-delete (via `ISupplierRepository.Remove`, backed by `AuditEntity.IsDeleted` — no hard delete), `204 No Content`. |

Binds directly to the existing `SaveSupplierRequest`/`SupplierDto` Application
DTOs for the request/response body rather than introducing separate wire-format
contract types — those DTOs are already exactly the shape a create/update/read
caller needs, no field renaming happens in this controller. Error mapping
follows the same `ProblemDetails` + `code` extension convention as every other
controller in this project: `Supplier.NotFound` → 404,
`Supplier.CreditLimitForbidden` → 403, every other `Supplier.Invalid*` code →
400.

Tenant isolation is not reimplemented here — it comes automatically from
`AppDbContext`'s query filter on `TenantEntity` (same as every other
tenant-scoped repository in this codebase), exactly as the task brief
specified.

**No DI registration changes needed** — `ISupplierService`/`ISupplierRepository`/
`ICurrentUserService` were already registered (`APFlow.Application.DependencyInjection`,
`APFlow.Infrastructure.DependencyInjection`) since WP-009/WP-051; the
controller and service resolve via the existing registrations.

### 8. Tests

**`SupplierServiceTests`** (`tests/APFlow.Application.Tests/Features/Suppliers/SupplierServiceTests.cs`) —
16 tests total (7 from the original round + 9 added this session): every
field's create/update path, `Code`/`Email`/`Phone` over-length rejection, and
four dedicated Credit Limit permission-split tests (non-FinanceManager
setting it on create is forbidden; non-FinanceManager omitting it on create
succeeds; non-FinanceManager changing it on update is forbidden and leaves
the persisted value untouched; non-FinanceManager re-submitting the *same*
value on update succeeds for every other field).

**`SuppliersControllerTests`** (`tests/APFlow.Api.Tests/Controllers/SuppliersControllerTests.cs`) —
10 tests (9 from the original round + 1 added this session confirming
`Supplier.CreditLimitForbidden` maps to a real `403` with the `code` extension
set), via a hand-written `FakeSupplierService` (same pattern as
`WorkflowTemplateControllerTests`'s `FakeWorkflowQueryService`).

---

## Decisions made without an existing catalogue to follow

Checked `docs/AI/06_Domain_Reference_Data.md` first for both, per that
document's own "never invent, escalate instead" rule. It documents only the
role catalogue and the invoice-status catalogue — nothing for payment terms
or supplier status. Both decisions below are recorded as open items in
`docs/Backlog.md` rather than treated as permanently settled, in case a later
WP (particularly WP-042's real Sage 50 integration) needs something richer.

### Payment terms → plain "net days" integer

Considered a named-terms catalogue (`"Net 30"`, `"Due on Receipt"`, `"End of
Month"`, etc.) but rejected it as speculative: nothing in this WP's spec or
the existing codebase specifies what such a catalogue should contain, and
inventing one risks a schema mismatch against whatever WP-042's actual Sage 50
export needs. A plain nullable `int` ("net days") is the minimal
representation that still round-trips cleanly through a numeric-terms export
and needs no accompanying lookup table.

### Supplier status → minimal `ACTIVE`/`INACTIVE` flag

Considered a richer state machine (e.g. Active/Inactive/Suspended/Under
Review, or tenant-configurable statuses the way `Invoice.Status` is) but
rejected it for the same reason: no requirement in this WP's spec calls for
more than a binary flag, and `06_Domain_Reference_Data.md` has no supplier
status catalogue to model against. New `SupplierStatusCodes` constants class
added (mirroring `InvoiceStatusCodes`), string-typed on the entity (not an
enum) purely for representational consistency with `Invoice.Status` — no
workflow/transition enforcement exists around this field the way it does for
invoices; any caller can set either value via `PUT /api/suppliers/{id}`.

### Credit Limit "change" semantics on update

The spec says a request that "changes `CreditLimit`" needs `FINANCE_MANAGER`.
Interpreted literally: comparing the request's value against the *existing
entity's current value*, not merely "is the field non-null in the request" —
so a non-Approver submitting a form that round-trips the same (read-only, per
WP-027's own UI spec) Credit Limit value alongside real edits to other fields
is not blocked. Only an actual attempted change is rejected.

### `Code`/`Email`/`Phone` — no format validation beyond length

Spec names these three fields without specifying validation rules. Kept
consistent with `AccountingReference`'s existing precedent (plain optional
string, length-checked only) rather than inventing email-format regex
validation or a code-uniqueness constraint neither the spec nor
`06_Domain_Reference_Data.md` calls for.

---

## Test results

Baseline before this WP (Sprint 1 final): **391** backend tests passing (11
Domain + 84 Infrastructure + 45 Integrations + 189 Application + 62 Api).

After this WP: **415** backend tests passing (11 Domain + 84 Infrastructure +
45 Integrations + **203** Application (+14) + **72** Api (+10)).

Full `dotnet test APFlow.sln` run clean, no failures, no skips.

---

## Explicitly NOT done this round

- **Not pushed.** Commits are local only, per this session's ground rules —
  the user pushes manually.
- **Not deployed.** No CI/CD run has been triggered by this work.
- **Not live-verified.** No live database migration has been applied to the
  dev environment; no live HTTP call has been made against the new
  `api/suppliers` endpoints. All verification in this round is via the
  backend test suite (unit tests against fakes) and local `dotnet build`/
  `dotnet test` runs — no `TestAppDbContext`/real-SQL-Server migration test
  was added or run for this specific migration (the existing
  `AppDbContextTests`/`AppDbContextTenantIsolationTests` exercise the
  `Supplier` table generically but were not extended with a
  migration-specific assertion).
- **Frontend is untouched.** This WP's spec scopes it as Backend Engineer
  work only — WP-027 (Senior React Engineer, depends on this WP) is where the
  UI, including the Credit Limit read-only/editable split, gets built.
