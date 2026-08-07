# GB Skips — Dev-to-Real-Tenant Migration Notes

**Status:** Planning notes for Sprint 2 — referenced by `docs/Sprint2-Plan.md` (WP-087, WP-088, WP-089)
**Purpose:** Capture the two items discussed regarding moving GB Skips from the current dev-tenant stand-ins to their real production identity/mailbox, plus the group-based role assignment improvement raised alongside them.

**Important distinction — read first:** this is **not** the "Second-Tenant Readiness" gate (`docs/Second_Tenant_Readiness_Gate.md`, if present). That gate governs onboarding a genuinely new, additional customer beyond GB Skips. This document is about GB Skips — AP Flow's *existing* single tenant — moving from its current dev stand-ins (the `rameezjav` CIAM tenant, the Business Basic mail tenant) to GB Skips' own real identity and mailbox. GB Skips remains tenant #1 throughout; nothing here touches the multi-tenant architecture question.

---

## Item 1 — Single sign-on via federation with GB Skips' own Microsoft 365

**What it is:** GB Skips' own Entra tenant configured as an external identity provider within AP Flow's CIAM tenant. GB Skips employees sign in with their own corporate Microsoft credentials — no separate AP Flow password.

**Key architectural point, confirmed:** federation does not change how AP Flow's tenant-scoping works. The CIAM tenant still issues the token, still carrying **AP Flow's own CIAM tenant id** as `tid` — not GB Skips' tenant id. `TenantEntity`, query filters, and `WorkflowTemplate` lookups are all unaffected.

**What it actually requires:**
- A small app registration in GB Skips' own Entra tenant — needs their IT/admin involvement, not a same-session task.
- One-time federation trust configuration on AP Flow's CIAM side.
- **Role assignment remains a manual per-user step** unless combined with Item 3 below — a federated user's first sign-in creates a shadow account in the CIAM tenant, and someone still has to assign the correct role.

**Timing:** Sprint 2 — requires GB Skips' own IT involvement, not a quick turnaround.

**Corresponds to:** `WP-087` in `docs/Sprint2-Plan.md`.

---

## Item 2 — A dedicated Graph app registration for GB Skips' real mailbox

**What it is:** when GB Skips' real invoice-receiving mailbox replaces the current dev stand-in (`invoices@acoounts01.onmicrosoft.com`, the Business Basic tenant), Graph read access to that real mailbox needs its own app registration — separate from `apflow-graph-dev`, the same way that was already kept separate from the SPA/API registrations (three distinct security boundaries).

**Why this is expected, not new scope:** this is exactly what the original Graph multi-tenancy decision already anticipated — per-tenant Graph app registration, per-tenant admin consent, Key Vault secrets under `graph-secret-{tenantId}`. The dev setup was always a stand-in for this, not the final shape.

**What it actually requires:**
- Confirmation of which real M365 tenant hosts GB Skips' actual invoice mailbox (their own tenant, or a dedicated one — needs their input).
- A new app registration in that tenant, `Mail.ReadWrite` (and, once WP-031/WP-040 need it, `Mail.Send`) application permission, admin consent from GB Skips' own admin.
- New Key Vault secret (`graph-secret-{GB Skips' real tenant id}`), following the established naming convention.
- Update to `Workers:TenantId`/Graph config to point at the real values.

**Timing:** Sprint 2, alongside Item 1 — both are part of the same "go live with GB Skips' real identity" event.

**Corresponds to:** `WP-088` in `docs/Sprint2-Plan.md`.

---

## Item 3 — Group-based role assignment, not per-user

**What it is:** create an Entra security group per app role (e.g. `AppFlow-FinanceManagers`, `AppFlow-APReviewers`) and assign the app role to the group rather than to individual users. New users are onboarded by adding them to the correct group; no per-user app-role assignment needed going forward.

**Why this matters:** directly supports Item 1 at scale — GB Skips onboarding a new starter should mean adding them to the right group (ideally eventually driven by GB Skips' own group membership), not someone manually setting an app role in AP Flow's Entra tenant for every individual hire. This should be built **before** the Administration Portal (`WP-043`), not retrofitted onto per-user assignment after the fact.

**Status:** raised during WP-064's execution (2026-08-01) as a good instinct worth doing properly — Sprint 1's four demo/test accounts were assigned roles directly, which was fine for four hand-picked accounts but doesn't scale.

**Timing:** Sprint 2 — do this ahead of `WP-043` (Administration Portal), so that UI is built against the group model from the start.

**Corresponds to:** `WP-089` in `docs/Sprint2-Plan.md`.
