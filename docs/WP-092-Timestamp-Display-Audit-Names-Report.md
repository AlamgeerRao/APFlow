# WP-092 — Consistent Timestamp Display & Audit Trail Display Names: Report

**Status:** Done. Committed only — not pushed, not deployed, not live-verified
this round, per explicit session instruction.

**Role:** Backend + Frontend Engineer. **Sprint:** Sprint 2. **Dependencies:** None.

---

## History

Most of this WP (Part A's timestamp audit, and Part B's backend
`PerformedByDisplayName` capture) was built in an earlier session and sat
committed only inside a worktree, never merged to `main`. This session found
that worktree, verified its build/tests were still green against current
`main`, and found one real gap against the spec before merging: **Part B task
6 ("Update `AuditLogDto`/`AuditSummaryPanel`") had only been done on the
backend DTO — the frontend consumer that actually renders the audit list
(`invoiceDetailMapping.ts`) still discarded the new field entirely and
rendered a raw user-id guid instead of a name.** Completed that gap this
session before merging. Everything below describes the final, complete state.

---

## Part A — Timestamps: viewer-local everywhere, including Received

**Task 1 (audit every timestamp display):** Every timestamp in the app already
routed through the shared `formatDate`/`formatDateTime`
(`src/APFlow.Web/src/utils/format.ts`) except two gaps, both fixed here:

**Task 2 (fix `Received`):** `InvoiceHeaderSummary.tsx`'s "Received" field
previously called `formatDate(invoice.receivedAt.slice(0, 10))` — truncating
the real ISO timestamp down to a date-only string before formatting, so it
rendered "01 Jul 2026" instead of a real date+time value even though
`receivedAt` genuinely carries a time component
(`InvoiceDetail.receivedAt`, mapped straight from `InvoiceDto.CreatedAtUtc`).
Now calls `formatDateTime(invoice.receivedAt)` directly, same as every other
timestamp field. New test:
`InvoiceHeaderSummary.test.tsx`'s `renders Received as date and time, not
date-only` — asserts `01 Jul 2026, 08:00`, not the old date-only value.

**Task 3 (consolidate a second, duplicate implementation):** `AuditSummaryPanel.tsx`
had its own local `formatTimestamp`, written during WP-072 to fix a real crash
(`RangeError: Invalid time value` on `new Date(undefined)`) but never
consolidated with the shared `formatDateTime` in `format.ts`, which had the
identical bug independently (it threw on `null`/`undefined`/unparseable
input rather than falling back). `format.ts#formatDateTime` is now
`(isoTimestamp: string | null | undefined): string`, applying the exact
`Number.isNaN(date.getTime())` guard `formatDate`/`AuditSummaryPanel`'s own
local copy already used — same fallback contract, same `'—'` sentinel, now in
one place. `AuditSummaryPanel.tsx`'s local `formatTimestamp` is removed;
`format.test.ts` gained coverage for the null/undefined/unparseable cases now
handled by the shared function.

No other timestamp display in the app needed a change — every other call
site (Notes, Invoice Queue table, Pagination, etc.) was already routing
through the shared formatter correctly.

---

## Part B — Audit trail display names

**Task 4 (`AuditLog.PerformedByDisplayName`):** New nullable column
(`src/APFlow.Domain/Entities/AuditLog.cs`), populated once at staging time in
`AuditService.CreateAsync` from `ICurrentUserService.DisplayName` — the exact
pattern `InvoiceNote.AuthorDisplayName` (WP-055) already established: a
snapshot at the moment the action happened, not a live lookup, so it reflects
the actor's name at the time even if their Entra profile name later changes.
`AuditService` now takes `ICurrentUserService` as a constructor dependency
(it previously had none). `AuditLogConfiguration.PerformedByDisplayName` uses
`HasMaxLength(200)`, matching `InvoiceNoteConfiguration.AuthorDisplayName`'s
own convention exactly.

**Task 5 (migration):** `AddAuditLogPerformedByDisplayName` — a single
nullable `AddColumn`, no default value needed (nullable, so every existing
row simply gets `NULL`, correctly representing "no name was ever captured for
this historical row").

**Task 6 (`AuditLogDto`/`AuditSummaryPanel`, completed this session):**

- Backend: `AuditLogDto` gained `PerformedByDisplayName`, populated by
  `AuditQueryService` straight off the entity — no transformation.
- Frontend (the actual gap found and closed this session):
  `invoiceDetailMapping.ts`'s `AuditLogResponseDto` (the real wire-shape
  contract for `recentAuditEntries`) gained `performedByDisplayName: string | null`,
  and a new `resolveActorLabel` function replaces the old
  `entry.performedByUserId ?? 'system'` mapping:
  - A captured display name is used directly.
  - No captured name, but the action was performed by the system (background
    pipeline, no authenticated caller — `CreatedBy` stamped as the literal
    string `"system"`, or absent) renders as **"System"**.
  - No captured name, but a real actor performed it (a token with no name
    claim at the time, or a historical row from before this column existed)
    renders as **"Unknown user"** — never a raw guid, which is what the old
    code rendered and which is not "readable" by any real definition.
  `AuditSummaryPanel.tsx` itself needed no change — it already renders
  whatever `AuditEntry.actor` resolves to; the fix lives entirely in the
  mapping layer, the actual point where the real wire field was being
  discarded.

**Task 7 (tests):**

- Backend: `AuditServiceTests`/`AuditQueryServiceTests`/`AuditLogRepositoryTests`
  extended for the new field and the new `ICurrentUserService` constructor
  dependency; `InvoicesControllerTests`/`InvoiceServiceTests`/
  `InvoiceProcessingServiceTests` updated for `AuditService`'s changed
  constructor signature (an `ICurrentUserService` instance already existed in
  scope at every call site — none needed a new fake).
- Frontend (this session): `invoiceDetailMapping.test.ts` gained two new
  cases — `performedByDisplayName` preferred over `performedByUserId` when
  both are present, and the "Unknown user" fallback for a real actor with no
  captured name; the pre-existing null-fallback test was renamed and updated
  to assert `"System"` (was asserting the old lowercase `"system"`) and every
  existing audit-entry literal in the test file was extended with the new
  required field.

**Task 8 (live-verify):** Not performed this round — see "Explicitly NOT done
this round" below.

---

## Test results

Baseline before this WP (Sprint 1 final): **391** backend / **280** frontend
tests passing.

After this WP: **397** backend tests passing (11 Domain + 86 Infrastructure
(+2) + 45 Integrations + 193 Application (+4) + 62 Api, up from the 391
baseline); **287** frontend tests passing (was
280 baseline, +2 from `InvoiceHeaderSummary.test.tsx`'s new Received test and
`format.test.ts`'s new null/undefined coverage, +2 more from this session's
`invoiceDetailMapping.test.ts` additions, net +5 accounting for the renamed
existing test).

Full `dotnet test APFlow.sln` and `npx vitest run` both clean, no failures,
no skips. `npx tsc -p tsconfig.app.json --noEmit` clean.

---

## Explicitly NOT done this round

- **Not pushed.** Commits are local only, per this session's ground rules —
  the user pushes manually.
- **Not deployed.** No CI/CD run has been triggered by this work.
- **Not live-verified.** No live database migration has been applied to the
  dev environment; the "fresh entry shows a real name, an old entry shows the
  fallback" side-by-side confirmation the spec's Task 8 asks for has not been
  performed against real data. All verification in this round is via the
  backend/frontend test suites and local `dotnet build`/`dotnet test`/
  `npx vitest run`/`npx tsc --noEmit` runs.
