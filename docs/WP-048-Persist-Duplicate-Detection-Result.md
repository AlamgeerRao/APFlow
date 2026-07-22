# WP-048 — Persist Duplicate Detection Result on Invoice: Report

**Status:** Complete. One discrepancy against the work package's assumed starting
state is flagged below, not silently resolved.
**Priority:** as assigned.

## Important: two of the five tasks were already done before this work package started

Checking the actual codebase (not assuming) before writing anything showed that
`Invoice.IsPotentialDuplicate`/`DuplicateCheckReason` (task 1) and a version of
"persist the result somewhere" already existed, added by an earlier, ad-hoc commit
made directly against `main` outside the normal WP-XXX.zip sequence (visible in the
WP-012 merge report's commit `8c9527d`, which implemented the Chief Technical
Architect's WP-010 sign-off ahead of a formal work package). This explains why this
work package's "Out of Scope" section says "calling `CheckAsync` automatically from
the pipeline — that is WP-049" - whoever wrote WP-048 was working from the
project's formal work-package history, which doesn't yet reflect that ad-hoc
commit. In reality, `InvoiceProcessingService` already called the duplicate check
automatically and already persisted its result - just not the way tasks 3/4 below
require. This report treats the already-existing entity fields as task 1 satisfied
and refactors the already-existing pipeline call site to match tasks 3/4's actual
requirements, rather than either re-doing task 1 from scratch or leaving the
pipeline's now-incompatible call site broken. Flagged here so this isn't mistaken
for scope creep into WP-049 - the pipeline wiring already existed; this WP only
adapts it to a changed interface.

## Task 1 — Entity fields (already present, verified, unchanged)

`Invoice.IsPotentialDuplicate` (`bool`, default `false`) and
`Invoice.DuplicateCheckReason` (`string?`) already existed on the entity and in
`InvoiceConfiguration` (`HasDefaultValue(false)` / `HasMaxLength(4000)`). No change
made - verified these match the task's exact requirement.

## Task 2 — Migration

**This project has never had an EF Core migration before** (confirmed: no
`Migrations` folder existed anywhere, despite `Microsoft.EntityFrameworkCore.Design`
and a working `AppDbContextDesignTimeFactory` having been in place since early in
the project - the tooling was ready, just never invoked). Generated for real, using
that existing factory:

```
dotnet ef migrations add InitialCreate --project src/APFlow.Infrastructure --startup-project src/APFlow.Infrastructure --output-dir Persistence/Migrations
```

**Files created:**
- `src/APFlow.Infrastructure/Persistence/Migrations/20260722184739_InitialCreate.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/20260722184739_InitialCreate.Designer.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/20260722184739_InitialCreate.sql`
  (the rendered SQL script, via `dotnet ef migrations script`, included for easy
  review without needing the EF tooling installed - the `.cs` files above are the
  real migration `dotnet ef database update` actually uses)

**Because no migration has ever existed, this one is necessarily a full baseline**
(`AuditLogs`, `Suppliers`, `Invoices`, `InvoiceNotes` - every table this project has
today), not an incremental "add two columns" diff. This is expected, correct EF
Core behavior when migrations are introduced retroactively to an existing Code
First project - confirmed in the generated SQL:

```sql
CREATE TABLE [Invoices] (
    ...
    [IsPotentialDuplicate] bit NOT NULL DEFAULT CAST(0 AS bit),
    [DuplicateCheckReason] nvarchar(4000) NULL,
    ...
);
```

`DEFAULT CAST(0 AS bit)` and `NULL` satisfy task 2's exact requirement for existing
rows. Could not run `dotnet ef database update` against a real SQL Server in this
sandbox (LocalDB is not supported on this platform - confirmed by trying); the
generated `.sql` script is real EF Core output, not hand-written, so it is
authoritative regardless.

## Task 3 — `DuplicateDetectionService` is now a true pure compute service

**Read literally** ("no `SaveChangesAsync`, no `IInvoiceRepository` dependency, no
`DbContext` dependency" - three separate, explicit constraints, not one restated
three ways). The previous implementation (both before and after the ad-hoc
`8c9527d` commit) still depended on `IInvoiceRepository` for reads
(`GetByIdAsync`/`GetAllAsync`) even though it never wrote - satisfying WP-010's
original concern but not this task's literal wording.

**What changed:** `IDuplicateDetectionService.CheckAsync(Guid, CancellationToken)`
(async, `Result`-wrapped, fetches its own data) became
`IDuplicateDetectionService.Check(Invoice candidate, IReadOnlyList<Invoice> otherInvoices)`
(synchronous, plain return, zero I/O). The caller now supplies both the candidate
and the comparison set; `DuplicateDetectionService`'s constructor takes only
`ILogger<DuplicateDetectionService>`. Verified by a reflection-based test
(`Constructor_HasNoPersistenceDependency`) that inspects the actual constructor
parameters, not just current call patterns - a future regression that reintroduces
a repository dependency will fail this test immediately.

A consequence of removing the repository dependency: `Check` can no longer fail
(the old "invoice not found" failure mode was inherent to the service fetching its
own data - once the caller supplies it, there's nothing left that can go wrong
inside this method). `Check` returns `DuplicateCheckResult` directly, not
`Result<DuplicateCheckResult>`.

## Task 4 — `IInvoiceRepository.PersistDuplicateCheckResultAsync`

New method, fetches by id, sets both fields, and calls `SaveChangesAsync` itself -
unlike every other mutating method on this interface (which stage only). This is a
deliberate difference from WP-013's `IAuditService.LogAsync` "stage, don't save"
pattern, and for a different reason: audit staging needed to commit atomically
*with* an in-flight invoice update in the same method. A duplicate-check result is
always computed and persisted as its own separate step, strictly after the invoice
it describes has already been created/updated and committed by an earlier,
unrelated call - there is nothing in-flight to batch it with, so committing
immediately is simpler and correct. Implemented on `IInvoiceRepository`/
`InvoiceRepository` (real EF Core) and `FakeInvoiceRepository` (tests). Proven
against a real (InMemory-provider) `AppDbContext` in
`InvoiceRepositoryPersistDuplicateCheckResultTests`, including that it respects
tenant isolation (cannot persist against another tenant's invoice id).

## Task 5 — No `InvoiceNote` created

Confirmed: neither the old nor the new implementation ever creates an
`InvoiceNote` for a duplicate-check result. No change was needed for this task -
recorded here only to confirm it was checked, not overlooked.

## `InvoiceProcessingService` - adapted, not "wired in" (see note above)

Since the pipeline already called the old `CheckAsync(Guid)` shape, changing the
interface's signature required updating that one call site or the solution
wouldn't compile. `ProcessAttachmentAsync` now: fetches the full invoice list once
via `IInvoiceRepository.GetAllAsync` (serves as both the duplicate-check candidate
lookup and the comparison set - no extra round-trip), calls the new synchronous
`Check`, and persists via the new dedicated `PersistDuplicateCheckResultAsync`
method instead of the old manual fetch-mutate-`Update`-`SaveChangesAsync` sequence.
This is a mechanical adaptation to an interface WP-048 itself changed, not new
orchestration behavior - WP-049 remains the work package for anything beyond
keeping this existing call site correct.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings (including the
  new migration files).
- `dotnet test`:
  - `APFlow.Application.Tests` - 89/89 pass, including the rewritten
    `DuplicateDetectionServiceTests` (now synchronous, no repository) and the new
    `Constructor_HasNoPersistenceDependency` regression guard.
  - `APFlow.Infrastructure.Tests` - 64/64 pass, including the 3 new
    `InvoiceRepositoryPersistDuplicateCheckResultTests`.
- Migration SQL verified via `dotnet ef migrations script` (real EF Core output,
  inspected above) - could not run `dotnet ef database update` against a live SQL
  Server in this sandbox (no LocalDB support on Linux).
