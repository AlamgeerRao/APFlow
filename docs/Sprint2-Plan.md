# AP Flow — Sprint 2 Plan

**Status:** Verification pass complete against real source code — every work package checked directly against the codebase, not just described from what it should logically do. All items in §3/§4 are dispatch-ready except where explicitly noted otherwise. WP-026, WP-027, WP-030, WP-031, WP-043, WP-092, WP-093 delivered, committed. WP-089 delivered live (Entra/Azure AD config, docs pushed). WP-043 user/role-management scope permanently retired — see its entry.
**Prepared by:** Chief Technical Architect
**Numbering:** `WP-026`–`WP-045` was reserved for Sprint 2 from the start (this is why Sprint 1's post-QA fixes began at `WP-046`, skipping straight past it). Rescoped original work packages keep their original numbers below. Genuinely new work (not in the original 20) continues from **WP-087**, the first number after Sprint 1's actual last use (`WP-086`).

---

## 0. Why this isn't just the original plan unchanged

The original Sprint 2 work packages were scoped before Sprint 1 existed in its final form. Sprint 1 ran far longer than planned specifically because the workflow engine, role-based approval gating, and the real transition graph all got built out properly along the way — which means **two original Sprint 2 work packages are now already done**, several others needed rescoping once checked against the real codebase, and Sage 50 (§8) is now a fully-scoped, deliberately-chosen approach rather than an open research question.

**Verification pass status: complete.** Every work package below has been checked directly against the real source code. Seven real findings changed scope from the original assumption — WP-026 (existing entity), WP-027 (naming collision with an existing page), WP-031/032 (query reason already captured by an existing mechanism), WP-038 (confirmed clean/greenfield), WP-089 (not a coding task at all), the background-worker pattern (WP-030/036/044, one dedicated class per concern), and **WP-043 (user/role management permanently retired as scope, once WP-089 revealed what it would actually require — see its own entry).**

---

## Pending manual action — not urgent, do when convenient

**`Mail.Send` Graph permission grant is still outstanding.** WP-031 built and unit-tested the outbound-email send capability in full, but genuinely could not complete the permission grant itself — this requires a human's one-time interactive sign-in to the mailbox's own M365 tenant (`1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`), confirmed blocked with real evidence (a device-code login that needs a browser, and a `403 Authorization_RequestDenied` when the app tried using its own credential to grant itself the permission — an app genuinely cannot do this to itself).

**To do, whenever convenient — not blocking any current work:**
```bash
az login --tenant 1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf

GRAPH_SP_ID=$(az ad sp show --id 00000003-0000-0000-c000-000000000000 --query id -o tsv)
MAIL_SEND_ROLE_ID=$(az ad sp show --id 00000003-0000-0000-c000-000000000000 \
  --query "appRoles[?value=='Mail.Send'].id" -o tsv)

az ad app permission add --id 40d63c64-ff18-4028-ba92-01ca93c1c432 \
  --api 00000003-0000-0000-c000-000000000000 \
  --api-permissions $MAIL_SEND_ROLE_ID=Role

az ad app permission grant --id 40d63c64-ff18-4028-ba92-01ca93c1c432 \
  --api 00000003-0000-0000-c000-000000000000

az ad app permission admin-consent --id 40d63c64-ff18-4028-ba92-01ca93c1c432

# Confirm:
az ad app permission list --id 40d63c64-ff18-4028-ba92-01ca93c1c432
```

**Consequence of leaving this undone:** `GraphEmailSendService.SendAsync` is fully built and wired in, but will fail live with a Graph `403` the moment it's actually exercised (WP-031's own transitions to `Query Raised`, and WP-040's remittance emails once built). Not urgent — nothing currently deployed exercises this path yet — but needed before WP-031/WP-040 can be genuinely live-verified, not just code-complete.

---

## 1. Sprint 2 Deliverables

By the end of Sprint 2, GB Skips will be able to:

- Manage suppliers directly within AP Flow, with credit limit changes restricted to Full Approvers.
- Raise a query with a supplier and have it actually sent by email — not just tracked internally (the internal query workflow itself already works, from Sprint 1).
- Upload and manage supplier statements.
- Monitor supplier credit limits and overdue invoices, with reminders.
- Generate remittances and email them to suppliers.
- Export approved remittances for import into Sage 50, with AP Flow tracking and flagging whenever that import is still pending — see §8.
- View system health from within AP Flow. **User/role management is not, and will not be, an AP Flow feature — permanently out of scope, see WP-043.**
- Sign in using their own Microsoft 365 credentials, once federation with GB Skips' real tenant is set up.

---

## 2. What's already done, and doesn't need a Sprint 2 work package at all

The **invoice approval engine, the workflow transition graph, role-gated approval actions, the query-status workflow (Needs Query → Query Raised → Awaiting Supplier Response), and the Invoice Workflow API** are all fully built, tested, and live-verified from Sprint 1. What Sprint 2 adds on top of the query workflow specifically is the part never in scope for Sprint 1: actually **emailing** the supplier.

**Also already exists, and matters for WP-026 specifically:** a `Supplier` entity and table are already live — `InvoiceProcessingService.ResolveSupplierAsync` (WP-012) has been auto-creating `Supplier` rows since Sprint 1's ingestion pipeline first shipped. WP-026 extends this, it does not build it from scratch.

**Also already exists, and matters for WP-027 specifically:** a page literally named "Suppliers" (`SuppliersPage.tsx`) already exists and is live — Sprint 1's read-only "browse invoices by supplier" view (WP-019/065). WP-027 extends this page (confirmed, Option A) rather than building a separate screen.

**Also confirmed, and matters for WP-089/WP-043 specifically:** roles are derived entirely from the Entra JWT `roles` claim — no local `UserRole` table exists anywhere in the codebase (confirmed at WP-046). WP-089 (done) proved group-based assignment needs zero application code, and does so entirely in the CIAM tenant — a piece of **shared platform infrastructure**, not tenant-scoped application data the way everything else in AP Flow is. That distinction is why WP-043's user/role piece is now permanently retired, not deferred — see its entry.

**Also confirmed, and matters for WP-031/032 specifically:** `UpdateInvoiceStatusRequest.Notes` is already mandatory on every status transition (`InvoicesController.UpdateStatus`, live since WP-084) — `Invoice.NoteRequiredForTransition` rejects any transition without one, and the note is created atomically with the status change. This means the "query reason" WP-032 was originally going to need a dedicated field for **already exists**.

**Also confirmed, and matters for WP-031/040 specifically:** no outbound email-sending capability exists anywhere — `EmailService`/`IEmailService` only ever did mailbox connection verification (WP-004). Genuinely new work.

**Also confirmed, and matters for WP-038 specifically:** no `Remittance` entity exists yet anywhere in `APFlow.Domain/Entities`. Genuinely greenfield.

**Also confirmed, and matters for WP-033/034, WP-036/037 specifically:** no `Statement` or `Notification` entity exists anywhere either. Both are genuinely greenfield — checked the complete `APFlow.Domain/Entities` listing directly, not assumed.

**Also confirmed, and matters for WP-030/036/044 specifically:** exactly one background worker exists today, `EmailIngestionWorker` (WP-067) — a single `BackgroundService`, per-cycle DI scope (never resolving a scoped service like `AppDbContext` once at startup), all exceptions caught inside the cycle so a bad run can't take the whole API host down, timer-based `Task.Delay` loop. This is the confirmed, proven template every new scheduled job should follow — as its own dedicated class, not merged into the existing worker.

**Also confirmed, and matters for WP-036 specifically:** `IngestionIssue` (WP-076) is a good, directly reusable template for notification dedup — same-key deduplication with an `OccurrenceCount`/`LastSeenUtc` pair, so a repeated condition (e.g. "credit limit still exceeded") produces one updated record per poll cycle, not a new row every time.

**Also confirmed, and matters for WP-043's system-health section specifically:** `/health/live`/`/health/ready` (WP-004/WP-024) already exist and are already the authoritative health signal — no new backend health logic needed, just a UI surfacing what's already there.

---

## 3. Sprint 2 work packages (original reserved numbering, WP-026–045)

### WP-026 — Supplier Management (Backend Engineer) — ✅ DONE, PUSHED
**Objective:** Extend supplier management with the fields needed for a real management screen — credit limit, payment terms, accounting reference, status.

**Correction — critical, read before writing any code or migration:** a `Supplier` entity and table **already exist and are live** — `InvoiceProcessingService.ResolveSupplierAsync` (Sprint 1, WP-012) has been auto-creating `Supplier` rows since ingestion first shipped, whenever a new supplier name is seen on an extracted invoice (case-insensitive, trimmed, exact-match resolution, create-if-absent). This WP is **not** a greenfield build.

**Tasks:**
1. Confirm the existing `Supplier` entity's current fields directly in the codebase before writing anything — do not assume a blank slate.
2. Add the new fields this WP needs (`Code`, `Email`, `Phone`, `CreditLimit`, `PaymentTerms`, `AccountingReference`, `Status`) to the **existing** entity/table via a migration that alters it — never a new table, never a parallel entity.
3. The existing auto-create-on-ingestion behavior in `ResolveSupplierAsync` must keep working completely unchanged.
4. Build the manual CRUD API/service for the new management fields.
5. **Credit Limit permission split:** any authenticated user with base access can update every field **except** `CreditLimit`. A request that changes `CreditLimit` must be rejected with `403` unless the caller holds the `FINANCE_MANAGER` role — explicit rejection, not a silent no-op. Use a direct role check, **not** the `WorkflowTransition`/`ApprovalPolicy` machinery.
6. **Known, accepted gap — do not attempt to fix as part of this WP:** OCR variance can create duplicate `Supplier` rows. Separate, already-logged, low-priority Backlog item.

**Dependencies:** None — first thing to build, several later WPs depend on it.

### WP-027 — Supplier Management UI (Senior React Engineer) — ✅ DONE, PUSHED

**Confirmed — Option A:** extend the existing `SuppliersPage.tsx` with management capability, rather than building a separate screen.

**Tasks:**
1. Add create/edit actions to the existing Suppliers page — building on the existing `SupplierGroupList` context.
2. All fields except Credit Limit — standard editable inputs, available to any user with base access.
3. **Credit Limit — visible to everyone, editable only for a Full Approver.** Plain read-only text for a Reviewer, a normal editable input for an Approver. Do not hide it, and do not show a disabled/greyed-out input.
4. The backend's explicit `403` (WP-026 Task 5) is the real enforcement — the frontend restriction is for a good experience, not the security boundary itself.

**Dependencies:** WP-026.

### WP-028 — *(retired)*
Invoice Approval Engine — already done in Sprint 1 (WP-050, WP-051, WP-053, WP-085, WP-086). Number retired, not reused.

### WP-029 — *(retired)*
Invoice Workflow API — already done in Sprint 1 (WP-054). Number retired, not reused.

### WP-030 — Invoice Workflow Dashboard (Senior React Engineer) — ✅ DONE, PUSHED
**Objective:** Replace the Dashboard placeholder with real status counts and recent activity.
**Confirmed:** Reuse `GET /api/invoices/folders` (WP-059) for status counts — no new endpoint needed for that part. Consider a "remittances pending Sage import" tile once WP-042 exists — see §8.
**Dependencies:** None (data already exists).

**Delivered:** `useDashboard` combines `supplierFolderClient.getFolderCounts` (WP-059, unchanged) with a new `dashboardClient.getRecentActivity`, which also needed no new endpoint — it reuses the already-live `GET /api/invoices` sorted by its own default field, `CreatedAtUtc`, descending, deliberately keeping its response DTO narrower than `invoiceClient.ts`'s own (only the four fields the feed renders). `StatusCountsGrid` renders one clickable tile per folder, linking to that status's Invoice Queue route via a new shared `navConfig.statusCodeToQueuePath` helper (extracted out of `buildInvoiceQueueLinks`'s own inline kebab-casing, so a tile and its matching nav sub-link can never disagree). `RecentActivityList` shows the 8 most-recently-created invoices, reusing `InvoiceStatusBadge`/`formatDateTime`, each linking to the Review Screen.

### WP-031 — Query Management: Outbound Supplier Email (Backend Engineer) — ✅ DONE, PUSHED (Mail.Send grant still pending — see top of document)
**Objective:** Actually send the query to the supplier by email.

**Confirmed clean, no collisions:** `EmailService`/`IEmailService` (WP-004) only ever performed mailbox *connection verification* — no send capability exists anywhere in the codebase.

**Confirmed resolved — no separate query-reason field needed:** the note already mandatory on every transition (WP-084) **is** the query reason. Do not build a second field.

**Tasks:**
1. Extend `GraphOptions` with the fields a send capability needs, following its existing documented pattern (client secret vs. Managed Identity fallback).
2. Add `Mail.Send` (Application) permission to the Graph app registration, admin consent re-granted.
3. Build the send mechanism as a new, small, focused service — sends from the existing `invoices@` mailbox.
4. When an invoice transitions to `Query Raised`, trigger the email send, using the existing mandatory note as the query content.
5. Shares its send mechanism with WP-040 (remittance email) — build once, both features call it.

**Dependencies:** None now.

### WP-032 — Query Management UI (Senior React Engineer)
**Objective:** Trigger sending a query, show its email-sent status.
**Confirmed resolved:** the existing mandatory-note flow already collects the query reason. This WP surfaces that the note-driven transition to `Query Raised` also resulted in a real email being sent.
**Dependencies:** WP-031.

### WP-033 — Statement Upload & Processing (Backend Engineer)
**Confirmed clean:** no `Statement` entity exists anywhere in `APFlow.Domain/Entities` — genuinely greenfield.
**Dependencies:** WP-026.

### WP-034 — Statement Management UI (Senior React Engineer)
**Dependencies:** WP-033.

### WP-035 — Credit Limit Monitoring (Backend Engineer)
**Confirmed:** `Invoice` already has `GrossTotal`, `Currency`, `SupplierId`, `Status`, and `DueDate` — everything needed to calculate outstanding balance vs. `Supplier.CreditLimit` (WP-026) and detect overdue invoices, without inventing new fields on either entity.
**Dependencies:** WP-026.

### WP-036 — Reminder & Notification Service (Backend Engineer)
**Confirmed pattern:** build as its own dedicated `BackgroundService` class, following `EmailIngestionWorker`'s exact structure (per-cycle DI scope, exceptions caught inside the cycle, timer loop) — not merged into the existing worker.
**Confirmed reusable template:** `IngestionIssue`'s dedup design (`OccurrenceCount`/`LastSeenUtc` keyed on a natural identity) is a good, direct template for notification dedup — a persistently-exceeded credit limit should update one record, not create a new one every cycle.
**Note:** Once WP-042 exists, a "remittance still pending Sage import after N hours" reminder is a natural fit for this same service — see §8.
**Dependencies:** WP-035.

### WP-037 — Notification Centre UI (Senior React Engineer)
**Dependencies:** WP-036.

### WP-038 — Remittance Generation Engine (Backend Engineer)
**Confirmed clean:** no `Remittance` entity exists anywhere — genuinely greenfield. `Invoice.GrossTotal`/`Currency`/`SupplierId` at `APPROVED` status are the fields to query against.
**Dependencies:** Existing Approved-status invoices (already exists).

### WP-039 — Remittance Management UI (Senior React Engineer)
**Objective, extended:** surface each remittance's Sage import status (Exported / Confirmed in Sage / Pending — see §8) once WP-042 exists.
**Dependencies:** WP-038, WP-042 (for the status display specifically).

### WP-040 — Email Remittance Service (Backend Engineer)
**Send identity — resolved:** same as WP-031, the existing `invoices@` mailbox with `Mail.Send` added. **Shares the exact send mechanism built in WP-031 Task 3** — do not build a second implementation.
**Dependencies:** WP-038, WP-031.

### WP-041 — Sage 50 Connector: Framework & Read Side (Backend Engineer)
**Objective:** Build our own lightweight Connector application, deployed on-premises at GB Skips, matching the already-approved architecture (SA-008: on-premises, outbound-only).
**Approach — confirmed:** use Sage 50 UK's native, free ODBC driver for the **read side**. No cost, no dependency on Grant explaining "Sage Copilot." **Not blocked on the Grant call.**
**Dependencies:** WP-026 (needs the Supplier entity to sync against).

### WP-042 — Sage 50 Connector: Export & Import-Status Tracking (Backend Engineer)
**Objective — confirmed approach:** ODBC read-only, no scheduled import capability exists in Sage 50 (§8's research). AP Flow generates an export file; GB Skips manually imports it; AP Flow's read-back tracks whether it was.

**Tracking design:**
- `ExportedAt` — when AP Flow generated the file.
- `ConfirmedInSageAt` — when the Connector's read-back first found the matching transaction (null until then).
- A visible **"Pending Sage Import"** state in the UI (WP-039).
- Optionally, a reminder (WP-036) if pending too long.

**Tasks (skeleton):**
1. Define the export file format Sage 50's import feature accepts — refine with the Grant call whenever it happens, don't block starting on a reasonable assumption.
2. Build export file generation from an approved remittance.
3. Build the read-back reconciliation check (via WP-041's ODBC read) — match on remittance reference/supplier/amount.
4. Expose the resulting status via API for WP-039.
5. Confirm live: export, manually import into a test Sage 50 company, confirm the Connector detects it.

**Dependencies:** WP-041, WP-038.

### WP-043 — System Status Page (Senior React Engineer) — ✅ DONE, not pushed/deployed/live-verified this round (commit only)

**Renamed from "Administration Portal," scope permanently reduced — decided 2026-08-07.**

**User/role management is retired from AP Flow's scope entirely — not deferred, not read-only, not revisited later.** The reasoning: everything AP Flow manages elsewhere (invoices, suppliers, notes) is tenant-scoped application data, protected by the existing `TenantEntity` boundary. Identity and role assignment in the CIAM tenant is **shared platform infrastructure** — the same tenant that will eventually serve every future AP Flow customer, per the Second-Tenant Readiness gate. Giving any single tenant's portal a path into that, even read-only, blurs a boundary that matters far more once a second tenant exists. Keeping it out entirely, permanently, is the correct call — not a temporary simplification.

**The replacement process:** GB Skips raises a request (however they'd raise any other request today — email, a call, whatever their existing channel is); a Platform Administrator actions it directly via the Entra portal or `az`, using the process already fully documented in WP-089 (`docs/GB_Skips_Production_Migration_Notes.md` Item 3). Nothing new needs building for this to work — it already works today.

**Noted for later:** if the Engineering Support Agent (`docs/Support-Agent-Architecture-Plan.md`, parked as WP-090) is ever built, "raise a role-change request" is a natural future ticket type for it — a human-in-the-loop request-and-action flow, not an in-app self-service feature. Not scoped now; just worth remembering so this isn't rediscovered as a "new" idea later.

**What remains, now genuinely small — this is the entire WP:**

1. A simple page reusing `/health/live`/`/health/ready` directly (no new backend logic — they already exist, WP-004/WP-024) — current status, and which component is degraded if any.
2. Display current app version/build info (already available via the deploy pipeline).
3. Gate to any authenticated user, or `FINANCE_MANAGER` if a role gate is wanted — low stakes either way, since there's nothing sensitive left on this page. **Recommend any authenticated user** — simpler, and appropriate for read-only operational status information.

**Explicitly out of scope, permanently:** user list, role display, role changes, group membership — any of it, in any form.

**Application settings, notification settings, accounting provider selection** — still genuinely open/deferred from the original scope (no concrete settings identified yet; notification/Sage settings depend on WP-036/037 and WP-041/042 existing first). Revisit only if a concrete need for an in-app settings screen emerges — not built speculatively.

**Dependencies:** None. Small enough to slot in wherever convenient.

**Delivered — two of this WP's own assumptions turned out to be wrong, confirmed by checking the real code first rather than assumed:**

1. **Task 1's "no new backend logic — they already exist" undersold what task 1 itself asks for.** `/health/live`/`/health/ready` existed and worked, but neither had a `ResponseWriter` configured anywhere (confirmed by grep) — ASP.NET Core's default health-check middleware writes only the aggregate status as plain text, with zero per-check detail. Literally satisfying "which component is degraded if any" needed one small addition: `ApiServiceCollectionExtensions.BuildHealthCheckResponse(HealthReport)` (a testable, extracted projection, same pattern `ConfigureCorsPolicy` already established) plus a JSON `ResponseWriter` wired onto both mappings. The health checks themselves are genuinely untouched — this is a response-shape addition, not new health-check logic.
2. **Task 2's "already available via the deploy pipeline" was not true.** Grepped the whole repo and `ci-cd.yml` for any version/commit stamping — none exists anywhere. Added the smallest real fix: `VITE_BUILD_SHA: ${{ github.sha }}` in `ci-cd.yml`'s existing "Build production bundle" step, the exact same `VITE_*` injection pattern already used for `VITE_ENTRA_CLIENT_ID`/etc. (WP-022). No backend version endpoint was built — the frontend bundle's own build commit is sufficient for "which build is this," and building a backend equivalent (Kudu deployment API calls, a stamped assembly attribute) would have been speculative scope beyond what this genuinely small WP needs.

**Also renamed the route/nav to match:** `/administration` → `/system-status`, nav label "Administration" → "System Status", `AdministrationPage.tsx` → `SystemStatusPage.tsx` (`git mv`, not delete+recreate). Kept "Administration" as a misleading label would have been actively wrong now that its user/role scope is permanently retired.

Task 3 (any authenticated user, no role gate) needed zero code — `ProtectedRoute` never checked roles to begin with, only authentication.

New tests: `HealthCheckResponseTests.cs` (3, backend), `systemStatusClient.test.ts` (4), `useSystemStatus.test.tsx` (3), `HealthReportCard.test.tsx` (4), `SystemStatusPage.test.tsx` (2). Full backend suite: 433 tests passing (up from 430). Full frontend suite: 367 tests passing (up from 354), `tsc -p tsconfig.app.json --noEmit` and `eslint` both clean.

### WP-044 — Scheduled Background Jobs (DevOps + Backend Engineer)
**Confirmed pattern:** same as WP-036 — its own dedicated `BackgroundService` class(es), following `EmailIngestionWorker`'s exact structure, not a second hosting paradigm (no Azure Functions/WebJobs needed).

### WP-045 — Sprint 2 QA & UAT (Senior QA Engineer)
Same philosophy as the improved WP-025: UI-first, console-error-strict, real accounts, real data, from the start. Add explicit coverage for WP-027's credit-limit permission split.

---

## 4. New work packages (not in the original 20) — continuing from WP-087

| WP | Title | Why it exists |
|---|---|---|
| **WP-087** | GB Skips Real Tenant — SSO Federation | `docs/GB_Skips_Production_Migration_Notes.md` Item 1 |
| **WP-088** | GB Skips Real Mailbox — App Registration | Same document, Item 2 |
| **WP-089** | Group-Based Role Assignment | Same document, Item 3 — rescoped, see below — **✅ DONE, LIVE, PUSHED** |
| **WP-090** | Engineering Support Agent (prototype) | `docs/Support-Agent-Architecture-Plan.md` — **parked, revisit towards the end of Sprint 2** |
| **WP-091** | Customer Support Agent (prototype) | Same document — **same parked status** |
| **WP-092** | Consistent Timestamp Display & Audit Trail Display Names | Two real gaps found live — **✅ DONE, PUSHED** |
| **WP-093** | Surface Suppliers With No Invoices Yet | Real gap found in WP-027's own delivery — **✅ DONE, PUSHED** |

### WP-089 — Group-Based Role Assignment (Entra/Azure AD Configuration — DevOps, not a coding task) — ✅ DONE, LIVE, PUSHED

**Rescoped.** Roles derive entirely from the Entra JWT `roles` claim — no local `UserRole` table exists. Group-based assignment requires **zero application code changes**.

**Tasks (all Entra/Azure AD configuration, no code):**
1. ✅ Created two Entra security groups — **APFlow Finance Managers** (`6e943cce-...`) / **APFlow AP Reviewers** (`4521e58d-...`), one per `APFlow-Api-Dev` app role.
2. ✅ Assigned each group the corresponding App Role on the API's Enterprise Application (service principal `0a857b93-...`) — **no P1 license needed, confirmed live** (the commonly-cited restriction is a portal UI limitation, not a Graph API one).
3. ✅ Moved WP-064's four test/demo users into the matching group, removed their direct per-user assignments — confirmed live via fresh ROPC tokens for both test accounts (identical `roles` claim, now group-sourced) and a real authenticated `GET /api/invoices/folders` call. Demo pair moved but otherwise left untouched, per WP-064's established convention.
4. ✅ Documented the process for adding a future user — `docs/GB_Skips_Production_Migration_Notes.md` Item 3. **This is now also the permanent, sole mechanism for all future role changes — see WP-043.**

**Dependencies:** None. Done before WP-043, as planned — and directly informed WP-043's scope retirement.

---

### WP-092 — Consistent Timestamp Display & Audit Trail Display Names (Backend + Frontend Engineer) — ✅ DONE, PUSHED

**Part A — Timestamps: viewer-local everywhere, including Received**
1. Audit every place a timestamp is shown and confirm each uses the existing shared `formatDateTime` (viewer-local) formatter.
2. Fix `Received` specifically — currently date-only; extend to date + time.
3. Fix anything else Task 1 finds inconsistent.

**Part B — Audit trail display names**
4. Add `PerformedByDisplayName` to `AuditLog`, populated at staging time from `ICurrentUserService.DisplayName` — same pattern as `InvoiceNote.AuthorDisplayName` (WP-055).
5. Generate the migration.
6. Update `AuditLogDto`/`AuditSummaryPanel`, with a readable fallback for historical rows with no captured name.
7. Tests: new entries carry the correct name; historical/null cases fall back sensibly.
8. Live-verify: a fresh action shows a real name; an old entry shows the fallback.

**Provide:** Files modified, migration, tests, live confirmation showing a fresh entry and a pre-existing entry side by side.

---

### WP-093 — Surface Suppliers With No Invoices Yet (Senior React Engineer) — ✅ DONE, PUSHED

**Priority: Medium — closes a real gap in WP-027's own "Add supplier" feature.** Found during WP-027's review: a supplier created via "+ Add supplier" is invisible on the Suppliers page again immediately after creation, because the page's existing browse view (`SupplierGroupList`) is invoice-grouped data, not a direct supplier listing — a brand-new supplier with zero invoices never appears there until its first invoice arrives.

**Tasks:**
1. Add a small section to `SuppliersPage` listing any `Supplier` from `useSuppliers()` (already built, WP-027) with no matching entry in the invoice-grouped list.
2. Each entry in this new section gets the same Edit action as an invoice-grouped supplier.
3. Live/tested confirmation: create a supplier, confirm it's immediately visible in this new section without needing an invoice first.

**Dependencies:** WP-027 (already done).

---

## 5. Suggested build order

1. **WP-026 → WP-027** (Suppliers) — ✅ done.
2. **WP-089 → WP-043** (role groups, then System Status) — WP-089 ✅ done; WP-043 ✅ done.
3. **WP-031 → WP-032, and WP-038 → WP-039/WP-040** — shared outbound-email capability. WP-031 ✅ done; WP-032/038/039/040 ready now.
4. **WP-033/034 → WP-035 → WP-036/037** (Statements → Credit Limits → Notifications) — ready now, all confirmed clean.
5. **WP-087/088** (real tenant migration) — parallel with anything above, gated on GB Skips' own IT.
6. **WP-041 → WP-042** (Sage Connector) — both unblocked, ready now.
7. **WP-030** and **WP-044** (scheduled jobs) — WP-030 ✅ done; WP-044 ready now.
8. **WP-092** — ✅ done.
9. **WP-093** — ✅ done.
10. **WP-045** (QA) — near the end.
11. **WP-090/091** (Support Agents) — parked, end of sprint.

**Everything in this sprint is now fully dispatch-ready** except the two parked Support Agent items.

---

## 6. Open questions

- None outstanding. Grant/Hometech call: no longer a blocker for anything, just useful for refining WP-042's export format whenever it happens.

---

## 7. What's already resolved — no longer open

- Outbound email send identity: same `invoices@` mailbox, `Mail.Send` added to the existing Graph app registration. Built once in WP-031, reused by WP-040. **Grant itself still pending — see top of document.**
- Support Agents: parked until later in the sprint.
- **Sage 50 write approach:** file-based export + manual import, AP Flow tracking. Not blocked on the Grant call.
- **Timestamp display:** viewer-local everywhere, extended to `Received` (WP-092, done).
- **Audit trail display names:** resolved to a real name at write time (WP-092, done).
- **WP-026/027/030 scope:** confirmed against the real, existing codebase — all three done.
- **WP-093:** zero-invoice supplier visibility gap closed — done, pushed.
- **WP-031/032 scope:** confirmed no separate query-reason field needed.
- **WP-033/034/036/037/038 scope:** all confirmed genuinely greenfield — no colliding entities exist.
- **WP-030/036/044 pattern:** confirmed — each a dedicated `BackgroundService` class following `EmailIngestionWorker`'s proven structure.
- **WP-089:** confirmed as Entra/Azure AD configuration, not application code — done and live, see `docs/GB_Skips_Production_Migration_Notes.md` Item 3. No P1 license required.
- **WP-043:** user/role management permanently retired as AP Flow scope. Renamed "System Status Page," reduced to health/version display only. Requests handled externally via the process WP-089 already documented. Done — both of its own scope assumptions (health response already had per-check detail; build info already available) turned out to be false, fixed as part of delivering it, not deferred.

---

## 8. Sage 50 — confirmed approach and research findings

### Read side — confirmed, free, unblocked

Sage 50 UK ships with a native ODBC driver — the standard way third-party applications read Sage 50 data as a regular database. No special licensing needed. WP-041 builds against this now.

### Write side — confirmed read-only natively; no scheduled import capability found either

1. **The native ODBC driver is read-only.** Confirmed directly from the Sage user community.
2. **No native scheduled or watched-folder import capability exists for transaction data.** Backups can be scheduled via Windows Task Scheduler — but nothing equivalent exists for importing transactions. Real businesses asking exactly this question are consistently pointed toward paid third-party connectors starting around $4,250/year — if a native automatic-import feature existed, that market wouldn't exist.

### Decision, confirmed

**AP Flow exports a properly-formatted file per approved remittance; a GB Skips staff member manually imports it into Sage 50.** No £4,000/year Developer Program subscription, no third-party commercial connector. AP Flow closes the automation gap by reading back Sage 50's data to confirm whether each export has actually been imported, and surfacing anything still pending — see WP-042.

### What the Grant call is for now (no longer a gate)

1. Which Sage 50 version GB Skips is running.
2. The exact file format Sage 50's import feature accepts.
3. Where the Connector would be hosted — same machine as Sage 50 or another on the same network; Windows Server version; .NET runtime availability; outbound firewall/proxy rules.
4. Practical logistics — who at GB Skips will perform the manual import day to day.

**Attendees, per the original ruling:** Chief Technical Architect, Integration Architect, Technical Lead. Product Owner gets a readout afterward.
