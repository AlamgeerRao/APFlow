# WP-050 — Tenant-Configurable Workflow Engine: Decisions & Report

**Status:** OPEN — schema, seed data (states only), and a fully-tested validation
mechanism are complete. Transition enforcement is explicitly NOT enabled - see
below. Needs Chief Technical Architect sign-off on multiple items before the next
step (enabling enforcement) can happen.
**Owner:** Chief Technical Architect.
**Raised:** WP-050 delivery.

## The central finding that shaped this delivery

Task 4's own wording ("do not silently finalise a transition set not explicitly
confirmed") is the reason this document exists. While implementing task 5, a
second, related problem surfaced: **the platform-default transition graph is not
documented anywhere in this project's reference material either** -
`06_Domain_Reference_Data.md` §2 gives only the state list and each state's
terminal flag, never which transitions between them are valid. Today,
`UpdateInvoiceRequest`'s own doc comment already says as much: "No transition
validation is performed... any status can be set to any other status here."

This matters because it changes what "finish task 5" can safely mean. Wiring a
strict, data-driven transition check into `InvoiceService.UpdateAsync` with zero
seeded `WorkflowTransition` rows for *either* template would reject every status
change in the entire application - not an enforcement of anything confirmed, a
regression. Confirmed directly: `InvoiceServiceTests.UpdateAsync_StatusChanged_...`
(WP-013) exercises a real Received -> Extracted change expecting success; that
test still passes today specifically because enforcement is not yet wired in.

**Decision made:** build the complete schema and a fully-tested, ready-to-enable
validation mechanism; seed only the confirmed-safe data (state lists for both
templates); do not seed any `WorkflowTransition` rows for either template; do not
call the validation service from any blocking path yet. This satisfies task 4's
explicit instruction and extends the same caution to the platform-default side of
the same problem, which turned out to have an identical gap.

## Task 4 — the proposed transition set, restated for explicit confirmation

This is exactly the set given in the work package, unchanged, reproduced here so
sign-off can happen against a single, unambiguous record:

1. `AWAITING_REVIEW` → `CHECKED_READY_TO_APPROVE` (Standard/Reviewer action)
2. `CHECKED_READY_TO_APPROVE` → `APPROVED` (Full/Approver action only - WP-051)
3. `AWAITING_REVIEW` or `CHECKED_READY_TO_APPROVE` → `NEEDS_REVIEW_FEBINA` (escalation, any reviewer)
4. `NEEDS_REVIEW_FEBINA` → `CHECKED_READY_TO_APPROVE`, `NEEDS_QUERY`, or `REJECTED` (resolution outcomes)

**Not yet seeded or enforced.** Once confirmed, adding these as
`WorkflowTransition` rows against `WorkflowSeedData.GbSkipsTemplateId` is a small,
mechanical follow-up (a new migration adding `InsertData` calls) - the schema and
`WorkflowValidationService` are already built and tested against exactly this
shape of data (see `WorkflowValidationServiceTests`, which uses equivalent
test-constructed transitions to prove the mechanism itself works).

## Also needs confirmation: the platform-default transition graph

Not part of the original task list, but blocking the same "wire up enforcement"
step task 5 asks for. Since no document defines it, proposing nothing here rather
than inventing one - this needs the Chief Technical Architect (or Product Owner)
to specify the platform-default Invoice workflow's actual allowed transitions
(all 14 states, including `EXTRACTED` - see below) before `InvoiceService.UpdateAsync`
can safely enforce transitions for platform-default tenants either.

## Discrepancy flagged: `EXTRACTED` is not in `06_Domain_Reference_Data.md` §2

