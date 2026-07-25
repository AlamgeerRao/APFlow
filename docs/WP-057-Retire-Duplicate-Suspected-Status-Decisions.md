# WP-057 — Retire `DUPLICATE_SUSPECTED` Status Row: Report

**Status:** Complete. One framing discrepancy is flagged below (not a blocker -
proceeded with the task as given, per the reasoning explained), plus one
consequence flagged for follow-up (the canonical reference document itself was
not edited).

## Framing discrepancy, flagged not silently accepted

This work package's objective states the retirement was "flagged as
out-of-scope in WP-053." That is not quite what WP-053's own decisions document
says. Re-reading `docs/WP-053-Transition-Enforcement-Decisions.md`'s
"Discrepancy 2" directly:

- `06_Domain_Reference_Data.md`'s revision history said `DUPLICATE_SUSPECTED`'s
  continued relevance was **under review, explicitly not yet resolved** ("do not
  assume `DUPLICATE_SUSPECTED` is still reachable until that is resolved").
- WP-053 deliberately did **not** delete the `StatusReference` row on that basis
  - not because deletion was "out of scope" in the sense of being already-decided
    but deferred for later technical convenience, but because the underlying
    business question (should this status be retired at all?) had not been
    resolved, and WP-053's own scope was transition enforcement, not status
    catalogue changes.
- WP-053's own words: "If the intent is to remove the status entirely, that
  needs a follow-up work package - a `StatusReference` deletion, which this one
  was not scoped to make."

Read literally, WP-053 flagged the *technical action* (a `StatusReference`
deletion) as needing its own work package - it did not itself resolve or record
that the retirement had been decided. This work package's arrival, naming that
exact action and citing WP-053 as the reason, is being treated here as that
decision now having been made and communicated via the normal mechanism this
project uses for delivering Architect-approved requirements to a Backend
Engineer (a Developer Work Package, per `05_Development_Workflow.md`) - not as
something requiring a separate escalation before proceeding. Flagged here so the
distinction between "WP-053 deferred this" (accurate) and "WP-053 flagged this as
out-of-scope [implying already-approved]" (this work package's framing) is on
the record, per `06_Domain_Reference_Data.md`'s own instruction to follow the
document/history rather than a work package's characterization of it where they
disagree.

## Task 1 — Migration

`WorkflowSeedData.cs`'s `PlatformDefaultStatuses` and `GbSkipsStatuses` no
longer include the two `DUPLICATE_SUSPECTED` rows (ids
`00000000-0000-0000-0002-000000000004` and
`00000000-0000-0000-0003-000000000004`). Per this codebase's established
convention (every schema/seed-data change ships as a generated migration, never
hand-written SQL), the row removal itself is expressed as a source change to the
seed data list, and the actual migration was generated from that diff:

```
dotnet ef migrations add RemoveDuplicateSuspectedStatus --project src/APFlow.Infrastructure --startup-project src/APFlow.Infrastructure --output-dir Persistence/Migrations
```

This produced exactly two `DeleteData` calls (one per row, one per template) -
nothing else touched. `dotnet ef migrations add` printed its generic "may result
in the loss of data" warning, expected and correct for any `DeleteData` -
verified there is no other narrowing hidden alongside it (see the generated
`.cs` file: only the two expected `DeleteData` calls, and a `Down` that
correctly re-inserts both original rows verbatim if ever rolled back).

**Merge note:** this work package's own source drop predated WP-056 (still
missing `InvoiceExtractedFieldKeys.Currency` and other WP-056 content), so its
migration was regenerated fresh against the current `main` model rather than
copied in verbatim, avoiding any risk of migration-history drift stacking on
top of WP-056's own migration. The resulting migration is
`20260725112900_RemoveDuplicateSuspectedStatus` (not
`20260725102654_RemoveDuplicateSuspectedStatus` as in the source drop). As with
WP-055/WP-056 before it, only the files this WP's own "Files modified" list
below names were merged; unrelated stale content in the drop (frontend files,
`InvoiceQueryService.cs`/`InvoiceListItemDto.cs`/`InvoiceQueryParameters.cs`,
`CurrentUserService.cs`, `InvoiceExtractedFieldKeys.cs`, other `docs/WP-*`
files, `.claude/settings.local.json`) was left untouched.

## Task 2 — Confirmation, re-verified after removal

**Source grep, run again after the seed-data edit** (not just trusted from
before), across everything except historical migration files (which are
append-only history and correctly still show the original `INSERT` - migration
files are never edited after the fact in this codebase):

```
$ grep -rn "DuplicateSuspected|DUPLICATE_SUSPECTED" src --include=*.cs | grep -v "/Migrations/"
src/APFlow.Infrastructure/Persistence/WorkflowSeedData.cs:60:    /// "DUPLICATE_SUSPECTED" is NOT included (WP-057 retirement) - see
src/APFlow.Infrastructure/Persistence/WorkflowTransitionSeedData.cs:33:/// <b>DUPLICATE_SUSPECTED has no transitions - and, as of WP-057, no longer
src/APFlow.Infrastructure/Persistence/WorkflowTransitionSeedData.cs:43:/// DUPLICATE_SUSPECTED-related transition rows exist here, now or previously.
src/APFlow.Infrastructure/Persistence/WorkflowTransitionSeedData.cs:62:    /// DUPLICATE_SUSPECTED (retired entirely as of WP-057 - see this class's own
src/APFlow.Domain/Common/Constants/InvoiceStatusCodes.cs:28:    public const string DuplicateSuspected = "DUPLICATE_SUSPECTED";
```

