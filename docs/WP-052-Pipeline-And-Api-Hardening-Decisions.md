# WP-052 — Pipeline & API Hardening: Report

**Status:** Complete. Two genuine gaps are flagged for sign-off, not silently
resolved (Part D's extraction-confidence requirement; the WP-015 fixture was
unavailable).

## Part A — EF Core Migration Workflow

**Squashed, not appended.** The three existing incremental migrations (WP-048's
`InitialCreate`, WP-050's `AddWorkflowEngine`, WP-051's `AddApprovalPolicy`) were
deleted and replaced with a single fresh `InitialCreate` baseline generated against
today's full model (Invoices/Suppliers/InvoiceNotes, `AuditLogs`, the WP-050
workflow-engine tables, `ApprovalPolicies`). Safe to do because none of these
migrations had ever been applied to a real, persistent database anywhere - this
project has never had a deployed database (confirmed at WP-048/050/051, no LocalDB
support on Linux at the time).

**Genuinely verified this time, not just script-generated.** Previous work
packages (WP-048, WP-050, WP-051) could only generate and inspect migration SQL,
never actually apply it - LocalDB doesn't exist on Linux. For WP-052, a real SQL
Server instance was installed in this sandbox (native Ubuntu package, not LocalDB;
worked around one real obstacle - a `liblber`/OpenLDAP ABI mismatch between the
Ubuntu 24.04 sandbox and the 22.04-targeted SQL Server packages, resolved by
installing the compatible `libldap-2.5-0` build from the Ubuntu 22.04 archive) and
run without systemd (this sandbox has no init system) by invoking `sqlservr`
directly, the same way the official Docker image does. `dotnet ef database update`
was run for real against this instance, and the result was independently verified
by querying the database directly (not just trusting a "Done." message): all 9
expected tables exist, with exactly the expected seed row counts (2
`WorkflowTemplates`, 30 `StatusReferences`, 1 `ApprovalPolicies`, 1
`WorkflowTransitions`).

**Documentation:** `05_Development_Workflow.md` itself is not present in this
repository working copy (only ever supplied as directly-shared chat context, never
a tracked file) - per the task's own "(or a short addendum)" allowance,
`docs/05_Development_Workflow_Addendum.md` records the new policy (every
model-changing work package must include a generated EF Core migration, never a
hand-written SQL file) for whoever maintains the master copy to merge in.
`docs/WP-046-Role-Catalogue-Remediation.md` was annotated in place (not rewritten)
with a "RESOLVED by WP-052 Part A" note against its own flagged gap.
`docs/WP-047-Duplicate-Matching-Reconciliation.md` was checked directly and never
referenced `db/migrations/*.sql` in the first place (no schema change in that WP) -
no correction was needed or made there.

## Part B — Content-Hash-Based Ingestion Idempotency Key

`Invoice.SourceDocumentContentHash` (lowercase hex SHA-256, 64 chars) added.
Computed once, in `InvoiceProcessingService.ProcessAttachmentAsync`, from the byte
array WP-007's extraction already produced in memory - no re-read. The idempotency
check now compares this hash instead of `SourceDocumentBlobName`;
`SourceDocumentBlobName` itself is unchanged in every other respect (still
computed, still stored, still the storage-path/traceability field) - only its
former role as the dedup key was removed, per the task's explicit scoping.

Both required test scenarios added and passing: two attachments sharing a file
name but with different content are both processed (the old blob-name key would
have silently collided them); two attachments with different file names but
identical content are correctly deduplicated (only the first is saved, the second
reports `AlreadyProcessed` against the same invoice id).

Several pre-existing tests broke as an expected, understood side effect: `WP-049`'s
shared `NewAttachment()` test helper always used the same 3 hardcoded bytes
regardless of file name, so tests that processed multiple "different" attachments
under different names were now (correctly) being deduplicated by content. Fixed by
making the helper derive distinct content per distinct file name, rather than
weakening the new dedup logic.

## Part C — Extended Automatic Audit Logging

`AuditActions.InvoiceCreated`/`InvoiceDeleted`/`NoteAdded` added.
`InvoiceService.CreateAsync`/`DeleteAsync`/`AddNoteAsync` each now stage an entry
via `IAuditService.LogAsync` (never `LogAndSaveAsync` - see Part D below for why
that distinction matters) immediately before their own existing
`SaveChangesAsync` call, so each commits atomically with the operation it
describes - no independent commit anywhere, per WP-013's established pattern and
this task's own explicit instruction.

A shared `InvoiceAuditSnapshot` (`SupplierId`, `SupplierName`,
`SupplierInvoiceNumber`, `InvoiceDate`, `GrossTotal`, `Currency`, `Status`),
serialized to JSON via `System.Text.Json` with default (PascalCase) property
naming, is used for both `CreateAsync`'s `NewValue` and `DeleteAsync`'s
`PreviousValue` - deliberately the same shape for both, so a reviewer comparing a
deletion's snapshot against the corresponding creation's is looking at directly
comparable data. `AddNoteAsync`'s `NewValue` is the raw note content string, not
JSON, per the task's literal wording (no multi-field state to capture there).