`06_Domain_Reference_Data.md` §2's baseline catalogue lists 13 states; the
already-shipped, tested `InvoiceStatus` enum (WP-009/WP-012, now retired - see
below) had a 14th, `EXTRACTED`, produced by the WP-012/WP-049 ingestion pipeline
once Document Intelligence analysis completes. This predates
`06_Domain_Reference_Data.md` and was never cross-checked against it until this
WP. Kept in the seeded platform-default catalogue (both templates actually - see
below) because removing it would break the already-shipped pipeline, which relies
on it as a real, persisted status. Confirm whether `EXTRACTED` should be formally
added to `06_Domain_Reference_Data.md` §2, or whether the pipeline should be
changed to land on a different, already-documented status instead (e.g.
`PROCESSING`) - out of scope for WP-050 to decide unilaterally.

## Task 6 — `InvoiceStatus` enum retired, not projected

Of the two options task 6 offered, the enum was **retired**, not kept as a "thin
projection": `Invoice.Status` is now a plain `string` (matching
`StatusReference.Code`). A "thin projection while keeping `Invoice.Status` typed
as the enum" was considered and rejected - it cannot structurally represent a
GB Skips invoice sitting in `CHECKED_READY_TO_APPROVE`, since no such enum member
could ever exist without hardcoding a tenant-specific value into a shared type,
which `06_Domain_Reference_Data.md` §3 explicitly prohibits. `InvoiceStatusCodes`
(a constants class, not an enum) replaces it for referencing the known
platform-default codes in code without raw string literals - it is documentation
convenience, not the source of truth (the seeded `StatusReference` rows are).

**Blast radius:** every file referencing the old enum was updated (17 files -
`Invoice.cs`, DTOs, `InvoiceService.cs`, `InvoiceProcessingService.cs`,
`InvoiceConfiguration.cs`, and 11 test files). All pre-existing tests across all
5 test projects continue to pass unchanged in behavior - this was a
representation change, not a behavior change, for every existing code path.

## GB Skips' tenant id is a placeholder

`WorkflowSeedData.GbSkipsPlaceholderTenantId` is NOT GB Skips' real Entra tenant
id - this codebase has never had a verified one (same "no real environment
values yet" situation as `docs/WP-002-Entra-Verification-Checklist.md` and
`docs/WP-004-Graph-Verification-Checklist.md`). The GB Skips `WorkflowTemplate`
row exists in the database but is unreachable by any real user until this value
is corrected to GB Skips' actual `tid` claim value.

## Design decision: `IOptionallyTenantScoped`, not `TenantEntity`

