# WP-053 — Seed & Enable Workflow Transition Enforcement: Report

**Status:** Complete. Two discrepancies between the work package's wording and the
actual confirmed reference data are flagged below, not silently resolved.

## What this closes

WP-050's central open item. That work package built and fully tested
`IWorkflowValidationService` but deliberately never called it from a blocking
path, because neither template had a confirmed transition graph seeded - activating
enforcement then would have rejected *every* status change in the application.
WP-053 supplies both confirmed graphs, which is what finally makes activation safe.

## Discrepancy 1: `RECEIVED -> EXTRACTED` is not in the confirmed graph

WP-053 task 5 says the WP-013 regression test "should still pass, since Received →
Extracted is in the confirmed baseline graph." **It is not.** The confirmed table in
task 1 routes ingestion as `RECEIVED -> PROCESSING -> EXTRACTED` - there is no direct
`RECEIVED -> EXTRACTED` edge anywhere in it.

The WP-013 test (`AuditLogRepositoryTests.UpdateInvoiceStatus_RealContext_...`)
did exactly that direct jump, so with enforcement live it now correctly fails.
Rather than invent an unconfirmed `RECEIVED -> EXTRACTED` edge purely to make an
existing test pass - which would have silently widened the approved graph - the
test was updated to step through `PROCESSING`, exercising two genuinely-confirmed
transitions instead of one unconfirmed one. It now also asserts both resulting
audit entries, so it proves *more* than before, not less.

**Worth confirming:** the WP-012/WP-049 ingestion pipeline does not use
`InvoiceService.UpdateAsync` (WP-049 made it construct invoices directly at
`EXTRACTED` in a single atomic write - see
docs/WP-049-Wire-Duplicate-Detection-Into-Pipeline.md), so it is unaffected by
enforcement and continues to work. But that does mean a real ingested invoice
never passes through `PROCESSING` at all - it is created directly at `EXTRACTED`.
If `PROCESSING` is meant to be a real observed state during ingestion, that is a
pipeline change, not a transition-graph change, and is outside WP-053's scope.

## Discrepancy 2: `DUPLICATE_SUSPECTED` has not actually been removed

WP-053's preamble says "note: DUPLICATE_SUSPECTED has been removed — see its
revision history entry." The current `06_Domain_Reference_Data.md` (the version
supplied with this work package) still lists it in §2's table; its revision history
says its "continued relevance is under review separately... do not assume
`DUPLICATE_SUSPECTED` is still reachable **until that is resolved**" - i.e. under
review, explicitly not yet resolved, and not removed.

Followed the document rather than the work package's characterisation of it, per
`06`'s own AI Agent Rules. Practical effect is minimal and matches the intent
either way: `DUPLICATE_SUSPECTED` remains a seeded, valid `StatusReference` row in
both templates (removing it would be an unapproved schema/data change), but
WP-053's confirmed graphs contain **no edge into or out of it**, so it is now
genuinely unreachable in practice. Verified directly against the real database:
zero transitions reference it. If the intent is to remove the status entirely,
that needs a follow-up work package - a `StatusReference` deletion, which this one
was not scoped to make.

## Role gating: extended, and where it lives

WP-051 gated exactly one transition with a hardcoded `if` in
`InvoiceService.UpdateAsync`. WP-053 generalises this to all four gated
transitions via `APFlow.Domain.Common.Constants.RoleGatedTransitions`.

Placed in **Domain**, not Infrastructure's seed data, specifically because both
layers need it and Application must not reference Infrastructure (Solution
Structure §2): Application enforces the gate, Infrastructure documents it
alongside the seeded rows.

