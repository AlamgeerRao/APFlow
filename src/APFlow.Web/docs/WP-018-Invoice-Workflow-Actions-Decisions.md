# WP-018 — Invoice Workflow Actions — Decisions

**Status:** OPEN — implemented with reasoned defaults; needs explicit sign-off.
**Role:** Senior React Engineer
**Dependencies stated:** WP-050 (Tenant-Configurable Workflow Engine), WP-051
(Role-Gated Approval Policy Extension) — both complete on the backend, but see
§1: this delivery found a materially larger gap than either WP-017 or any of
WP-014/015/016 hit.

---

## 1. The gap — larger than any prior frontend WP's, read both docs in full first

Read `docs/WP-050-Workflow-Engine-Decisions.md` and
`docs/WP-051-Approval-Policy-Decisions.md` end to end before writing any code,
per this WP's own "read all the documents" instruction, and checked every file
under `APFlow.Api`, `IWorkflowQueryService`, `IWorkflowValidationService`,
`IApprovalAuthorizationService`, `WorkflowTransitionConfiguration`, and
`ApprovalPolicy` directly. What actually exists today:

- **No HTTP endpoint of any kind exists to change an invoice's status.**
  `InvoicesController` has only `GET /api/invoices/{id}` and
  `.../download` — no `PUT`/`PATCH`, despite `InvoiceService.UpdateAsync`
  (Application layer) already supporting a status change.
- **No endpoint exists to list available actions or check permission.**
  `IWorkflowQueryService.GetActiveTemplateAsync` (statuses + display names)
  and `IApprovalAuthorizationService.AuthorizeAsync` (role check) both exist
  at the Application layer but neither is exposed through any controller.
- **General transition validation is explicitly NOT wired in anywhere.**
  `IWorkflowValidationService`'s own doc comment says so directly: "NOT YET
  CALLED from `InvoiceService.UpdateAsync`'s blocking path." WP-051 added
  only a single, narrow, hardcoded gate (`CHECKED_READY_TO_APPROVE ==
  previousStatus && APPROVED == request.Status`) — not a general "look up
  permitted transitions" mechanism.