Every remaining hit is either a doc comment explaining the retirement, or the
`InvoiceStatusCodes.DuplicateSuspected` constant itself - deliberately kept (see
"Scope note" below), not a live reference to a seeded row. Confirmed separately
that the current (post-migration) `AppDbContextModelSnapshot.cs` contains zero
occurrences of `DUPLICATE_SUSPECTED` at all - the model's *current* state, as
opposed to migration history, has no trace of the row.

**Automated, re-runnable confirmation query** (the more durable form of "confirm
no remaining reference" than a one-off manual query - it re-verifies on every
future test run, not just today): a new test,
`DuplicateSuspectedStatus_NoLongerExistsInEitherTemplate_AfterWP057Removal`
(`WorkflowTemplateRepositoryTests`, run against a real `AppDbContext` with the
actual `WorkflowSeedData`/`WorkflowTransitionSeedData` seeded data, not a hand
-constructed fixture), asserting for BOTH the platform-default and GB Skips
templates: `DUPLICATE_SUSPECTED` is absent from `Statuses`, and absent from
either side of every `Transitions` edge. Output: **pass** (see Build & Test).

**Scope note - the constant was kept, not removed:** `InvoiceStatusCodes.DuplicateSuspected`
still exists as a named constant. Task 1 scoped this work to a `StatusReference`
row deletion (a data change); removing the C# constant would be a broader source
change the task didn't ask for, and the constant remains a legitimate reference
for the SA-007 code even though it's no longer seeded (e.g. for any historical
`Invoice.Status` value still sitting in a real database from before this
migration - `Invoice.Status` is a plain string with no foreign-key constraint to
`StatusReference.Code`, so an old row at this status is unaffected by the
catalogue change and the constant remains meaningful for anyone querying such
rows). No code path sets `Invoice.Status` to this value anywhere in this
codebase (confirmed by the grep above) - only the seed data referenced it, and
that reference is now gone.

## Consequence flagged, then resolved (2026-07-25)

`docs/AI/06_Domain_Reference_Data.md` §2 still listed `DUPLICATE_SUSPECTED` in
its platform-default status table at the time this work package closed - this
repository's copy of that canonical, "Approved — Permanent Reference" document
was **not** edited as part of this work package. Per that document's own AI
Agent Rules, changes to it are the Chief Technical Architect's/Product Owner's
to make. Flagged in the WP-057 status report to the Chief Technical Architect
rather than edited unilaterally.

**Resolution:** the Architect's own maintained copy of the document already
had `DUPLICATE_SUSPECTED` removed and a fuller Revision History (three entries,
not one) - this repository's tracked copy was simply lagging behind, the exact
same stale-copy problem `docs/WP-053-Transition-Enforcement-Decisions.md` hit
against this same document. The Architect's copy was committed into the repo
verbatim (one cosmetic fix applied on the Architect's end first: the two
original revision-history entries were mashed onto one line without a break).
See `README.md`'s WP-057 row and the commit that lands this fix for
confirmation.

## Files modified

- `src/APFlow.Infrastructure/Persistence/WorkflowSeedData.cs` - removed the two
  `DUPLICATE_SUSPECTED` seed rows; doc comments updated
- `src/APFlow.Infrastructure/Persistence/WorkflowTransitionSeedData.cs` - doc
  comments updated to reflect the row no longer existing (previously described
  it as "remains a seeded, valid `StatusReference` row")
- `src/APFlow.Infrastructure/Persistence/Migrations/20260725112900_RemoveDuplicateSuspectedStatus.cs`
  / `.Designer.cs` / `.sql` (new; regenerated fresh against current `main` -
  see "Merge note" above)
- `src/APFlow.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` -
  regenerated by `dotnet ef migrations add`
- `tests/APFlow.Infrastructure.Tests/Persistence/WorkflowTemplateRepositoryTests.cs` -
  fixed a status-count assertion (14 → 13) that the removal broke; added the new
  automated confirmation test
- `README.md` - added WP-057 row; flagged the now-stale `06_Domain_Reference_Data.md`
  §2 table entry in the open-decisions list
- `docs/WP-057-Retire-Duplicate-Suspected-Status-Decisions.md` (this file)

## Migration

```
dotnet ef migrations add RemoveDuplicateSuspectedStatus --project src/APFlow.Infrastructure --startup-project src/APFlow.Infrastructure --output-dir Persistence/Migrations
```

Two `DeleteData` calls against `StatusReferences`, one per template, matched by
each row's fixed seed id - no other table or row touched. `.sql` rendering
included alongside for review without the EF tooling.

**Verified against a real, running SQL Server** (`localhost\SQLEXPRESS` on this
machine, not `(localdb)\mssqllocaldb` - see local dev environment notes):
`dotnet ef database update` applied cleanly, and both `DeleteData` rows were
independently confirmed gone via a direct query (`StatusReferences` now has
13 rows for the platform-default template, 15 for GB Skips - both down by
exactly one, as expected). Also exercised end to end through the new automated
test against `WorkflowTemplateRepository`/`AppDbContext`.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings, whole solution.
- `dotnet test` across all 5 test projects - **321/321 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Application.Tests` 149,
  `APFlow.Api.Tests` 40, `APFlow.Infrastructure.Tests` 76 (+1 new; one existing
  test's status-count assertion also corrected),
  `APFlow.Integrations.Tests` 45.
- `/health/live` → 200; `/health/ready` → 200, `Degraded` (Graph/Blob Storage
  unconfigured in this dev sandbox, per WP-004's severity ruling - not
  `Unhealthy`; database connectivity independently confirmed by the migration
  verification above).
