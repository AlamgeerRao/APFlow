# WP-051 — Role-Gated Approval Policy Extension: Report

**Status:** Complete for everything the codebase actually needs today. Task 5 is
documentation-only (WP-038 doesn't exist).
**Dependencies:** WP-046 (complete), WP-050 (complete).

## Premise discrepancy: `ApprovalPolicy` did not exist before this work package

The task list describes "extending the existing `ApprovalPolicy` mechanism...
currently scoped only to Payment Batch approval." Checked directly: no
`ApprovalPolicy` entity, no `PaymentBatch` concept, no approval-policy mechanism of
any kind existed anywhere in this codebase before WP-051 (confirmed by search,
zero matches) - the same class of premise mismatch as WP-046 (assumed
`UserRole`/SQL migrations), WP-048 (assumed the pipeline wasn't wired), and WP-050
(assumed a documented transition graph).

**Resolution:** built `ApprovalPolicy` as the mechanism's original creation, but
designed generically (domain-parameterized: `Domain` is a plain string, not an
enum scoped to Invoices) so it can plausibly serve a future Payment Batch domain
per the task's own framing - see `ApprovalDomains.PaymentBatchApproval`, defined
but not seeded or consumed by any code. No actual Payment Batch entities/features
were invented; only the domain constant exists, matching task 5's "document the
intended hook" instruction rather than building something task 5 explicitly says
isn't ready to be built.

## Task 2 — FINANCE_MANAGER confirmed workable as GB Skips' Full/Approver tier

Confirmed, not merely assumed: the approved 6-role catalogue
(`06_Domain_Reference_Data.md` §1) has exactly one role matching "Finance Manager /
Decision-Maker" with approval authority, already named as the interim mapping for
Full/Approver (Patrick, Febina) pending this exact confirmation. Nothing in any
confirmed reference document conflicts with this mapping, and no second role with
comparable approval semantics exists in the catalogue to compete for the same tier.
**No new role introduced.** `AP_REVIEWER` remains the Standard/Reviewer mapping,
unchanged and untouched by this work package.

## Task 4 scope: only ONE transition seeded, not all four of WP-050's proposed set

WP-050 deliberately left every `WorkflowTransition` row unseeded, pending Chief
Technical Architect confirmation of both the GB Skips proposed set and the
still-undocumented platform-default graph. WP-051's task 4 explicitly directs
gating the `CHECKED_READY_TO_APPROVE` -> `APPROVED` transition by role - read as
implicit confirmation of *this one edge specifically*, since the task treats it as
something that already exists and needs a check added, not something to design
from scratch. Accordingly, **only this single transition was seeded** for the GB
Skips template. The other three edges WP-050 proposed
(`AWAITING_REVIEW` -> `CHECKED_READY_TO_APPROVE`, the `NEEDS_REVIEW_FEBINA`
escalation and resolution edges) remain unconfirmed and unseeded - flagged here so
this narrower reading isn't mistaken for having resolved WP-050's open item in
full.

## The role gate is narrow, not a general transition-enforcement activation

`InvoiceService.UpdateAsync` now checks the acting user's roles against the seeded
`ApprovalPolicy`, but **only** when `previousStatus == CHECKED_READY_TO_APPROVE`
and `request.Status == APPROVED` specifically - not a general
"validate every transition against `IWorkflowValidationService`" activation, which
remains blocked on the platform-default transition graph being undocumented
anywhere (see `docs/WP-050-Workflow-Engine-Decisions.md`). This check only ever
matters for GB Skips tenants: the platform-default template has no
`CHECKED_READY_TO_APPROVE` status at all, so `previousStatus` can never equal it
for a platform-default invoice. Checked and rejected before any field is mutated,
so an unauthorized attempt leaves the invoice completely untouched (verified by
`UpdateAsync_CheckedReadyToApproveToApproved_ApReviewerRole_Rejected_InvoiceUnchanged`).

Fails closed if no policy is configured at all (`Approval.PolicyNotConfigured`) -
an unconfigured domain is not treated as "no restriction," matching this
codebase's established fail-closed philosophy (WP-003's tenant filter, WP-012's
idempotency check, WP-013's audit staging).

## Task 5 — the WP-038 hook, documented not built

WP-038 (remittance/payment batch creation) does not exist in this codebase. The
intended hook, so WP-038 doesn't have to rediscover this pattern:

1. At the point WP-038 creates a remittance/payment batch, call
   `IApprovalAuthorizationService.AuthorizeAsync(ApprovalDomains.PaymentBatchApproval, currentUserService.Roles, cancellationToken)`
   before persisting the batch - the identical pattern
   `InvoiceService.UpdateAsync` now uses for `ApprovalDomains.InvoiceApproval`.
2. Seed an `ApprovalPolicy` row for `ApprovalDomains.PaymentBatchApproval` (GB
   Skips' `RequiredRole`, presumably `FINANCE_MANAGER` again, but that is WP-038's
   call to confirm against whatever the Requirements Addendum says about payment
   batch sign-off specifically - not assumed here).
3. No new repository/service work needed - `IApprovalPolicyRepository`/
   `IApprovalAuthorizationService` already support any domain string; only a new
   seeded policy row and one call site are required.

## Files created

- `src/APFlow.Domain/Entities/ApprovalPolicy.cs`
- `src/APFlow.Domain/Common/Constants/ApprovalDomains.cs`
- `src/APFlow.Application/Interfaces/IApprovalPolicyRepository.cs`, `IApprovalAuthorizationService.cs`
- `src/APFlow.Application/Features/Approval/ApprovalAuthorizationService.cs`
- `src/APFlow.Infrastructure/Persistence/ApprovalPolicyRepository.cs`, `ApprovalPolicySeedData.cs`
- `src/APFlow.Infrastructure/Persistence/Configurations/ApprovalPolicyConfiguration.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/20260723090735_AddApprovalPolicy.cs` (+ `.Designer.cs`, `.sql`, updated `AppDbContextModelSnapshot.cs`)
- 3 new test files (`ApprovalAuthorizationServiceTests`, `ApprovalPolicyRepositoryTests`, plus `FakeApprovalPolicyRepository`/`FakeCurrentUserService`/`FakeApprovalAuthorizationService` shared fakes)

## Files modified

- `src/APFlow.Infrastructure/Persistence/AppDbContext.cs` - new `ApprovalPolicies` DbSet (reuses WP-050's existing `IOptionallyTenantScoped` filter mechanism - no new filter code needed)
- `src/APFlow.Infrastructure/Persistence/Configurations/WorkflowTransitionConfiguration.cs` - seeds the one confirmed transition
- `src/APFlow.Application/Features/Invoices/InvoiceService.cs` - new `ICurrentUserService`/`IApprovalAuthorizationService` dependencies, the narrow role-gate check in `UpdateAsync`
- `Application/DependencyInjection.cs`, `Infrastructure/DependencyInjection.cs` - new registrations
- 5 test files updated for `InvoiceService`'s two new constructor parameters (behavior otherwise unchanged for all pre-existing tests)

## Migration

`20260723090735_AddApprovalPolicy` (on top of WP-050's `20260723083648_AddWorkflowEngine`):
- Creates `ApprovalPolicies` (unique index on `Domain`+`TenantId`, filtered to
  non-null `TenantId` so multiple platform-default rows across domains don't
  collide with the uniqueness constraint in an unintended way)
- Seeds 1 policy row (GB Skips, `InvoiceApproval`, `FINANCE_MANAGER`, dual-control
  disabled) - same placeholder tenant id as WP-050's `WorkflowSeedData` for
  consistency, still not GB Skips' real Entra tenant id
- Seeds 1 `WorkflowTransition` row (`CHECKED_READY_TO_APPROVE` -> `APPROVED`, GB
  Skips template) - see the scope note above
- No column alterations, no data-loss warning from `dotnet ef migrations add`
  (purely additive)

Verified via `dotnet ef migrations script` (real EF Core output). Could not run
`dotnet ef database update` against a live SQL Server in this sandbox (no LocalDB
support on Linux, same constraint as WP-048/WP-050).

## Tests (required deliverable)

`InvoiceServiceTests` (the two explicitly required, plus supporting coverage):
- `UpdateAsync_CheckedReadyToApproveToApproved_ApReviewerRole_Rejected_InvoiceUnchanged` -
  required scenario; also asserts the invoice is left completely untouched
- `UpdateAsync_CheckedReadyToApproveToApproved_FinanceManagerRole_Succeeds` -
  required scenario
- `UpdateAsync_CheckedReadyToApproveToApproved_NoPolicyConfigured_FailsClosed`
- `UpdateAsync_OtherTransitions_NotGatedByApprovalPolicy` - proves the gate's
  narrow scope

Both required tests use the REAL `ApprovalAuthorizationService` (backed by a fake
repository), not a mocked `Result`, so they prove the actual policy-checking logic
works, not just that `InvoiceService` reacts correctly to a canned answer.

`ApprovalAuthorizationServiceTests` (7 tests, direct unit coverage): required-role
match/mismatch, multiple roles, no policy configured (fail closed), no roles,
tenant-specific-preferred-over-platform-default, blank domain.

`ApprovalPolicyRepositoryTests` (Infrastructure.Tests, real `AppDbContext`, **real
seeded data**): GB Skips' placeholder tenant sees the seeded `FINANCE_MANAGER`
policy; a different tenant sees no policy at all (not GB Skips' by mistake); the
`PaymentBatchApproval` domain has no policy seeded (task 5 confirmed as
documentation-only).

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings.
- `dotnet test` across all 5 test projects - **261/261 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Api.Tests` 19, `APFlow.Infrastructure.Tests` 72,
  `APFlow.Integrations.Tests` 45, `APFlow.Application.Tests` 114.
