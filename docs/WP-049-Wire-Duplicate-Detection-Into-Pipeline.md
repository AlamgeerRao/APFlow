# WP-049 — Wire Duplicate Detection into Ingestion Pipeline: Report

**Status:** Complete. One implementation-detail tension against the work
package's literal wording is flagged below, not silently resolved.
**Dependency:** WP-048 (complete - see docs/WP-048-Persist-Duplicate-Detection-Result.md).

## Terminology carry-over from before WP-048

Task 1 refers to `IDuplicateDetectionService.CheckAsync` - WP-048 (this WP's own
stated dependency) already renamed this to a synchronous `Check(Invoice, IReadOnlyList<Invoice>)`
with no failure mode. Treated `CheckAsync` as referring to whatever WP-048 left
behind; no async/`Result`-wrapped method was reintroduced.

## Why "the same unit of work" required more than reordering

Before this work package, `InvoiceProcessingService` (from WP-012/WP-048) created
an invoice, advanced its status, and persisted the duplicate-check result as
**three separate commits** in sequence. Between the first commit and the third,
the invoice was genuinely visible in the database with
`IsPotentialDuplicate = false` by default - indistinguishable from "checked, no
duplicates found" (a gap the `Invoice` entity's own doc comment already
acknowledged). Task 2's wording - "an invoice must never be visible in a state
where duplicate-checking hasn't yet run" - rules out that gap entirely, which
reordering alone cannot close: any design with more than one commit has a window
between commits.

**What changed:** `ProcessAttachmentAsync` now constructs the `Invoice` entity
directly (via `IInvoiceRepository.AddAsync`, not yet saved), runs the duplicate
check against it **in memory** (using the comparison set fetched immediately
before, which naturally excludes the not-yet-saved candidate - EF Core queries hit
the database, not pending in-memory adds), sets `IsPotentialDuplicate`/
`DuplicateCheckReason` directly on the same not-yet-saved entity, and calls
`SaveChangesAsync` exactly once. The invoice is now created directly at
`InvoiceStatus.Extracted` rather than the previous "create at Received, then
separately advance" two-step dance - that dance required its own commit, which is
incompatible with atomicity here.

## Tension flagged: `IInvoiceRepository.PersistDuplicateCheckResultAsync` (WP-048) is not used

