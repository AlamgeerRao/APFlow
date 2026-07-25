# AP Flow

AP Flow is a cloud-native, multi-tenant SaaS platform for Accounts Payable automation.

## Technology

- .NET 9
- ASP.NET Core
- React
- TypeScript
- Azure SQL
- Azure Blob Storage
- Microsoft Graph
- Azure AI Document Intelligence

## Solution

```
src/
tests/
docs/
```

## Status

Sprint 1 – MVP build in progress.

### Sprint 1 deliverable

A deployable MVP capable of:

- Microsoft Entra authentication and role-based access.
- Connecting to a Microsoft 365 mailbox using Microsoft Graph.
- Reading invoice emails.
- Extracting PDF attachments.
- Storing PDFs in Azure Blob Storage.
- Extracting invoice data using Azure AI Document Intelligence.
- Persisting invoices in Azure SQL.
- Detecting duplicate invoices.
- Providing a React web interface for reviewing invoices.
- Managing the manual workflow (Inbox → Needs Query → On Query → Approved).
- Recording notes and audit history.
- Running from an automated Azure deployment pipeline with monitoring and logging enabled.

### Work packages

| WP | Work Package | Role | Status |
|----|---|---|---|
| WP-001 | Solution Foundation | Backend Engineer | Done |
| WP-002 | Authentication & RBAC | Backend Engineer | Done |
| WP-003 | Database Foundation | Backend Engineer | Done |
| WP-004 | Microsoft Graph Integration | Backend Engineer | Done |
| WP-005 | Azure Blob Storage | Backend Engineer | Done |
| WP-006 | Email Synchronisation Service | Backend Engineer | Done |
| WP-007 | PDF Attachment Extraction | Backend Engineer | Done |
| WP-008 | Azure AI Document Intelligence Integration | Backend Engineer | Done |
| WP-009 | Invoice Domain Model & Persistence | Backend Engineer | Done |
| WP-010 | Duplicate Invoice Detection | Backend Engineer | Done |
| WP-011 | Invoice Repository & Query Services | Backend Engineer | Done. `IInvoiceQueryService` itself shipped with WP-011. It was wired to `GET /api/invoices` on 2026-07-25 while auditing the repo against recorded rulings, but that HTTP wiring was removed again the same day when `InvoicesController.cs` was overwritten wholesale by WP-055's merge (Chief Technical Architect ruling: WP-055's zip predates the WP-011 wiring and is authoritative for the controller) — the query service itself is untouched and fully tested, just not currently exposed over HTTP. Re-wiring is tracked in `docs/Backlog.md` |
| WP-012 | Invoice Processing Pipeline (orchestration only) | Backend Engineer | Done. Ruling (2026-07-25) required switching the idempotency key from `messageId`+`fileName` to a content hash — already satisfied by WP-052 Part B (`SourceDocumentContentHash`) — see `docs/WP-012-Invoice-Processing-Pipeline-Decisions.md` |
| WP-013 | Audit Logging & Activity History | Backend Engineer | Done. Ruling (2026-07-25) extended automatic audit logging beyond status changes to Create/Delete/AddNote — already satisfied by WP-052 Part C — see `docs/WP-013-Audit-Logging-Decisions.md` |
| WP-014 | Dashboard Shell & Navigation | Senior React Engineer | Done. All decisions approved as delivered — see `docs/WP-014-Dashboard-Shell-Decisions.md` |
| WP-015 | Invoice Work Queue | Senior React Engineer | Done. Ruling (2026-07-25) approved items 2-6. Its missing backend dependency (`GET /api/invoices`, item 1) was built 2026-07-25, then removed again the same day by the WP-055 merge (see WP-011 row) — item 1's dependency gap is therefore open again, tracked in `docs/Backlog.md`; the fixture-to-real-client swap itself remains additionally blocked on WP-002 (real frontend auth) — see `docs/WP-015-Invoice-Queue-Decisions.md` |
| WP-016 | Invoice Review Screen | Senior React Engineer | Done. Ruling (2026-07-25) approved items 1/3/4/5/6; its dependencies were already deployed. The fixture-to-real-client swap is blocked on WP-002 (real frontend auth), tracked in `docs/Backlog.md` — see `docs/WP-016-Invoice-Review-Decisions.md` |
| WP-017 | Notes & Comments Component | Senior React Engineer | Done. Ruling (2026-07-25) resolved item 1's three open questions; items 2-7 approved as delivered. The real API endpoint (`GET`/`POST /api/invoices/{id}/notes`, `AuthorDisplayName`) was first built 2026-07-25 during a repo-wide audit, then superseded/reworked the same day by WP-055's own delivery of the same feature area (Chief Technical Architect ruling: WP-055 authoritative) — same endpoints, but `AuthorDisplayName` is now `nvarchar(200)` (not 256) and `ICurrentUserService.DisplayName` is a required interface member (not default-implemented as null) — see `docs/WP-055-Invoice-Notes-Api-Decisions.md`. The fixture-to-real-client swap itself is blocked on WP-002 (real frontend auth), tracked in `docs/Backlog.md` — see `docs/WP-017-Invoice-Notes-Decisions.md` |
| WP-018 | Query / On Query / Approved Workflow UI | Senior React Engineer | Done. Ruling (2026-07-25) approved as delivered overall — the transition-graph/endpoint gap is closed by WP-053/WP-054. The fixture's role gating was corrected 2026-07-25 (four transitions, not one); the fixture-to-real-client swap itself is blocked on WP-002 (real frontend auth), tracked in `docs/Backlog.md` — see `docs/WP-018-Invoice-Workflow-Actions-Decisions.md` |
| WP-019 | Supplier Folder View | Senior React Engineer | Not started |
| WP-020 | API Integration & Error Handling | Senior React Engineer | Not started |
| WP-021 | Azure Infrastructure (App Service, SQL, Storage) | DevOps Engineer | Not started |
| WP-022 | CI/CD Pipeline (GitHub Actions) | DevOps Engineer | Not started |
| WP-023 | Application Configuration & Secrets (Key Vault) | DevOps Engineer | Not started |
| WP-024 | Logging, Monitoring & Application Insights | DevOps Engineer | Not started |
| WP-025 | Sprint 1 QA Review & Regression Testing | Senior QA Engineer | Not started |
| WP-046 | Role Catalogue Remediation (SA-007 E-05) | Backend Engineer | Done. Both flagged discrepancies confirmed/resolved by ruling (2026-07-25) — no `UserRole` table is intentional architecture; the missing EF Core migration mechanism was already resolved by WP-052 Part A — see `docs/WP-046-Role-Catalogue-Remediation.md` |
| WP-047 | Duplicate Matching Criteria Reconciliation | Backend Engineer | Done |
| WP-048 | Persist Duplicate Detection Result; Pure-Compute Detection Service | Backend Engineer | Done |
| WP-049 | Duplicate Check Auto-Invocation in Processing Pipeline | Backend Engineer | Done. Replaces the prior ad-hoc three-commit adaptation (create → advance status → persist duplicate flag) with a true atomic single-save pipeline. Ruling (2026-07-25) confirmed the design and clarified `RECEIVED`/`PROCESSING` remain valid graph edges in general, just skipped by this pipeline — see `docs/WP-049-Wire-Duplicate-Detection-Into-Pipeline.md` |
| WP-050 | Tenant-Configurable Workflow Engine | Backend Engineer | Schema, seed data (statuses only), and a fully-tested validation mechanism delivered; its central open item (transition enforcement not enabled) was subsequently closed by WP-053 — see `docs/WP-050-Workflow-Engine-Decisions.md` |
| WP-051 | Confirm GB Skips Role Mapping (Full/Approver → FINANCE_MANAGER) | Chief Technical Architect / Product Owner | Done. Confirmed FINANCE_MANAGER as GB Skips' Full/Approver tier and enforces it via a new role-gated `ApprovalPolicy` mechanism on the `CHECKED_READY_TO_APPROVE` → `APPROVED` transition — see `docs/WP-051-Approval-Policy-Decisions.md` |
| WP-052 | Pipeline & API Hardening (EF Core migrations, content-hash idempotency, extended audit logging, Invoice Detail API) | Backend Engineer | Done. Ruling (2026-07-25) approved Parts A/B/C exactly as delivered; Part D's per-field extraction-confidence gap tracked as WP-056 in `docs/Backlog.md`, and its field-name mismatch against WP-015 ruled in the backend's favour — see `docs/WP-052-Pipeline-And-Api-Hardening-Decisions.md` |
| WP-053 | Seed & Enable Workflow Transition Enforcement | Backend Engineer | Done. Seeds both templates' full confirmed transition graphs (57 rows) and wires `IWorkflowValidationService` + a generalised four-transition role gate live into `InvoiceService.UpdateAsync`, closing WP-050's central open item. Ruling (2026-07-25) resolved both flagged discrepancies; the `DUPLICATE_SUSPECTED` `StatusReference` row cleanup is tracked as WP-057 in `docs/Backlog.md` — see `docs/WP-053-Transition-Enforcement-Decisions.md` |
| WP-054 | Invoice Workflow Transition API | Backend Engineer | Done. Exposes WP-053's enforcement over HTTP — `GET /api/invoices/{id}/available-actions` and `PATCH /api/invoices/{id}/status` — as thin wrappers over the existing `IWorkflowValidationService`/`IApprovalAuthorizationService`/`InvoiceService.UpdateAsync` path; no new transition or role-gating rule introduced. One naming discrepancy flagged (the task's example `Workflow.RoleNotPermitted` code doesn't exist — mapped to the real `Approval.Unauthorized`/`Approval.PolicyNotConfigured` codes instead), not silently invented — see `docs/WP-054-Invoice-Workflow-Transition-Api-Decisions.md` |
| WP-055 | Invoice Notes API | Backend Engineer | Done. Delivers `GET`/`POST /api/invoices/{id}/notes` and `InvoiceNote.AuthorDisplayName`. Per Chief Technical Architect ruling, this **supersedes/reworks** the ad-hoc WP-017 notes-API implementation added during the 2026-07-25 repo-wide audit (commit `ed044b8`): same endpoint shapes, but `AuthorDisplayName` is `nvarchar(200)` (not 256) and `ICurrentUserService.DisplayName` is now a required interface member (not default-implemented as null) — every implementer of that interface was updated accordingly. Merging this WP's zip (which predates WP-011's HTTP wiring) also **removed `GET /api/invoices`** — `InvoicesController.cs` was adopted wholesale per the same ruling, reopening WP-015's item 1 dependency (see WP-011/WP-015 rows, and `docs/Backlog.md`) — see `docs/WP-055-Invoice-Notes-Api-Decisions.md` |
| WP-056 | Persist Per-Field Extraction Confidence | Backend Engineer | Done. Closes the WP-052 Part D gap — new `InvoiceExtractedField` child entity, populated by the ingestion pipeline, replaces `GET /api/invoices/{id}`'s `ExtractionConfidenceNote` placeholder with real `extractedFields` data. Chief Technical Architect ruling (2026-07-25) on the one flagged scope question: `Currency` is included as its own row (with a null `ConfidenceScore`), not excluded — one row per `InvoiceExtractionResult` field, always. This WP's own source drop predated WP-011's `Search` parameter and WP-055's rework; only the files WP-056 itself owns were merged, and its EF migration was regenerated fresh against `main` rather than copied from the drop — see `docs/WP-056-Persist-Extraction-Confidence-Decisions.md` |
| WP-057 | Retire `DUPLICATE_SUSPECTED` Status Row | Backend Engineer | Done. Completes the `StatusReference` deletion WP-053 deferred (its own scope was transition enforcement, not status catalogue changes) — both templates' `DUPLICATE_SUSPECTED` rows removed via migration `RemoveDuplicateSuspectedStatus`; `InvoiceStatusCodes.DuplicateSuspected` (the C# constant) deliberately kept for any historical `Invoice.Status` value. This WP's own source drop predated WP-056; only the files WP-057 itself owns were merged, and its EF migration was regenerated fresh against `main`. `docs/AI/06_Domain_Reference_Data.md` §2's `DUPLICATE_SUSPECTED` row was flagged as stale here and separately reported to the Chief Technical Architect — **the Architect's own maintained copy already had it removed**; the mismatch was this repo's tracked copy lagging behind, the same stale-copy problem WP-053 hit. Corrected 2026-07-25 by committing the Architect's copy directly (see the WP-053 note below) — see `docs/WP-057-Retire-Duplicate-Suspected-Status-Decisions.md` |