Several pre-existing tests needed updating: `CreateAsync` now stages its own entry
unconditionally, so tests that previously asserted "zero audit entries exist" or
"exactly one entry exists" after a `CreateAsync` + `UpdateAsync`/`AddNoteAsync`
sequence needed to account for the additional `InvoiceCreated` entry - fixed by
filtering assertions to the specific action each test is actually about, not by
weakening the new logging.

## Part D — Invoice Detail API Endpoint

**First real API controllers in this codebase.** `builder.Services.AddControllers()`
and `app.MapControllers()` were already wired (WP-001) but nothing had ever been
registered to them - `InvoicesController` is the first. Both endpoints implemented
under `[ApiController] [Route("api/invoices")]`; the solution-wide fallback
authorization policy (WP-002) already requires an authenticated caller on every
action - no `[AllowAnonymous]` added. The controller adds no tenant-checking logic
of its own; tenant isolation comes entirely from the services it composes
(`IInvoiceService`'s query filter, `IBlobStorageService`'s tenant-prefixed blob
names), matching "APFlow.Api ... contains no business logic."

**`GET /api/invoices/{id}`** returns `InvoiceDetailResponse`: the existing
`InvoiceDto` (WP-009, including `IsPotentialDuplicate`/`DuplicateCheckReason` from
WP-048 and `SourceDocumentBlobName`) plus recent audit entries via
`IAuditQueryService.SearchAsync` (WP-013, now complete per Part C). A failed audit
query does not fail the request - the invoice is still returned, with an empty
history list, logged as a warning.

**`GET /api/invoices/{id}/download`** streams the blob directly through the API
(`IBlobStorageService.DownloadAsync` + `File()`) rather than issuing a SAS-URL
redirect - simpler, and avoids inventing a SAS-expiry-window decision nobody
specified; the task's wording explicitly allows either. Stages a `DocumentViewed`
entry on success. A failed audit-staging attempt does not block the document
response - the same "a missing audit entry is a smaller problem than refusing an
otherwise-authorized action" reasoning used at every other audit-staging call site
in this codebase.

### New: `IAuditService.LogAndSaveAsync`

The download endpoint's audit entry needed a genuinely new capability: every
existing `LogAsync` caller (`InvoiceService`'s four methods) always has some OTHER
change in the same method to commit the staged entry together with. A read-only
`GET` endpoint has no such other change - there is nothing for a bare `LogAsync`
call to ever actually get saved by. Added `LogAndSaveAsync` (stages via the same
internal logic, then immediately commits) specifically for this standalone-write
case - exactly the scenario `IAuditLogRepository.SaveChangesAsync`'s own doc
comment (WP-013) already anticipated ("exposed here... for any future caller that
genuinely needs a standalone audit-only write"). `LogAsync` itself is unchanged;
every existing caller continues to use it unmodified.

### Flagged, not silently resolved: per-field extraction confidence is not available

Part D asks the endpoint to return "extracted-field and confidence data (WP-008)."
`Invoice`'s own doc comment (WP-009) explicitly excluded persisting this,
reasoning it should wait for "a real requirement" to justify the entity's growth -
this task is arguably exactly that requirement arriving, but satisfying it means
extending what the ingestion pipeline PERSISTS (a schema change capturing
confidence per field), which is a different, larger scope than "build an API
endpoint over data that already exists" - Part D's own framing. Nothing in this
codebase retains WP-008's confidence scores past the original pipeline run for any
invoice, past or future, under the current schema. Rather than silently omitting
the requirement or inventing a schema change unscoped by this task,
`InvoiceDetailResponse.ExtractionConfidenceNote` makes the gap visible directly in
the API response itself: a fixed, documented string explaining why, not real data.
Confirm whether a follow-up work package should extend `Invoice` to persist
per-field confidence (closing the exact gap WP-009's own doc comment anticipated),
or whether this requirement should be dropped/deferred.

### Flagged: no WP-015 fixture was available to reconcile against

The task asks to "reconcile field names/casing against WP-015's fixture-proposed
list." No such fixture was available in this delivery's working context.
Proceeded with `InvoiceDto`'s existing field names verbatim (PascalCase in C#,
serialized as camelCase JSON via ASP.NET Core's default controller-API naming
policy - no custom `JsonSerializerOptions` configured), reusing `InvoiceDto`
(WP-009) and `AuditLogDto` (WP-013) as-is rather than inventing new names, per the
task's own "do not introduce a third, incompatible naming scheme" instruction.
Confirm this matches WP-015's fixture once it's available; if it doesn't, the
correction is a rename in `InvoiceDetailResponse`/`InvoiceDto`, not a redesign.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings.
- `dotnet test` across all 5 test projects - **274/274 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Api.Tests` 27 (19 carried over + 8 new
  controller tests), `APFlow.Infrastructure.Tests` 72, `APFlow.Integrations.Tests`
  45, `APFlow.Application.Tests` 119 (114 carried over + 5 new: 2 Part B, 3 Part C).
- Both migrations (`InitialCreate`, `AddSourceDocumentContentHash`) verified via
  `dotnet ef database update` against a real, running SQL Server instance, with
  the resulting schema independently queried and confirmed to match expectations
  exactly (see Part A above for detail).
