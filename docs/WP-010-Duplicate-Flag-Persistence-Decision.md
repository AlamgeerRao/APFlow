# WP-010 — Duplicate Flag/Reason Persistence: Decision Required

**Status:** IMPLEMENTED — ruling recorded 2026-07-20, implemented the same day.
`Invoice.IsPotentialDuplicate`/`DuplicateCheckReason` are persisted fields;
`InvoiceProcessingService` (WP-012) computes and persists them automatically for
every attachment it processes. See "## Implementation" below.
**Owner:** Chief Technical Architect.
**Raised:** WP-010 delivery.

## Ruling (Chief Technical Architect, 2026-07-20)

**Reasoning:** WP-010's own stated objective - detect duplicates before approval
- requires the result to survive until an approval step exists to consult it. An
approval workflow isn't built yet, but building duplicate detection as ephemeral
now means it silently stops doing its job the moment approval is built, unless
someone remembers to rewire it. That's a foreseeable gap worth closing now rather
than later.

**Decisions:**

- **Persist as new `Invoice` fields, not an automatic `InvoiceNote`.** Add
  `IsPotentialDuplicate` (bool) and `DuplicateCheckReason` (string, nullable)
  directly to `Invoice`. This is queryable by the dashboard/future approval UI
  without joining to notes, and it represents system-computed state, not a
  human-authored note - `InvoiceNote` stays reserved for human/audit commentary,
  not system output. This is a schema change; approved here explicitly, so it
  does not conflict with "don't fabricate requirements" - this sign-off is the
  requirement.
- **Persistence ownership:** `DuplicateDetectionService` should not call
  `SaveChangesAsync` itself. Keep it a pure compute service (single
  responsibility, per Engineering Principles). The calling orchestrator (invoice
  ingestion pipeline) is responsible for invoking `CheckAsync` and persisting the
  result via `IInvoiceRepository`. This keeps `DuplicateDetectionService`
  reusable and testable in isolation, matching Testing Standards.
- **Wiring:** `CheckAsync` should be invoked automatically as part of the
  ingestion pipeline (immediately after extraction/before the invoice is first
  surfaced to a reviewer), so the flag is already present the first time a human
  sees the invoice. Tracked as a small follow-up task against the ingestion
  orchestrator (`InvoiceProcessingService`, WP-012) - not a new WP, a completion
  item on the existing pipeline. See `docs/Backlog.md`.

**On the three non-blocking QA observations below:**

- Currency excluded from matching criteria - confirmed correct, no change. Logged
  as a backlog candidate; not added speculatively.
- No status filtering - confirmed correct as-is. A resubmitted rejected invoice
  should still be flagged. No change needed.
- `GetAllAsync()` full-table scan - accepted as known MVP tech debt. Tracked in
  `docs/Backlog.md` as a `GetBySupplierAsync` addition to `IInvoiceRepository`;
  not worth building ahead of actual volume.

Treat the "Decision needed" checklist below as answered by the ruling above, not
as still-open.

## Implementation (2026-07-20)

The ruling above is now implemented, same day:

- `Invoice.IsPotentialDuplicate` (bool, `HasDefaultValue(false)`) and
  `Invoice.DuplicateCheckReason` (string?, `HasMaxLength(4000)`) added directly to
  the entity and `InvoiceConfiguration` - not an `InvoiceNote`, per the ruling.
- `DuplicateDetectionService` is unchanged - still a pure compute service, no
  `SaveChangesAsync` access, fully testable in isolation exactly as before.
- `InvoiceProcessingService` (WP-012) gained a new `IInvoiceRepository`
  dependency (alongside its existing `IInvoiceService` one) used for exactly one
  thing: `PersistDuplicateCheckResultAsync` fetches the just-saved invoice,
  writes `IsPotentialDuplicate`/`DuplicateCheckReason` from a successful
  `CheckAsync` result, and calls `Update`/`SaveChangesAsync` - the orchestrator
  owns the write, per the ruling. Multiple `DuplicateMatch.Reason` strings are
  joined into one `DuplicateCheckReason` value.
- Wiring is automatic: `CheckAsync` already ran immediately after every
  successful invoice save in the pipeline (this was true before this ruling
  too); what changed is that a successful result is now persisted rather than
  only surfaced in the per-run `InvoiceProcessingItemResult`/logs. A failed
  check still does not fail the item and leaves the invoice's persisted flag at
  its prior value (false/null for a newly created invoice).
- Tests: `InvoiceProcessingServiceTests` asserts the persisted entity's
  `IsPotentialDuplicate`/`DuplicateCheckReason` directly (both the flagged and
  the check-failed cases), in addition to the pre-existing assertions on the
  per-run result's own `IsPotentialDuplicate` field.

This closes the `docs/Backlog.md` row for this item. The three QA observations
above (Currency, Status filtering, `GetAllAsync` full scan) remain backlog-only -
none were in scope for this change.

## What exists today