- **Only ONE `WorkflowTransition` row is seeded in the entire system:**
  `CHECKED_READY_TO_APPROVE → APPROVED` (GB Skips only), gated by
  `FINANCE_MANAGER` — confirmed by reading
  `WorkflowTransitionConfiguration.cs` directly (one `HasData` call). The
  other three edges this WP's own task 2 explicitly asks for by name
  (`AWAITING_REVIEW → CHECKED_READY_TO_APPROVE`, both `NEEDS_REVIEW_FEBINA`
  escalation edges, and the three `NEEDS_REVIEW_FEBINA` resolution edges)
  are, per WP-050's own status line, "OPEN... needs Chief Technical
  Architect sign-off" and were explicitly NOT seeded by WP-051 either
  ("only this single transition was seeded... The other three edges... remain
  unconfirmed and unseeded").
- **The platform-default transition graph is not documented anywhere in this
  project's reference material at all** — not even as an unconfirmed
  proposal. WP-050 raised this itself and proposed nothing, "rather than
  inventing one."
- **No role information exists in the frontend's acting-user stand-in.**
  `ActingUser` (WP-014) had `tenantId`/`tenantName`/`displayName` only.

**Decision made:** follow the same precedent WP-014/015/016/017 already
established for an unconfirmed dependency, extended to this larger gap.
Rather than blocking entirely (which would mean delivering nothing this WP
asks for) or inventing new business rules from nothing, this delivery:

1. Built the general, tenant-and-role-driven rendering mechanism task 1 asks
   for (no hardcoded button set — see §2), against a client-side interface
   (`WorkflowActionClient`) a real HTTP client can replace in one line.
2. For GB Skips' three unconfirmed edges, used WP-050's OWN already-recorded
   proposed transition set verbatim (`docs/WP-050-Workflow-Engine-Decisions.md`
   §"Task 4 — the proposed transition set") as the fixture data — this is
   reproducing an existing, attributed backend-team proposal, not fabricating
   a new one, but it is **not yet backend-confirmed or backend-enforced**.
3. For the platform-default tenant, seeded **zero** actions for any status —
   matching WP-050's own reasoning exactly ("proposing nothing... rather than
   inventing one") rather than inventing a graph nobody has specified.
4. Extended `ActingUser` with a `roles` field (§5) so the acceptance
   criteria's two-role scenario is genuinely testable today.

**Proposed HTTP contract** (for the backend engineer's reference, not binding):

```
GET /api/invoices/{invoiceId}/available-actions

200 OK
[
  { "toStatusCode": "string", "label": "string" }
]
```
Already filtered server-side against the caller's actual roles (via
`ICurrentUserService`/`IApprovalAuthorizationService`) — task 3's own
"query the user's permission before rendering the button" reads most
naturally as the SERVER deciding what's even in this list, not the client
separately fetching a raw policy and re-implementing the role-comparison
logic `ApprovalAuthorizationService` already owns.

```
POST /api/invoices/{invoiceId}/status

Request: { "toStatusCode": "string" }

200 OK
{ "newStatusCode": "string" }

403 Forbidden — insufficient role for a role-gated transition (task 7)
400 Bad Request — toStatusCode is not a currently-available action for this invoice
404 Not Found
```

**Needs confirmation:** the exact route/shape above; whether the three
unconfirmed GB Skips edges (and by extension "Mark Checked & Ready to
Approve"/"Escalate to Febina" as real, working buttons) should be confirmed
and seeded now, deferred, or reconsidered entirely; whether/how the
platform-default transition graph gets documented and seeded, since until it
is, platform-default tenant users will see **no workflow actions at all**
under this delivery (see §2) — this is a real, user-visible gap, not a
theoretical one.

## 2. Task 1 — how "do not hardcode" was actually satisfied

`WorkflowActionButtons` has no tenant-specific or status-specific branching
of any kind — it renders whatever `WorkflowAction[]` it's given. All
tenant/status/role-specific behaviour lives entirely in
`workflowActions.fixture.ts`'s data and `FixtureWorkflowActionClient`'s
filtering logic, not in any component or page. This is what makes the
"no fixed Approve/Move to Query/Mark Query Raised/Resolve Query set" and
"GB Skips specifically surfaces two extra actions" requirements both true
simultaneously from the same, single code path — GB Skips has more rows in
the fixture map for its own tenant key, that's the only difference.

**Direct consequence, flagged prominently:** because the platform-default
fixture map is empty (§1), a platform-default tenant user sees "No actions
are available for this invoice's current status" for every invoice today.
This is the intellectually honest result of there being zero documented
platform-default transitions anywhere — not a bug in this delivery's
mechanism, but very likely not the experience a platform-default AP Reviewer
should actually have. Flagged for the Chief Technical Architect together
with WP-050's own still-open item.

## 3. Task 3 — role gating is scoped to exactly the one real backend rule

Of every action in the fixture data, only `CHECKED_READY_TO_APPROVE →
APPROVED` carries a `requiredRole` (`FINANCE_MANAGER`) — matching WP-051's
own narrow scope exactly (WP-051's own report: "not a general
'validate every transition'... this check only ever matters for GB Skips
tenants"). Every other action's `requiredRole` is `null` (available to any
authenticated tenant user), since nothing in either WP-050 or WP-051 gates
any of them. `WorkflowActionClient.getAvailableActions` filters BEFORE
returning — an unpermitted action is simply absent from the array, never
present-but-disabled, matching this task's literal "do not render or enable"
wording and the acceptance criteria's "does not see... Approve" phrasing.

`executeAction` re-checks permission independently before mutating (defense
in depth, mirroring `InvoiceService.UpdateAsync`'s own "checked before any
field is mutated" guarantee) — this is the path a test can exercise directly
to prove the rejection message and behaviour work (task 7), since the normal
UI path never lets an unpermitted click happen in the first place.

## 4. Task 4 — reused existing infrastructure, added nothing new

`InvoiceStatusBadge` (already used in `InvoiceHeaderSummary`, WP-016) already
resolves an invoice's status code to its tenant's `WorkflowTemplate` display
name via `useWorkflowTemplate` — genuinely tenant-driven, not a hardcoded
string, already satisfying this task's wording exactly. `WorkflowActionsPanel`
reuses this exact component rather than building a second, parallel status
display — avoiding both duplicated logic (`02_Project_Standards.md` §1 DRY)
and the risk of the two displays drifting out of sync.

## 5. Confirmation UI and refresh mechanism (tasks 5, 6)

**Confirmation:** a lightweight inline banner (`ConfirmActionDialog`,
`role="alertdialog"`) rather than a modal/overlay component — avoids adding a
new modal/focus-trap dependency for a single yes/no decision
(Simplicity First, `02_Project_Standards.md` §1). Needs confirmation: whether
a true modal (blocking the rest of the page) is required instead of an
inline, in-panel confirmation.

**Refresh after update:** `WorkflowActionsPanel` does not reload the
invoice itself — it calls the `onStatusChanged` prop, which
`InvoiceReviewPage` wires to `useInvoiceDetail`'s existing `retry()`. This
reloads the WHOLE invoice (not just this panel's own action list), so the
new status is reflected everywhere it's displayed on the page (this panel's
badge, `InvoiceHeaderSummary`'s badge) from one call, exactly as a real
`PUT` + refetch would behave. `useWorkflowActions` itself re-fetches its own
action list automatically whenever its `fromStatusCode` argument changes —
which happens naturally once the parent's refetched `invoice.status` flows
back down as a new prop.

To make this possible in a fixture-only environment, `invoices.fixture.ts`
(WP-015's file) gained one small, additive, exported function,
`updateFixtureInvoiceStatus` — it mutates the SAME shared fixture array every
other fixture client already reads from, so a status change is immediately
visible to the queue and detail views alike, without inventing a second,
parallel "current status" store. This is the one shared file this WP
touched beyond its own new files; the change is purely additive (one new
exported function) and does not alter `InvoiceClient`/`InvoiceDetailClient`'s
existing interfaces or behaviour in any way.

## 6. `ActingUser.roles` — the auth stand-in's own next gap

WP-014's provisional `ActingUser` never carried role information — nothing
in this codebase needed it before WP-018. Added `roles: string[]`, and
extended `LoginPage`'s demo-user picker with a GB Skips `FINANCE_MANAGER`
user (Patrick) and a GB Skips `AP_REVIEWER` user (Priya Shah), specifically
so this WP's own acceptance criteria (both roles, one tenant) can be verified
locally. Every existing test constructing an `ActingUser`/`AuthContextValue`
literal needed a `roles` array added — a small, mechanical, behavior-preserving
update (5 files), the same class of "blast radius" WP-050 itself documented
for its own enum retirement. Real role assignment remains WP-002's (Entra)
responsibility, unchanged from WP-014's own open item.

---

## AI Agent Rules acknowledgement

Per `02_Project_Standards.md` §7 and `01_Project_Context.md` §9, none of the
above is presented as final. §1 is the largest open item any frontend WP in
this project has raised to date — broader than an unconfirmed HTTP contract
over already-agreed business rules (WP-014/015/016's situation) or a
missing-but-simple CRUD endpoint (WP-017's situation): here, three of the
four transitions this WP was asked to surface are not backend-confirmed
business rules yet at all, and the platform-default tenant has none
whatsoever. Flagged here for Chief Technical Architect sign-off before this
delivery's `FixtureWorkflowActionClient` is treated as anything more than a
UI shell proving the rendering mechanism works.

---

## Revision history

**WP-018 → WP-018a:** two changes, made while fixing a packaging defect
(delivery zip had every source/test file nested under an incorrect
`docs/src/APFlow.Web/src/...` path instead of `APFlow.Web/src/...` at the
repo root — corrected):

1. **WP-054 (Invoice Workflow Transition API) is now in progress**, and its
   spec confirms the exact HTTP contract this WP proposed against in §1 —
   `GET /api/invoices/{id}/available-actions` returning
   `{ targetStatusCode, targetStatusLabel }[]`. This delivery's
   `WorkflowAction` type originally used `toStatusCode`/`label`. Renamed
   both fields (and every consumer: `workflowActionClient.ts`,
   `useWorkflowActions.ts`, `ConfirmActionDialog.tsx`,
   `WorkflowActionButtons.tsx`, `WorkflowActionsPanel.tsx`, and all
   affected tests) to match WP-054's confirmed field names exactly, so the
   "swap in the real client with no contract renegotiation" goal both WPs
   state actually holds. `workflowActions.fixture.ts`'s internal
   `RawWorkflowAction` type (private to that file) was left unchanged —
   only the public `WorkflowAction` shape the client returns needed to
   match.

   **Two differences remain, not yet reconciled, flagged for the real
   integration rather than guessed at now:**
   - WP-054's `PATCH /api/invoices/{id}/status` returns the full updated
     `InvoiceDetail`; this client's `executeAction` currently returns only
     `{ newStatusCode: string }`. The real HTTP client (when built) should
     return the full `InvoiceDetail` and let the caller use it directly,
     rather than the current pattern of returning a bare status code and
     relying on the caller's own `retry()` to refetch.
   - WP-054's request body accepts an optional `notes` field; this WP's
     `ConfirmActionDialog` doesn't collect one (no task in this WP's
     original scope asked for it). This client's `executeAction` has no
     `notes` parameter at all yet — adding one is additive and
     non-breaking whenever a future WP asks for a notes-on-transition UI
     input; not added speculatively here.

2. Removed a stale `// eslint-disable-next-line react-hooks/set-state-in-effect`
   comment in `useWorkflowActions.ts` — the referenced rule doesn't exist
   in this project's installed `eslint-plugin-react-hooks` version, which
   ESLint treats as a hard error. Confirmed the underlying code doesn't
   trigger any rule actually active in this project's config; no functional
   change.

Full verification re-run after both changes: `tsc -b` (0 errors),
`vitest run` (128/128 tests pass across the full project), `eslint .` (0
errors/warnings), `vite build` (0 errors).