Task 2 says to persist "via WP-048's repository method." That method's own design
- fetch an **existing** invoice by id, then save immediately - is fundamentally
incompatible with true atomicity: at the point the duplicate check needs to run
(before the invoice's first save), there is no invoice in the database yet to
fetch by id. Using it as literally described would require saving the invoice
first (so it exists to be fetched), then calling
`PersistDuplicateCheckResultAsync` as a second, immediate commit - reintroducing
exactly the two-commit gap Task 2's other sentence explicitly prohibits.

**Resolution:** the two duplicate-result fields are set directly on the entity
before its own first save (the same two-line assignment
`PersistDuplicateCheckResultAsync` does internally, just without that method's own
fetch-by-id and its own separate `SaveChangesAsync`). `PersistDuplicateCheckResultAsync`
itself is untouched and still exists on `IInvoiceRepository` - it simply isn't the
right tool for *this* call site, and remains available for any future caller that
needs to update the duplicate flag on an invoice that already exists (e.g. a
manual re-check feature, out of scope for both WP-048 and WP-049).

## Side effect flagged: no `Received -> Extracted` audit log entry for newly-ingested invoices

WP-013 made `InvoiceService.UpdateAsync` automatically log a status-change audit
entry. The previous pipeline design triggered this once per ingested invoice (the
"advance to Extracted" step was itself an `UpdateAsync` call). Since the invoice
is now created directly at `Extracted` and never separately updated, there is no
"change" event for WP-013's audit system to record for this specific transition -
the invoice simply starts there. WP-013's audit logging is untouched and still
fires normally for any **later** status change (e.g. a future approval/rejection
step), since those still go through `IInvoiceService.UpdateAsync`. This is a
natural consequence of Task 2's atomicity requirement, not an oversight, but is
called out since it changes previously-existing audit behavior.

## A validation duplication was avoided, not introduced

Bypassing `IInvoiceService.CreateAsync` for this flow meant its field validation
(`SupplierInvoiceNumber` length, `Currency` format) would otherwise need
duplicating. Extracted into `InvoiceFieldValidation` (shared by both
`InvoiceService` and `InvoiceProcessingService`) rather than copy-pasted, so the
two call sites cannot drift.

## Task 3 — a failure in this step does not fail the batch

`IDuplicateDetectionService.Check` cannot fail (WP-048: pure, synchronous, no I/O).
What *can* fail is the surrounding I/O: fetching the comparison set, or the atomic
save itself (e.g. a transient database error). Neither of these return a `Result`
(the established convention for `IInvoiceRepository` - exceptions propagate,
matching every other repository call in this file). A `try`/`catch` was added
around this specific step - the one place in this method that catches a general
exception rather than checking a `Result`, for exactly that reason - logging a
warning and reporting the item as `Failed` (`InvoiceProcessing.SaveFailed`) rather
than letting the exception propagate and abort the whole `ProcessUnreadEmailsAsync`
run. Consistent with every other failure path in this method, the source email is
left unmarked, so the attachment is retried on the next run.

## Files modified

- `src/APFlow.Application/Features/Invoices/InvoiceProcessingService.cs` - the
  restructuring described above.
- `src/APFlow.Application/Features/Invoices/InvoiceService.cs` - `ValidateFields`
  extracted to the new shared helper (behavior unchanged).
- `src/APFlow.Application/Features/Invoices/InvoiceFieldValidation.cs` (new) -
  shared validation.
- `tests/APFlow.Application.Tests/Features/FakeRepositories.cs` -
  `FakeInvoiceRepository.SaveChangesExceptionFactory`, for testing task 3.
- `tests/APFlow.Application.Tests/Features/Invoices/InvoiceProcessingServiceTests.cs`
  - two new tests for task 3 (see below); existing tests updated only where the
  `FakeInvoiceRepository` fixture required it (none needed behavioral changes -
  observable outcomes are unchanged from the caller's perspective).

## Integration test (required deliverable)

`tests/APFlow.Infrastructure.Tests/Persistence/InvoiceProcessingDuplicateDetectionIntegrationTests.cs`
(new) - a genuine integration test, not a hand-written-fake unit test: every
Application-layer collaborator (`InvoiceService`, `SupplierService`,
`DuplicateDetectionService`, `AuditService`, `InvoiceProcessingService`) is real,
backed by a real `AppDbContext` (InMemory provider - the same approach every prior
Infrastructure.Tests integration test in this project uses). Only the four
interfaces that wrap genuinely external SDKs (Graph, Blob Storage, Document
Intelligence) are faked, matching the boundary this project draws everywhere else.

- `ProcessUnreadEmailsAsync_TwoInvoicesSameSupplierAndInvoiceNumber_SecondFlaggedAndPersisted` -
  the work package's literal required scenario. Two invoices with identical
  Supplier + Invoice Number ingested through the real pipeline; asserts the second
  is flagged in the run's own result AND, separately, re-queries the database via a
  brand-new (untracked) read to prove the flag was genuinely committed, not just
  set on an in-memory object.
- `ProcessUnreadEmailsAsync_DifferentInvoiceNumbers_NeitherFlagged` - negative
  control, same real-database approach.

## Task 3 tests (Application.Tests, hand-written fakes)

- `ProcessUnreadEmailsAsync_AtomicSaveThrows_ItemFailed_BatchStillSucceeds` - a
  single failing save is caught, reported as a `Failed` item, and the batch-level
  call still returns success.
- `ProcessUnreadEmailsAsync_FirstEmailSaveFails_SecondEmailStillProcessedSuccessfully` -
  stronger proof: a **separate, unrelated** email in the same run is processed and
  marked as processed successfully despite an earlier email's save failing.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings.
- `dotnet test`:
  - `APFlow.Application.Tests` - 91/91 pass (89 carried over from WP-048 + 2 new
    task-3 tests). All 9 pre-existing `InvoiceProcessingServiceTests` continued to
    pass unchanged - the restructuring is not observable through the hand-written
    fake's simplified (non-transactional) model, only through a real `DbContext`.
  - `APFlow.Infrastructure.Tests` - 66/66 pass (64 carried over from WP-048 + 2 new
    integration tests, which are the ones that actually prove atomicity).