All decision docs raised to date have been ruled on by the Chief Technical
Architect. Several rulings left a concrete implementation item still
outstanding — these remain tracked in `docs/Backlog.md`, not here:
`docs/WP-015-Invoice-Queue-Decisions.md`, `docs/WP-016-Invoice-Review-Decisions.md`,
`docs/WP-017-Invoice-Notes-Decisions.md`, `docs/WP-018-Invoice-Workflow-Actions-Decisions.md`
(all four: fixture-to-real-client reconciliation), plus WP-057 raised by the
WP-053 ruling below (WP-056, also raised by a ruling below, is itself now
delivered — see that row above).

**2026-07-25 repo-wide audit against recorded rulings:** closed the backend
gaps those four rulings assumed were already available — `GET /api/invoices`
(WP-015's dependency, never actually wired to `InvoicesController` despite
WP-011 shipping the underlying query service) and the full Notes API
(WP-017's ruled shape: separate resource, server-resolved
`AuthorDisplayName`, `AddNoteAsync` returning the created note) — and
corrected WP-018's fixture to gate all four real role-gated transitions, not
one. Also **discovered `APFlow.Web` has no real authentication at all** -
`AuthContext.tsx` is still WP-014's tenant/role picker stub, no MSAL or HTTP
client exists, and the API requires a real Bearer token unconditionally
(no dev-auth bypass). This blocks every fixture-to-real-client swap
(WP-015/016/017/018) regardless of backend readiness - tracked as its own
`docs/Backlog.md` item ahead of those four.