Deliberately **not** a new column on `WorkflowTransition`. WP-050 left that entity
without a required-role field on purpose, and WP-051 established that "who may
perform an approval-type action" lives in `ApprovalPolicy` (tenant-configurable
data). So: `RoleGatedTransitions` identifies *which* transitions are gated;
`ApprovalPolicy` for `ApprovalDomains.InvoiceApproval` supplies *which role* each
requires. All four currently resolve to the same role (`FINANCE_MANAGER`, per
`06_Domain_Reference_Data.md` §1's interim Full/Approver mapping) because they all
check that one policy. If a future requirement needs different roles per
transition, that means a second approval domain and policy row - not a role column
here.

## Enforcement wiring

`InvoiceService.UpdateAsync` now runs two checks, both **before any field is
mutated** (so a rejected attempt leaves the invoice completely untouched), and both
only when the status is actually changing (a plain field edit is not a transition
and is neither validated nor gated - covered by its own test):

1. `IWorkflowValidationService.ValidateTransitionAsync` - is this edge in the
   acting tenant's template at all?
2. `IApprovalAuthorizationService.AuthorizeAsync` - if the edge is role-gated, does
   the acting user hold the required role?

## Seed data design

`WorkflowTransitionSeedData` builds both graphs programmatically rather than
listing ~57 rows by hand. Ids are **derived deterministically** from each row's own
(template, from, to) triple via MD5-as-mixing-function (explicitly not for any
security purpose - no secret, no integrity claim), because EF Core's `HasData`
requires stable ids across migration generations, and hand-assigning 57 fixed
GUIDs would be unreviewable and error-prone.

The migration deletes WP-051's single provisionally-confirmed row (which had a
hand-assigned id) and inserts the full 57-row graph, with a clean symmetric
rollback in `Down`.

## Files created

- `src/APFlow.Domain/Common/Constants/RoleGatedTransitions.cs`
- `src/APFlow.Infrastructure/Persistence/WorkflowTransitionSeedData.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/20260724084158_SeedWorkflowTransitions.cs` (+ `.Designer.cs`, `.sql`, updated `AppDbContextModelSnapshot.cs`)
- `tests/APFlow.Application.Tests/Features/FakeWorkflowValidationService.cs`

## Files modified

- `src/APFlow.Application/Features/Invoices/InvoiceService.cs` - new
  `IWorkflowValidationService` dependency; WP-051's hardcoded single-transition
  gate replaced with validation + generalised role gating
- `src/APFlow.Infrastructure/Persistence/Configurations/WorkflowTransitionConfiguration.cs` - seeds both full graphs
- `tests/APFlow.Application.Tests/Features/Invoices/InvoiceServiceTests.cs` - WP-053's required tests; WP-051's tests updated for the new helper shape
- `tests/APFlow.Application.Tests/Features/Invoices/InvoiceProcessingServiceTests.cs`,
  `tests/APFlow.Infrastructure.Tests/Persistence/AuditLogRepositoryTests.cs`,
  `tests/APFlow.Infrastructure.Tests/Persistence/InvoiceProcessingDuplicateDetectionIntegrationTests.cs`,
  `tests/APFlow.Infrastructure.Tests/Persistence/WorkflowTemplateRepositoryTests.cs` - constructor/assertion updates for live enforcement

One obsolete test was **removed, not adjusted**:
`UpdateAsync_OtherTransitions_NotGatedByApprovalPolicy` asserted that
`CHECKED_READY_TO_APPROVE -> NEEDS_QUERY` is *not* role-gated. WP-053 task 3
explicitly makes it gated, so the test's premise is now inverted - it is replaced
by the new `[Theory]` covering all four gated transitions.

## Tests (required deliverables)

- **AWAITING_REVIEW -> APPROVED permitted for platform-default, rejected for GB
  Skips** - both halves, as separate tests. The GB Skips rejection is a
  `Workflow.TransitionNotAllowed` failure even for a `FINANCE_MANAGER`, proving the
  edge genuinely doesn't exist rather than merely being role-blocked.
- **AP_REVIEWER cannot execute any of the four role-gated transitions** - a
  `[Theory]` over all four pairs, each also asserting the invoice is left untouched.
- **FINANCE_MANAGER can execute all four** - the matching `[Theory]`.
- **WP-013 regression check passes** - see Discrepancy 1 for the one adjustment
  required, and why.
- Plus: transition-not-in-graph rejected; unchanged-status field edits not
  validated or gated; fail-closed on unconfigured policy (carried over from WP-051).

These use the **real** `WorkflowValidationService` and `ApprovalAuthorizationService`
(backed by fake repositories), not fakes of the services themselves, so they prove
the actual validation and policy logic rather than reactions to canned results.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings.
- `dotnet test` across all 5 test projects - **285/285 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Api.Tests` 27, `APFlow.Infrastructure.Tests` 72,
  `APFlow.Integrations.Tests` 45, `APFlow.Application.Tests` 130.
- Migration applied via `dotnet ef database update` against a **real running SQL
  Server** (per WP-052 Part A's convention), and the result independently verified
  by querying the database directly: 57 transitions total (25 platform-default, 32
  GB Skips), `AWAITING_REVIEW -> APPROVED` present only for platform-default, and
  zero edges referencing `DUPLICATE_SUSPECTED`.
