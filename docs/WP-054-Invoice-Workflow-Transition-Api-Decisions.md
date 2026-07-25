# WP-054 — Invoice Workflow Transition API: Report

**Status:** Complete. One naming discrepancy between the work package's example
error code and what the underlying services actually produce is flagged below,
not silently resolved by inventing a new code.

## What this closes

The gap identified in the review of WP-053: `WorkflowValidationService` and
`InvoiceService.UpdateAsync`'s enforcement (transition graph + role gating) were
fully built and tested by WP-053, but unreachable over HTTP - `InvoicesController`
had only two `GET` endpoints (detail, download), and nothing in `APFlow.Api`
called `UpdateAsync` at all. WP-054 adds exactly two thin wrapper endpoints; no
new transition or role-gating rule is introduced anywhere in this delivery.

## Endpoints

### `GET /api/invoices/{id}/available-actions`

Returns only the transitions the calling user may actually execute right now -
not the full graph with permission flags (task 1's explicit instruction). Backed
by a new Application-layer service, `IInvoiceWorkflowActionsService`
(`InvoiceWorkflowActionsService`), which:

1. Loads the invoice (`Invoice.NotFound` if missing/not visible to the tenant -
   same code/message shape `InvoiceService` already uses).
2. Loads the acting tenant's active `WorkflowTemplate` via the existing
   `IWorkflowQueryService` (extended - see below).
3. Enumerates the edges leaving the invoice's current status.
4. For each edge gated by `RoleGatedTransitions.RequiresApprovalRole` (WP-053),
   calls the existing `IApprovalAuthorizationService.AuthorizeAsync` with the
   current user's roles and **omits** the edge entirely if unauthorized - it is
   not returned with a flag saying so, per task 1's wording and matching
   WP-018's own "don't render a button that will always be rejected" approach.

This reuses every enforcement building block WP-053 already wrote
(`IWorkflowQueryService`, `RoleGatedTransitions`, `IApprovalAuthorizationService`)
- nothing this endpoint lists can then be rejected by `PATCH .../status` for a
reason this endpoint could have already known about.

**One small, necessary Application-layer extension:** `IWorkflowTemplateRepository`
already loads a template's `Transitions` (WP-050), but `IWorkflowQueryService`/
`WorkflowTemplateDto` only ever exposed `Statuses`. Added `WorkflowTransitionDto`
and a `Transitions` list to `WorkflowTemplateDto` so `InvoiceWorkflowActionsService`
doesn't need a second, duplicate repository dependency of its own just to reach
data already loaded one layer down. This is not new business logic - just
surfacing already-loaded data through the existing query service, the same
service `GetById`'s eventual "review workflow" screens (WP-018/WP-019/WP-030, per
`IWorkflowQueryService`'s own doc comment) are already expected to use.

### `PATCH /api/invoices/{id}/status`

A thin HTTP wrapper over `InvoiceService.UpdateAsync` - no transition or
role-gating decision is made in the controller. Because `UpdateAsync`'s contract
is a full field replace (not a partial patch), and this endpoint's request body
only carries `targetStatusCode` (plus optional `notes`), the handler:

1. Fetches the invoice's current editable fields via the existing
   `IInvoiceService.GetByIdAsync`.
2. Resubmits them unchanged alongside the new status via `IInvoiceService.UpdateAsync`
   - the same call `InvoiceService`'s own WP-053 enforcement already gates.
3. On success, if `notes` is present and non-empty, records it via the existing
   `IInvoiceService.AddNoteAsync` (WP-009's freeform note mechanism, not a new
   per-transition note type - task 4's explicit "sufficient for MVP" framing). A
   failed note does not undo or hide the already-committed status change -
   logged as a warning only, the same "smaller problem" reasoning used
   throughout this codebase for every other non-critical side-effect failure.
4. Returns the WP-052 Part D `InvoiceDetailResponse` shape on success - the
   identical shape `GET /{id}` returns, built by the same private
   `BuildDetailResponseAsync` helper (extracted from `GetById` for this reason,
   so both endpoints share one source of truth rather than duplicating the
   audit-history-lookup-plus-fallback logic - Project Standards §1's DRY
   principle).

**Audit logging (task 5):** confirmed, not duplicated. `InvoiceService.UpdateAsync`
already stages an `InvoiceStatusChanged` audit entry itself whenever the status
actually changes (WP-013's original automatic logging, unaffected by WP-053).
This endpoint adds no audit call of its own for the status change; it only adds
one for the optional note, via the existing `AddNoteAsync`, which itself already
stages a `NoteAdded` entry (WP-052 Part C) - again, an existing mechanism, not a
new one.

## Error mapping: the one discrepancy

Task 2 names three example failure codes the `code` field should carry:
`Workflow.InvalidToStatus`, `Workflow.TransitionNotAllowed`, and
`Workflow.RoleNotPermitted`. The first two are real, exact codes
`WorkflowValidationService` produces. **`Workflow.RoleNotPermitted` does not
exist anywhere in this codebase.** The actual mechanism that rejects an
unauthorized role-gated transition is `IApprovalAuthorizationService`
(a separate, Approval-domain service - see WP-051/WP-053's own reasoning for why
role-gating is deliberately not folded into `WorkflowValidationService`), and it
produces `Approval.Unauthorized` (wrong role) or `Approval.PolicyNotConfigured`
(fail-closed, no policy configured for the domain at all).

Renaming either of those to a new `Workflow.RoleNotPermitted` code purely to
match this task's wording would mean editing `ApprovalAuthorizationService`
(WP-051 territory) to invent a code its own domain doesn't own, or translating
codes inside this controller - both of which manufacture a distinction the
system doesn't actually have, for no functional benefit. Instead: `ErrorProblem`
maps the two real `Approval.*` codes to `403 Forbidden`, and every `Workflow.*`
code (plus ordinary field-validation errors) to `400 Bad Request`, and the
response's `code` field always carries the real, unmodified `Error.Code`. The
frontend gets a specific, correct code to branch on either way - it is just
`Approval.Unauthorized`, not `Workflow.RoleNotPermitted`.

| Error code | HTTP status | Source |
|---|---|---|
| `Invoice.NotFound` | 404 | `IInvoiceService` / `IInvoiceWorkflowActionsService` |
| `Approval.Unauthorized` | 403 | `IApprovalAuthorizationService` |
| `Approval.PolicyNotConfigured` | 403 | `IApprovalAuthorizationService` (fail-closed) |
| `Workflow.InvalidDomainName` / `InvalidStatusCode` / `InvalidFromStatus` / `InvalidToStatus` / `TemplateNotFound` / `TransitionNotAllowed` | 400 | `IWorkflowValidationService` |
| Any `Invoice.*` field-validation error | 400 | `InvoiceFieldValidation` (unchanged from WP-009) |

## Files created

- `src/APFlow.Application/DTOs/AvailableActionDto.cs`
- `src/APFlow.Application/Interfaces/IInvoiceWorkflowActionsService.cs`
- `src/APFlow.Application/Features/Invoices/InvoiceWorkflowActionsService.cs`
- `src/APFlow.Api/Contracts/AvailableActionResponse.cs`
- `src/APFlow.Api/Contracts/UpdateInvoiceStatusRequest.cs`
- `tests/APFlow.Application.Tests/Features/Invoices/InvoiceWorkflowActionsServiceTests.cs`
- `docs/WP-054-Invoice-Workflow-Transition-Api-Decisions.md` (this file)

## Files modified

- `src/APFlow.Application/DTOs/WorkflowTemplateDto.cs` - new `WorkflowTransitionDto`
  record; `Transitions` added to `WorkflowTemplateDto`
- `src/APFlow.Application/Features/Workflow/WorkflowQueryService.cs` - populates
  the new `Transitions` field
- `src/APFlow.Application/DependencyInjection.cs` - registers
  `IInvoiceWorkflowActionsService`
- `src/APFlow.Api/Controllers/InvoicesController.cs` - two new endpoints; `GetById`
  refactored to share a new private `BuildDetailResponseAsync` helper; new
  private `ErrorProblem` helper for the `code`-bearing `ProblemDetails` mapping;
  new `IInvoiceWorkflowActionsService` constructor dependency
- `tests/APFlow.Application.Tests/Features/Workflow/WorkflowQueryServiceTests.cs` -
  one test added covering the new `Transitions` field
- `tests/APFlow.Api.Tests/Controllers/InvoicesControllerTests.cs` - `FakeInvoiceService`
  extended with real (configurable) `UpdateAsync`/`AddNoteAsync` behaviour
  (previously both threw `NotSupportedException`, unused by the pre-WP-054
  controller); new `FakeInvoiceWorkflowActionsService`; `CreateController` updated
  for the new constructor parameter; eight new tests added
- `README.md` - added WP-053 (previously missing from this table) and WP-054 rows

## Tests (required deliverables)

- **`available-actions` returns the correct filtered set for `AP_REVIEWER` vs
  `FINANCE_MANAGER` on the same invoice** - `InvoiceWorkflowActionsServiceTests`,
  using the real `WorkflowQueryService`/`ApprovalAuthorizationService` backed by
  fake repositories (not fakes of the services themselves), against a fixture
  built from the *real* `RoleGatedTransitions.All` pairs so gating is genuinely
  exercised, not assumed.
- **Status-change endpoint enforces the graph (rejects an invalid transition
  with the correct `code`)** - `UpdateStatus_TransitionNotAllowed_ReturnsBadRequestWithCode`.
- **Rejects a role-gated transition for an unauthorized user with 403** -
  `UpdateStatus_RoleNotPermitted_ReturnsForbiddenWithCode` (asserts the real
  `Approval.Unauthorized` code, per the discrepancy above).
- Plus: `available-actions` omits gated edges when no `ApprovalPolicy` is
  configured (fail-closed, not an error); `Invoice.NotFound` on both endpoints;
  `PATCH .../status` preserves every non-status field unchanged; adds/doesn't
  add a note depending on whether `notes` was supplied; `WorkflowQueryService`
  correctly surfaces `Transitions`.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings, whole solution.
- `dotnet test` across all 5 test projects - **301/301 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Application.Tests` 138 (+8 new),
  `APFlow.Api.Tests` 35 (+8 new), `APFlow.Infrastructure.Tests` 72,
  `APFlow.Integrations.Tests` 45.

## WP-018 contract compatibility

Implemented exactly the JSON shapes WP-054's own task text specifies:
`available-actions` returns `[{ "targetStatusCode", "targetStatusLabel" }]`;
`PATCH .../status` accepts `{ "targetStatusCode", "notes" }` and returns the
WP-052 Part D `InvoiceDetail` shape on success, or a `ProblemDetails` with a
`code` extension field on failure - all under ASP.NET Core's default camelCase
policy, no custom naming. No deviation from that spec was needed.

This backend delivery has no visibility into WP-018's actual frontend
code/fixture (frontend-owned, a different repository/work stream) - so this is
a confirmation against the contract as specified in this work package's own
text, not a line-by-line diff against WP-018's fixture client. If WP-018's
fixture used different field names or a different request/response shape than
what's written above, that is a mismatch to flag from the frontend side, not
something this delivery can independently verify.
