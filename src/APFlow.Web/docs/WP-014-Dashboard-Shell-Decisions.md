# WP-014 — Dashboard Shell & Navigation — Decisions

**Status:** OPEN — implemented with reasoned defaults; needs explicit sign-off.
**Role:** Senior React Engineer
**Depends on:** WP-050 (Tenant-Configurable Workflow Engine) — not yet implemented at time of writing.

---

## 1. APFlow.Web did not exist yet

Sprint 1 (WP-001–WP-013) was entirely backend. WP-014 is the first frontend work package, so this submission **creates `APFlow.Web` from scratch** (Vite + React 18 + TypeScript + Tailwind CSS + React Router 6), per `03_Solution_Structure.md` §1/§3. No project reference to any backend project was added — `APFlow.Web` communicates with `APFlow.Api` over HTTP only, per §2, and currently has no live HTTP calls at all (see §3 below).

## 2. Nav scoping — which links are "workflow-status-driven"

WP-014 task 3 states static top-level sections (Dashboard, Inbox, Invoice Queue, Query Queue, Approved, Suppliers, Administration), and that only "workflow-status-driven links, e.g. per-folder queues" are data-driven.

**Decision taken:** only **Invoice Queue** expands into one sub-link per non-terminal status in the acting tenant's `WorkflowTemplate` (ordered by `StatusReference.order`). All other top-level sections (Query Queue, Approved, Suppliers, Administration, Dashboard, Inbox) remain single static links.

**Reasoning:**
- The task's own example ("per-folder queues") points at the general invoice pipeline queue as the one place tenant-specific states need their own nav entry.
- The acceptance criterion — nav renders correctly for both tenants without a code change — requires GB Skips' two extra states (`CHECKED_READY_TO_APPROVE`, `NEEDS_REVIEW_FEBINA`) to visibly appear somewhere; Invoice Queue is the only section that can absorb them without inventing a routing scheme.
- Deciding which specific statuses belong under Query Queue vs. Approved is a business categorisation exercise WP-014 explicitly puts out of scope ("No business functionality"). Assigning that mapping now would require guessing at a scheme nobody has confirmed.

**Needs confirmation:** whether Query Queue/Approved should later also become data-driven sub-status views (e.g. Query Queue expanding into `NEEDS_QUERY`/`QUERY_RAISED`/`AWAITING_SUPPLIER_RESPONSE`), likely as part of WP-018.

## 3. Data source for WorkflowTemplate/StatusReference (WP-050 not yet built)

Since WP-050 has no API contract or endpoint yet, WP-014 implements:

- `src/types/workflowTemplate.ts` — `WorkflowTemplate`/`StatusReference` client-side types, a **proposed** shape, additive only.
- `src/api/workflowTemplateClient.ts` — a `WorkflowTemplateClient` interface (`getCurrentWorkflowTemplate(tenantId)`), with a `FixtureWorkflowTemplateClient` implementation backed by two local fixtures matching `06_Domain_Reference_Data.md` §2 exactly (platform-default 13 statuses; GB Skips adds `CHECKED_READY_TO_APPROVE`/`NEEDS_REVIEW_FEBINA` between `AWAITING_REVIEW` and `APPROVED`, 15 statuses total).
- All nav-consuming components depend only on the `WorkflowTemplateClient` interface, not the fixture implementation — swapping in a real HTTP-backed client once WP-050 ships requires changing exactly one line (`workflowTemplateClient` in `workflowTemplateClient.ts`), no consumer changes.

**Proposed HTTP contract for WP-050** (for the backend engineer's reference, not binding):

```
GET /api/tenants/current/workflow-template

200 OK
{
  "tenantId": "string",
  "templateName": "string",
  "statuses": [
    { "code": "string", "name": "string", "isTerminal": boolean, "order": number }
  ]
}
```

**Needs confirmation:** exact endpoint route, field names/casing, and how "current tenant" is resolved server-side (claim, header, subdomain) — all TBD pending WP-050 and WP-002.

## 4. Authentication stub (WP-002 not yet built)

`src/auth/AuthContext.tsx` provides a minimal `ActingUser { tenantId, tenantName, displayName }` context and `ProtectedRoute` guard so routing/nav have something real to consume. This is **not** real authentication — Microsoft Entra External ID sign-in is WP-002's responsibility. `LoginPage` offers a manual choice between the two tenant fixtures purely so the nav's tenant-dependent behaviour can be verified locally; it will be replaced wholesale by WP-002's real sign-in flow.

**Needs confirmation:** none required to proceed — this is explicitly temporary scaffolding, clearly isolated to `src/auth/`, and does not block WP-002 from replacing it entirely.

---

## AI Agent Rules acknowledgement

Per `02_Project_Standards.md` §7 and `01_Project_Context.md` §9, none of the above is presented as final. Items in §2 and §3 are implemented with reasoned defaults and flagged here for Chief Technical Architect sign-off, consistent with how prior sessions on this project (see WP-012's decision doc precedent) have handled cross-work-package dependencies that hadn't landed yet.

---

## Revision history

- **WP-014 → WP-014a:** Senior QA Engineer review (High priority) found zero unit test coverage and no test framework installed, against `02_Project_Standards.md` §5's unconditional testing requirement. Added Vitest + React Testing Library and unit tests for `navConfig.ts` (non-terminal filtering, ordering, non-mutation, null-template fallback), `FixtureWorkflowTemplateClient` (known-tenant resolution, unknown-tenant fallback, plus a regression guard confirming both fixtures match `06_Domain_Reference_Data.md` §2), and `ProtectedRoute` (renders children when authenticated; redirects to `/login` with the originating location preserved when not). No source logic changed — this revision is test coverage only. QA's other two findings (WP-002/WP-046 backend state, `InvoiceStatus` enum drift from reference data) were informational with no code action required for WP-014 and are unchanged here.
