# WP-012 — Invoice Processing Pipeline: Decisions Requiring Sign-Off

**Status:** OPEN — implemented with reasoned defaults; needs explicit sign-off.
**Owner:** Chief Technical Architect.
**Raised:** WP-012 delivery.

WP-012's task list ("Create InvoiceProcessingService", "Orchestrate: Email Sync,
PDF Extraction, Blob Storage, Document Intelligence, Duplicate Detection, Database
Save", "Add retry handling", "Add structured logging", "Ensure pipeline is
idempotent", "Return processing results") describes an orchestration shape, not a
fully-specified design - actually wiring six existing services together forces
several judgment calls none of WP-005 through WP-011 (each of which built one piece
in isolation) had to make. Each is implemented with a reasoned default below,
following this project's established pattern (see WP-003/004/005/010's decision
docs) of flagging rather than silently deciding.

## 1. `Invoice.SourceDocumentBlobName` — a new entity field

**What exists today:** a new nullable `string? SourceDocumentBlobName` property on
`Invoice`, mapped with `HasMaxLength(1024)` in `InvoiceConfiguration` (matching
`BlobStorageService.MaxPhysicalBlobNameLength`, since it stores a full logical blob
name). Also added to `CreateInvoiceRequest` (optional, defaults to null - existing
WP-009 call sites are unaffected) and `InvoiceDto`.