**2026-07-25 WP-055 merge — supersedes the above audit's notes-API work:**
WP-055's own work-package drop (built independently, against the pre-audit
codebase state) was ruled by the Chief Technical Architect to be authoritative
for the Invoice Notes API feature area, superseding the ad-hoc implementation
the audit above had just added. Net effect: the notes endpoints and
`AuthorDisplayName` column remain delivered, but with two concrete differences
(200-character column, required `ICurrentUserService.DisplayName` member) —
see the WP-055 row above and `docs/WP-055-Invoice-Notes-Api-Decisions.md`. The
ruling also accepted, as a deliberate trade-off rather than an oversight, that
adopting WP-055's `InvoicesController.cs` wholesale **removes `GET
/api/invoices`** again, since that file predates the audit's WP-011 wiring —
this reopens WP-015 item 1's dependency gap, tracked in `docs/Backlog.md`.

Resolved architecture decisions — ruling recorded 2026-07-20; follow-up implementation tracked in `docs/Backlog.md`:

- `docs/WP-004-Graph-Multitenancy-Decision.md`
- `docs/WP-004-Health-Check-Severity-Decision.md`
- `docs/WP-010-Duplicate-Flag-Persistence-Decision.md`

Resolved architecture decisions — ruling recorded 2026-07-25:

- `docs/WP-012-Invoice-Processing-Pipeline-Decisions.md` — item 2's idempotency-key change (content hash, not `messageId`+`fileName`) was already independently implemented by WP-052 Part B; item 3's fuzzy-matching gap is tracked as a non-blocking `docs/Backlog.md` item.
- `docs/WP-013-Audit-Logging-Decisions.md` — item 3's scope extension (audit Create/Delete/AddNote, not just status changes) was already independently implemented by WP-052 Part C.
- `docs/WP-014-Dashboard-Shell-Decisions.md` — all items approved as delivered.
- `docs/WP-046-Role-Catalogue-Remediation.md` — both flagged discrepancies confirmed/resolved; the missing EF Core migration mechanism was already independently resolved by WP-052 Part A.
- `docs/WP-015-Invoice-Queue-Decisions.md` — items 2-6 approved; item 1's fixture reconciliation against WP-011's real DTO remains tracked in `docs/Backlog.md`.
- `docs/WP-016-Invoice-Review-Decisions.md` — items 1/3/4/5/6 approved, including the `pdfUrl` proxied-endpoint decision (already implemented by WP-052 Part D); item 2's fixture reconciliation remains tracked in `docs/Backlog.md`.
- `docs/WP-017-Invoice-Notes-Decisions.md` — item 1's three open questions ruled (separate resource, server-resolved `AuthorDisplayName`, `AddNoteAsync` return-shape change); items 2-7 approved as delivered. The real endpoint was built to satisfy this ruling, then **superseded/reworked by WP-055** (see below) the same day — the endpoint remains delivered, just via WP-055's own implementation.
- `docs/WP-018-Invoice-Workflow-Actions-Decisions.md` — approved as delivered overall; the fixture-to-real-client swap remains tracked in `docs/Backlog.md`, with two specifics called out (full `InvoiceDetail` response; four gated transitions).
- `docs/WP-049-Wire-Duplicate-Detection-Into-Pipeline.md` — atomicity design confirmed correct; `RECEIVED`/`PROCESSING` clarified as valid graph edges in general, not retired states.
- `docs/WP-052-Pipeline-And-Api-Hardening-Decisions.md` — Parts A/B/C approved exactly as delivered; Part D's extraction-confidence gap is now **closed by WP-056** (see that row above and `docs/WP-056-Persist-Extraction-Confidence-Decisions.md`); its WP-015 field-name mismatch ruled in the backend's favour (frontend adapts).
- `docs/WP-053-Transition-Enforcement-Decisions.md` — both discrepancies resolved; the `DUPLICATE_SUSPECTED` `StatusReference` row cleanup is now **closed by WP-057** (see that row above and `docs/WP-057-Retire-Duplicate-Suspected-Status-Decisions.md`). This ruling's own note (that `docs/AI/06_Domain_Reference_Data.md` already had `DUPLICATE_SUSPECTED` removed) is **now confirmed correct and resolved 2026-07-25**: the Architect's maintained copy always had it removed — this repository's tracked copy was simply lagging behind and has now been committed to match, closing the same stale-copy problem flagged again in the WP-057 status report.
- `docs/WP-054-Invoice-Workflow-Transition-Api-Decisions.md` — approved as delivered; the `Workflow.RoleNotPermitted` naming discrepancy resolved (mapped to the real `Approval.*` codes).
- `docs/WP-055-Invoice-Notes-Api-Decisions.md` — the Chief Technical Architect ruled WP-055's independently-built drop authoritative for the Invoice Notes API feature area, superseding the WP-017-ruling-driven implementation the same-day audit had just added (see WP-017 row above): `AuthorDisplayName` is `nvarchar(200)` (not 256) and `ICurrentUserService.DisplayName` is a required member (not default-implemented). Accepted, as an explicit part of the same ruling, that merging WP-055's `InvoicesController.cs` wholesale removes `GET /api/invoices` again — see WP-011/WP-015 rows and `docs/Backlog.md`.

Other resolved architecture decisions (closed independently of the 2026-07-20 ruling batch, via implementation/verification or a later work package's ruling):

- `docs/WP-003-Tenant-Isolation-Decision.md` — resolved in WP-009, the document's own trigger condition (first `TenantEntity`-derived entities landed).
- `docs/WP-005-Blob-Storage-Tenant-Isolation-Decision.md` — resolved 2026-07-18; blob-name prefixing enforced in `BlobStorageService`, implemented and verified.
- `docs/WP-047-Duplicate-Matching-Reconciliation.md` — complete; its one provisional item (FINANCE_MANAGER as GB Skips' Full/Approver mapping) was subsequently confirmed by WP-051.
- `docs/WP-051-Approval-Policy-Decisions.md` — complete for everything the codebase needs today; Task 5 is documentation-only (WP-038 doesn't exist).
- `docs/WP-050-Workflow-Engine-Decisions.md` — its central open item (GB Skips' proposed transition set, the undocumented platform-default transition graph, and enabling enforcement) was subsequently confirmed, seeded, and wired live by WP-053. The GB Skips placeholder tenant ID (a separate, pre-existing item — see the doc's own note) remains open.