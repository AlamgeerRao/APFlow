# WP-013 — Audit Logging & Activity History: Decisions Requiring Sign-Off

**Status:** OPEN — implemented with reasoned defaults; needs explicit sign-off.
**Owner:** Chief Technical Architect.
**Raised:** WP-013 delivery.

## 1. "User" and "Date/Time" are not dedicated `AuditLog` columns

**What exists today:** `AuditLog` derives from `TenantEntity` (→ `AuditEntity`),
which already provides `CreatedBy` and `CreatedAtUtc`, auto-stamped by
`AppDbContext.SaveChanges` from `ICurrentUserService.UserId` (falling back to the
literal string `"system"` - an existing convention, not invented for this WP - see
`AppDbContext.ApplyAuditAndSoftDeleteConventions`). These satisfy WP-013 task 3's
"User" and "Date/Time" fields without a dedicated column for either. Only `Action`,
`EntityName`, `EntityId`, `PreviousValue`, and `NewValue` are new properties.
`AuditLogDto` (the read/query shape) renames these two inherited fields to
`PerformedByUserId`/`PerformedAtUtc` so a consumer doesn't need to know they are
reused base-class fields to understand what they mean.

**Why this wasn't just implemented and closed:** it is a real design choice
(dedicated columns would have been an equally defensible, more explicit
alternative) rather than an obvious fact, so recording it here rather than only in
code comments.

**Decision needed:**
- [ ] Confirm reusing `AuditEntity.CreatedBy`/`CreatedAtUtc` is acceptable, or
      specify dedicated `PerformedByUserId`/`PerformedAtUtc` columns instead.

## 2. `IAuditService.LogAsync` stages an entry; it does not save it

**What exists today:** `LogAsync` calls `IAuditLogRepository.AddAsync` (tracked,
unsaved) and returns the new entry's id - it never calls
`IAuditLogRepository.SaveChangesAsync`. The one concrete caller in this codebase
(`InvoiceService.UpdateAsync`, WP-013 task 4) stages the entry, then continues on to
its own pre-existing `_invoiceRepository.SaveChangesAsync()` call - since both
`IAuditLogRepository` and `IInvoiceRepository` resolve to the same `AppDbContext`
instance within one request/pipeline-run's DI scope, that single call commits the
invoice's status change and the audit entry describing it together, atomically. See
`AuditLogRepositoryTests.UpdateInvoiceStatus_RealContext_CommitsInvoiceAndAuditEntryTogether_InOneSaveChangesCall`
(`APFlow.Infrastructure.Tests`) for this proven end to end against a real
`AppDbContext` - not just reasoned about.

**Why this default was chosen:** mirrors the Chief Technical Architect's own WP-010
ruling almost exactly - "`DuplicateDetectionService` should not call
`SaveChangesAsync` itself... keep it a pure compute service... the calling
orchestrator is responsible for invoking `CheckAsync` and persisting the result." An
audit log entry that could be committed independently of the change it describes
(or vice versa) would defeat the entry's own purpose - an audit trail that says a
status change happened when it didn't (or omits one that did) is worse than no
entry at all.

**Consequence flagged, not a defect:** because of this, `LogAsync`'s return value is
only the new entry's `Guid` id, not a full `AuditLogDto` - the DTO's
`PerformedByUserId`/`PerformedAtUtc` fields are not genuinely accurate until the
caller's own save completes (they're stamped by that save, not by `LogAsync`), so
returning a DTO that looked fully populated before that point would be misleading.

**Decision needed:**
- [ ] Confirm this atomic-with-caller design is correct, or specify that
      `IAuditService` should own persistence itself (which would reopen the
      "independent commit" risk above and needs an explicit call on the trade-off).

## 3. Task 4 scope: status changes only, not "all invoice actions"

**What exists today:** the only automatic audit trigger implemented is
`InvoiceService.UpdateAsync` staging an entry when `Status` actually changes
(compared before mutating the tracked entity). `CreateAsync`, `DeleteAsync`, and
`AddNoteAsync` do not stage any audit entry.

**Why this is flagged:** WP-013's Objective says "provide a complete audit trail
for **all invoice actions**," but task 4, the only concrete trigger specified, says
"Automatically log invoice status changes" - narrower than the Objective's framing.
Per `02_Project_Standards.md` §7 ("never fabricate implementation details not
provided in the work package"), the literal, narrower task item was implemented
rather than guessing the Objective's broader framing was also meant to be a
concrete requirement. Extending automatic logging to creation/deletion/notes is a
small, mechanical addition once confirmed (the same `IAuditService.LogAsync` call,
staged the same way, in each of those three methods) - deliberately not done
speculatively.

**Decision needed:**
- [ ] Confirm status-changes-only is the intended WP-013 scope, or specify that
      `CreateAsync`/`DeleteAsync`/`AddNoteAsync` should also stage entries (and, if
      so, what `Action` constant and Previous/New Value shape each should use -
      e.g. does a "note added" entry's `NewValue` hold the note content itself, or
      just note that one was added?).

## 4. Supplier resolution to `Invoice`'s new field: N/A - no schema coupling to WP-012

Not a WP-013 decision, but recorded here for traceability: WP-013 does not touch
`Invoice.SourceDocumentBlobName`, `IDuplicateDetectionService`, or
`InvoiceProcessingService` at all. The Chief Technical Architect's separate WP-010
sign-off (persist `IsPotentialDuplicate`/`DuplicateCheckReason` as new `Invoice`
fields) is a distinct, already-approved backlog item against WP-010/WP-012, not
folded into this work package - WP-013's task list doesn't touch duplicate
detection, and bundling an unrelated approved change into this delivery would blur
which work package actually did what. Once that follow-up work package lands and
also calls `InvoiceService.UpdateAsync` (or wherever it ends up), it will
automatically get status-change audit logging for free via this WP-013 change, with
no coordination required.

## 5. No `Update`/`Remove` on `IAuditLogRepository`

**What exists today:** `IAuditLogRepository` exposes only `GetByIdAsync`,
`QueryAsync`, `AddAsync`, and `SaveChangesAsync` - no way to modify or delete an
audit log entry through the application at all (not even the soft-delete every
other `AuditEntity`-derived type gets via `DbSet.Remove`, since nothing calls
`Remove` on this DbSet anywhere in this codebase).

**Why this wasn't flagged as open:** an audit trail that could be edited or removed
through the same application it is auditing would defeat its own purpose - this
isn't a judgment call so much as the whole point of the feature. Recorded here only
so the omission reads as deliberate, not an oversight.

## Related

- `docs/WP-010-Duplicate-Flag-Persistence-Decision.md` - the WP-010 ruling this
  WP-013 delivery's "stage, don't save" design directly mirrors (see item 2 above).