**Why this wasn't just left out:** WP-009 explicitly excluded any Blob Storage
reference from `Invoice`, reasoning that "inventing a storage linkage now would
presume a strategy... nobody has decided." WP-012 is that decision point - Blob
Storage is now an explicit, required orchestration step, and without a durable
reference back to the stored PDF, uploading it would be write-only (no way to
retrieve it later, and no way to make the pipeline idempotent - see item 2 below).
Adding one field, mirroring `SourceEmailMessageId`'s existing shape and reasoning
exactly, was judged the smallest change that closes this gap without inventing
anything beyond what WP-012 itself now requires (no per-attachment vs per-invoice
strategy question, no retention policy, no second blob type - just "the logical
name of the one PDF this invoice came from").

**Decision needed:**
- [ ] Confirm a single traceability field (rather than, e.g., a dedicated
      `InvoiceDocument` entity, or storing the SAS URL instead of the logical name)
      is the right shape for this MVP.

## 2. Idempotency mechanism: `SourceDocumentBlobName` as the dedup key

**What exists today:** before doing any work for a PDF attachment, the pipeline
computes a deterministic logical blob name (`invoices/{messageId}/{fileName}`) and
checks (via `IInvoiceService.GetAllAsync()`, an in-memory scan - see item 6) whether
an invoice with that `SourceDocumentBlobName` already exists. If so, the attachment
is skipped entirely (no re-upload, no re-analysis, no new database row) and
reported as `AlreadyProcessed`. Combined with only marking an email as processed
once every attachment succeeded (see `IInvoiceProcessingService`'s doc comment), a
partial failure mid-run is always safe to retry: already-saved invoices are
skipped, only the genuinely-failed attachment(s) are retried.

**Known limitation:** the dedup key is `messageId + fileName`, not a content hash.
Two distinct PDF attachments on the same email that happen to share a file name
(e.g. both literally named `invoice.pdf`) would collide and the second would be
silently treated as already-processed. This is judged acceptably rare for MVP
(Graph typically de-duplicates/renames identical attachment names, and this is the
same class of trade-off WP-010 already accepted for its own matching heuristics),
but is a real, known gap - not something silently assumed to be impossible.

**Decision needed:**
- [ ] Confirm `messageId + fileName` is an acceptable idempotency key, or specify a
      content-hash-based key instead (e.g. SHA-256 of the PDF bytes) if the
      same-filename collision risk is not acceptable.

## 3. Supplier resolution: case-insensitive trimmed exact-name match, create-if-absent

**What exists today:** `ResolveSupplierAsync` loads every supplier visible to the
tenant (via `ISupplierService.GetAllAsync()`) and looks for a case-insensitive,
trimmed exact match against the extracted `SupplierName`. If none matches, a new
`Supplier` is created automatically via `ISupplierService.CreateAsync`.

**Why this default was chosen:** `Invoice.SupplierId` is a required, non-nullable
foreign key, and nothing upstream of this pipeline (WP-006 through WP-008) resolves
or creates suppliers - Document Intelligence returns only a free-text
`SupplierName` string. Some resolution strategy has to exist for the pipeline to
save anything at all. Exact-match-after-normalization is the simplest strategy that
doesn't fabricate a fuzzy-matching algorithm nobody asked for.

**Known limitation:** OCR/extraction variance ("Acme Ltd" vs "Acme Ltd." vs "ACME
LIMITED") will create duplicate `Supplier` rows for what a human would recognize as
the same company. No fuzzy matching, alias table, or manual-merge capability exists
anywhere in this codebase yet.

**Decision needed:**
- [ ] Confirm auto-creating a `Supplier` on first sight is acceptable, or specify
      that unmatched supplier names should instead fail the item and queue it for
      manual supplier assignment (which would require a review capability that does
      not yet exist - "Approval"/"Query workflow" are explicit WP-012 out-of-scope).
- [ ] Confirm exact-match-after-normalization is an acceptable MVP matching
      strategy, or specify a fuzzy-matching approach.

## 4. No extracted supplier name → the item fails (no placeholder supplier)

**What exists today:** if Document Intelligence returns a null/blank
`SupplierName`, `ResolveSupplierAsync` returns a failure
(`InvoiceProcessing.SupplierNameNotExtracted`) rather than creating a placeholder
"Unknown Supplier" record. The attachment is reported as `Failed`; the source email
is left unmarked so this is retried on a later run (which will fail identically
until the document is manually intervened upon - there is no automatic recovery
path for this specific case).

**Why this default was chosen:** fabricating a shared "Unknown Supplier" bucket
Supplier is a business-policy decision (do such invoices later get manually
reassigned? merged? does grouping them under one Supplier row cause its own data
problems?) with no basis in any of WP-001 through WP-011's documentation - exactly
the kind of invention `02_Project_Standards.md` §7 prohibits. Failing the item, with
a specific, machine-readable error code, was judged the safer default: it produces
no data at all rather than data that might need to be found and cleaned up later.

**Decision needed:**
- [ ] Confirm fail-the-item is correct, or specify a placeholder-supplier / manual-
      queue strategy (the latter would need a review capability this pipeline does
      not build).

## 5. Duplicate detection now persists its result (updated 2026-07-20 - superseded)

**Original position (as delivered):** the pipeline called
`IDuplicateDetectionService.CheckAsync` after saving each invoice and included
`IsPotentialDuplicate` in its own `InvoiceProcessingItemResult`, logging a warning
if a duplicate was flagged, but persisted nothing beyond that - no new `Invoice`
column, no automatic `InvoiceNote`. This was deliberate: resolving
`docs/WP-010-Duplicate-Flag-Persistence-Decision.md`'s still-open persistence
question as a side effect of this unrelated work package would have been exactly
the kind of undirected scope expansion `05_Development_Workflow.md` §9 prohibits.

**What exists today:** the Chief Technical Architect resolved
`WP-010-Duplicate-Flag-Persistence-Decision.md` on 2026-07-20 (ruling: persist, as
new `Invoice` fields, orchestrator-owned write) and it was implemented the same
day - see that document's own "## Implementation" section for the full detail.
This pipeline's `ProcessAttachmentAsync` now calls a new private
`PersistDuplicateCheckResultAsync` immediately after a successful `CheckAsync`,
writing `Invoice.IsPotentialDuplicate`/`DuplicateCheckReason` via a new
`IInvoiceRepository` dependency this class gained specifically for that purpose
(`DuplicateDetectionService` itself still has no `SaveChangesAsync` access - the
ruling was explicit that it must stay a pure compute service). A failed duplicate
check still does not fail the item and still leaves the invoice's persisted flag
at its prior value - that part of the original design is unchanged.

**Decision needed:** none - fully resolved and implemented; this item is kept
here (rather than deleted) as the historical record of why the pipeline's shape
changed after initial delivery.

## 6. Idempotency and supplier-matching both do full in-memory scans

**What exists today:** both the idempotency check (`IInvoiceService.GetAllAsync()`)
and supplier resolution (`ISupplierService.GetAllAsync()`) load every tenant
invoice/supplier into memory per attachment processed, rather than a targeted,
indexed query.

**Why this wasn't optimized:** this is the identical trade-off already accepted and
documented in `docs/WP-010-Duplicate-Flag-Persistence-Decision.md`'s own "Also
raised at QA review" section for `DuplicateDetectionService.CheckAsync` - correct
and acceptable at current/expected MVP volume, with no targeted lookup method
existing on either `IInvoiceRepository` or `ISupplierRepository` today. Adding one
wasn't requested by WP-012 and would be speculative optimization for a cost that
doesn't yet exist.

**Decision needed:** none - flagging only so this doesn't need rediscovering later.
A `GetBySourceDocumentBlobNameAsync`-style addition to `IInvoiceRepository` (and an
equivalent name-lookup on `ISupplierRepository`) would be the natural fix if/when
this becomes a real cost, same as WP-010's own note.

## 7. Retry policy: 3 attempts, linear backoff, applied to reads/idempotent-writes only

**What exists today:** a generic in-class retry helper (built on `Task.Delay` only -
no new package, per `02_Project_Standards.md` §2's "keep dependencies to a
minimum") retries each of Email Sync, PDF Extraction, Blob Upload, Document
Intelligence Analysis, and Mark-as-Processed up to 3 times with a 500ms/1000ms
linear backoff between attempts. Database Save and the duplicate-detection read are
deliberately NOT wrapped in this retry loop - both go through `AppDbContext`, whose
`SqlServer` options already configure `EnableRetryOnFailure` (see
`APFlow.Infrastructure.DependencyInjection.AddDatabase`); a second, uncoordinated
retry loop here would risk conflicting with that already-documented execution
strategy rather than adding real resilience.

**Why 3 attempts / this backoff:** an arbitrary, reasoned default - WP-012's task
list says "Add retry handling" without specifying attempt counts or backoff shape.
Chosen to absorb brief transient Azure/Graph blips without meaningfully slowing
down a pipeline run that has genuinely failed for a non-transient reason.

**Decision needed:**
- [ ] Confirm 3 attempts / linear backoff is acceptable, or specify different
      values. (No decision needed on the "don't wrap Database Save" choice itself -
      that's a documented existing execution-strategy conflict, not a preference.)

## 8. Invoice status advances to `Extracted` after a successful save (not left at `Received`)

**What exists today:** `IInvoiceService.CreateAsync` always starts a new invoice at
`InvoiceStatus.Received` (its own documented invariant, kept stable for other,
non-pipeline callers). Immediately after a successful save, this pipeline calls
`IInvoiceService.UpdateAsync` to advance the same invoice to
`InvoiceStatus.Extracted` - using the existing, already-supported update path, not
a special case added to `CreateAsync`. If that update call itself fails, the
invoice remains `Received` (a warning is logged) rather than failing the whole item
- the invoice is still safely saved and correct, just with a less specific status.

**Why this isn't listed as an open decision:** `InvoiceStatus.Extracted`'s own doc
comment already describes exactly this pipeline's end state ("PDF attachment
extracted (WP-007) and analyzed (WP-008); structured data is available") - this
isn't a judgment call so much as using the enum value the way it was already
documented to be used. Recorded here only so the two-step save-then-advance
sequence (rather than a single write) is traceable to a reason, not mistaken for an
oversight.

## Related

- `docs/WP-003-Tenant-Isolation-Decision.md`, `docs/WP-004-Graph-Multitenancy-Decision.md`,
  `docs/WP-004-Health-Check-Severity-Decision.md`, `docs/WP-005-Blob-Storage-Tenant-Isolation-Decision.md`,
  `docs/WP-010-Duplicate-Flag-Persistence-Decision.md` - same "implement a reasoned
  default, flag rather than silently decide" pattern.