`WorkflowTemplate`/`StatusReference`/`WorkflowTransition` need a genuinely
different tenant-scoping shape than every other entity in this codebase: a
platform-default row (`TenantId == null`) must be visible to *every* tenant, not
just a caller with no resolvable tenant (which is what a null match would mean
under `TenantEntity`'s existing fail-closed filter). A new marker interface
(`IOptionallyTenantScoped`) and a second `AppDbContext` query filter method
(`ApplyOptionalTenantAndSoftDeleteFilter`) were added rather than changing
`TenantEntity`'s own filter - the existing fail-closed behavior for every other
entity is exactly correct and was not touched.

## Files created

- `src/APFlow.Domain/Entities/WorkflowTemplate.cs`, `StatusReference.cs`, `WorkflowTransition.cs`
- `src/APFlow.Domain/Common/IOptionallyTenantScoped.cs`
- `src/APFlow.Domain/Common/Constants/InvoiceStatusCodes.cs`, `WorkflowDomains.cs`
- `src/APFlow.Application/Interfaces/IWorkflowTemplateRepository.cs`, `IWorkflowQueryService.cs`, `IWorkflowValidationService.cs`
- `src/APFlow.Application/DTOs/WorkflowTemplateDto.cs`
- `src/APFlow.Application/Features/Workflow/WorkflowQueryService.cs`, `WorkflowValidationService.cs`
- `src/APFlow.Infrastructure/Persistence/WorkflowTemplateRepository.cs`, `WorkflowSeedData.cs`
- `src/APFlow.Infrastructure/Persistence/Configurations/WorkflowTemplateConfiguration.cs`, `StatusReferenceConfiguration.cs`, `WorkflowTransitionConfiguration.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/20260723083648_AddWorkflowEngine.cs` (+ `.Designer.cs`, `.sql`, updated `AppDbContextModelSnapshot.cs`)
- 5 new test files (see below)

## Files modified

- `src/APFlow.Infrastructure/Persistence/AppDbContext.cs` - 3 new `DbSet`s, new optional-tenant filter
- `Invoice.cs`, `InvoiceConfiguration.cs`, `InvoiceDto.cs`, `InvoiceListItemDto.cs`, `InvoiceQueryParameters.cs`, `InvoiceService.cs`, `InvoiceProcessingService.cs` - enum retirement
- `Application/DependencyInjection.cs`, `Infrastructure/DependencyInjection.cs` - new service/repository registrations
- 6 test files updated for the enum retirement (behavior unchanged)
- `docs/WP-002-Entra-Verification-Checklist.md` - not touched by this WP (unrelated)

## Migration

`20260723083648_AddWorkflowEngine` (on top of WP-048's `20260722184739_InitialCreate`):
- Widens `Invoices.Status` from `nvarchar(32)` to `nvarchar(64)` (safe - a widening,
  not a narrowing; `dotnet ef migrations add`'s generic "may result in data loss"
  warning is EF Core's standard caution for any `AlterColumn`, not specific to
  this being unsafe)
- Creates `WorkflowTemplates`, `StatusReferences`, `WorkflowTransitions`
- Seeds 2 templates + 30 status rows (14 platform-default including `EXTRACTED`,
  16 GB Skips including the same plus its 2 tenant-specific additions) - 32
  `InsertData` calls total, confirmed by direct count against the generated
  migration file
- Zero `WorkflowTransition` rows seeded, deliberately

Verified via `dotnet ef migrations script` (real EF Core output, not hand-written)
- `.sql` copy included alongside the migration for review without needing the
  tooling installed. Could not run `dotnet ef database update` against a real SQL
  Server in this sandbox (no LocalDB support on Linux, same constraint as WP-048).

## Tests (required deliverable)

`WorkflowValidationServiceTests` (Application.Tests, hand-written fakes, explicit
test-constructed data - deliberately not the unconfirmed real proposed set):
- Platform-default tenant: `CHECKED_READY_TO_APPROVE`/`NEEDS_REVIEW_FEBINA` are not
  even valid statuses for its template (`Workflow.InvalidToStatus`)
- GB Skips tenant: these ARE valid, recognized statuses for its template (proving
  tenant-specific status resolution works) - but the actual transition is still
  rejected with zero seeded transitions (`Workflow.TransitionNotAllowed`, not
  `InvalidToStatus` - proving the mechanism distinguishes "not a valid status" from
  "valid status, no permitted edge")
- With an explicit, test-configured transition, the same GB Skips pair IS allowed
  - proving the mechanism can allow, not just always reject
- Invalid transitions between two individually-valid statuses are rejected for
  BOTH tenants
- Same-status "transitions" always succeed without any repository call
- Unknown status codes and unknown domains both return specific failures

`WorkflowTemplateRepositoryTests` (Infrastructure.Tests, real `AppDbContext`,
InMemory provider, **the real seeded data**, not test-constructed fakes):
- Platform-default tenant sees the 14-status baseline, no GB Skips extras, zero
  transitions
- GB Skips' placeholder tenant sees its 16-status template, both new statuses
  present and correctly positioned (`SortOrder`) between `AWAITING_REVIEW` and
  `APPROVED`, zero transitions
- A caller with no resolvable tenant still sees the platform default (unlike every
  other entity in this codebase, which fails closed to zero rows in that case -
  this is the one place a null-tenant match is intentional)

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings.
- `dotnet test` across all 5 test projects - **247/247 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Api.Tests` 19, `APFlow.Infrastructure.Tests` 69,
  `APFlow.Integrations.Tests` 45, `APFlow.Application.Tests` 103.