`IDuplicateDetectionService.CheckAsync` returns a `DuplicateCheckResult` (an
`IsPotentialDuplicate` flag, plus a list of `DuplicateMatch` records - the matched
invoice, which fields matched, and a human-readable reason) directly to the caller.
Nothing is written to the database. The `Invoice` entity is not modified in any way
- no new column, no status change, no `InvoiceNote` is created. A duplicate event is
also written to the application log (`LogWarning`) at check time, but that is
operational log output, not a queryable business record.

Call `CheckAsync` twice for the same invoice in the same state and you get the same
answer computed fresh both times; there is no persisted trace that a check ever
happened, once the log line rotates out.

## Why this default was chosen

WP-010's task list reads as three list items - "Flag potential duplicates",
"Record duplicate reason", "Log duplicate events" - followed by "Return duplicate
status to the caller." The literal, narrowest reading is that all three are
satisfied by the shape of what's returned to the caller (the flag and the reason
are both present on the returned result) plus the separate log line - i.e. "record"
means "include in the structured result," not "persist to the database."

Nothing in `01_Project_Context.md` through `05_Development_Workflow.md`, in the
`Invoice`/`InvoiceNote` entity doc comments, or in WP-010's own task list calls for
a schema change, and `02_Project_Standards.md` is explicit that AI agents must
"never fabricate implementation details not provided in the work package or
approved documentation." Inventing a new `Invoice` column or an automatic
`InvoiceNote` write was judged riskier than an ephemeral result, because it can't be
un-shipped as cheaply once other code (a future review UI, an approval workflow)
starts depending on that shape.

## Why this isn't just implemented and closed

`InvoiceNote` already exists as exactly the kind of "timestamped record attached to
an invoice" a persisted duplicate reason would naturally use, and "Record duplicate
reason" as a task distinct from "Flag potential duplicates" reads at least as
plausibly as "persist this, don't just return it" - particularly since WP-010's
stated objective is "Detect duplicate supplier invoices **before approval**," which
implies the result needs to survive long enough for a later approval step (not yet
built) to see it. An ephemeral, recompute-on-demand result cannot do that on its
own; something will eventually need to call `CheckAsync` again at approval time, or
the result needs to be persisted now. Only the Chief Technical Architect can confirm
which is intended.

## Decision needed

- [ ] Confirm ephemeral (compute-on-call, return to caller, log only) is correct for
      WP-010, or specify that `IsPotentialDuplicate`/the reason should be persisted
      - as new `Invoice` fields, as an automatically-created `InvoiceNote`, or some
      other shape.
- [ ] If persisted: confirm whether `DuplicateDetectionService` should write it
      directly (via `IInvoiceRepository`), or whether persistence belongs to a
      calling feature (e.g. wherever invoice ingestion or a future approval step
      invokes the check) instead - this also determines whether
      `DuplicateDetectionService` needs `SaveChangesAsync` access at all.
- [ ] Confirm whether/when `CheckAsync` is expected to be invoked automatically
      (e.g. after WP-008 extraction, or at invoice creation) versus only on demand -
      WP-010 didn't request wiring it into any pipeline, and none has been added.

## Also raised at QA review (non-blocking, PASS-compatible)

Three further observations from review, each explicitly Low-priority/non-blocking
- worth confirming with the business as real requirements surface, not code defects
as delivered. None change the PASS verdict; logging them here so they survive past
this conversation rather than needing to be re-discovered later.

- **`Currency` is not part of the matching criteria.** WP-010's task list names
  Supplier, Invoice Number, Invoice Date, and Gross Amount specifically - Currency
  wasn't listed, so it wasn't added, per the same "don't fabricate requirements
  beyond what was specified" reasoning as the rest of this doc. Worth confirming
  whether two invoices with identical number/date/amount but different currencies
  should ever be flagged as duplicates (plausible they should NOT be - e.g. a
  supplier billing the same PO in both GBP and EUR by mistake is arguably a
  different, real error worth its own detection, not a duplicate).
- **No `Status` filtering.** `CheckAsync` compares against every invoice returned by
  `GetAllAsync`, including ones already `Approved` or `Rejected`. Worth confirming
  whether a rejected invoice should still be able to trigger a duplicate flag against
  a new incoming one (arguably yes - a supplier resubmitting a previously-rejected
  invoice unchanged is exactly the kind of thing this feature should catch) or
  whether only "live" statuses should be compared.
- **`GetAllAsync()` loads every tenant invoice into memory** rather than querying
  scoped to the candidate's supplier. Correct and fine at current/expected MVP
  volume (`IInvoiceRepository` has no supplier-scoped read method today, and adding
  one wasn't requested by WP-010), but worth a note for whoever eventually looks at
  this under real production volume - a `GetBySupplierAsync`-style addition to
  `IInvoiceRepository` would be the natural fix if/when this becomes a real cost.

## Related

- `docs/WP-003-Tenant-Isolation-Decision.md`, `docs/WP-004-Health-Check-Severity-Decision.md`,
  `docs/WP-004-Graph-Multitenancy-Decision.md`, `docs/WP-005-Blob-Storage-Tenant-Isolation-Decision.md`
  - same "implement a reasoned default, flag rather than silently decide" pattern.
