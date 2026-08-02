# Sprint 1 — Acceptance Criteria

**Status:** Ready for WP-025 execution — all blocking pre-conditions satisfied
**Prepared by:** Chief Technical Architect
**For:** Senior QA Engineer (WP-025)
**Revision:** v2, 2026-08-02 — updated following WP-061 through WP-076

---

## Pre-conditions — all satisfied, confirm rather than re-derive

- [x] Azure AI Document Intelligence resource exists and is live (WP-061), Managed Identity + `Cognitive Services User` RBAC, no key wired into the app.
- [x] `app-apflow-dev-api` runs in its real (non-Development) configuration with no manual override (WP-061), confirmed durable across subsequent redeploys.
- [x] Real end-to-end ingestion proven with live evidence — real Graph read, real PDF extraction, real Blob upload, real SQL row (WP-069, closing WP-063). Confirm this is *still* true by sending one fresh test email as part of WP-025 itself, rather than relying solely on dev's own historical proof.
- [x] GitHub Actions pipeline has completed multiple fully green runs, no manual intervention (first at WP-062, confirmed repeatedly since). Confirm the specific commit currently deployed matches `origin/main`'s latest before starting.
- [x] Two CIAM test users exist with different roles assigned and confirmed live via real tokens (WP-064): `apflow-test-approver@rameezjav.onmicrosoft.com` (`FINANCE_MANAGER`) and `apflow-test-reviewer@rameezjav.onmicrosoft.com` (`AP_REVIEWER`). Credentials in Key Vault (`kv-apflow-dev-ryd3y6`) — QA should retrieve them there, not request them in chat.

**New for this revision — worth knowing before starting:**
- Application Insights is genuinely wired up and receiving real telemetry (WP-066); a monitoring dashboard and five alert rules are live (WP-024) — see §12.
- The very first request immediately after any deploy has shown a recurring, understood, expected cold-start blip (404/500, self-resolves in a few seconds) across five separate deploys. **This is not a QA fail if hit** — retry once; if it doesn't clear within ~30 seconds, that would be new and worth flagging.
- No literal browser-driven verification (clicking, screenshots) has been performed by anyone yet — every prior confirmation in this project was done via real API calls, deployed-bundle inspection, and direct database/log queries. **WP-025 is the first genuine end-to-end UI walkthrough this system will have had.** Go in expecting to be the first real pair of eyes on the rendered application, not re-confirming something already visually checked.

---

## 1. Microsoft Entra authentication and role-based access

- [ ] A user can sign in via the real Entra External ID (CIAM) tenant — confirmed working at the API/token level (WP-064); **confirm in a real browser** for the first time.
- [ ] An unauthenticated request to any protected API endpoint returns `401`.
- [ ] Signed in as `apflow-test-approver`: the header shows "Signed in as: Full Approver" (WP-076).
- [ ] Signed in as `apflow-test-reviewer`: the header shows "Signed in as: Standard Reviewer".
- [ ] `FINANCE_MANAGER` sees and can execute Approve on a `CHECKED_READY_TO_APPROVE` invoice; `AP_REVIEWER` does not see the button at all (not disabled — absent), and a direct API attempt returns `403`. Already proven at the API level (WP-064) — confirm the UI genuinely hides it, not just that the backend blocks it.
- [ ] Session expiry forces re-sign-in rather than silently failing subsequent requests.

## 2–5. Mailbox connection, reading emails, PDF extraction, Blob storage

- [x] **Proven with live evidence already** (WP-069): real email in, real Graph read, PDF extracted, Blob upload confirmed, SQL row created. QA should still run this fresh, end to end, as part of independent sign-off — don't rely solely on dev's own historical proof.
- [ ] Send a new email with a PDF invoice attachment; confirm it's picked up within one polling cycle (~60s) and appears in Invoice Queue at `AWAITING_REVIEW` directly — **not** `EXTRACTED` (WP-071 fixed this; if you see an invoice stuck at `EXTRACTED`, that's a genuine regression).
- [ ] A non-PDF attachment on an otherwise-normal email is ignored, not processed.
- [ ] An email with **no** processable PDF attachment now appears in **Inbox** (WP-076, no longer a placeholder) — confirm it's listed with sender, subject, and date.
- [ ] Send the same no-attachment email again in the same thread (reply); confirm Inbox shows one entry with an incremented occurrence count, not two separate entries.
- [ ] Re-processing an identical file (same bytes) does not create a duplicate `Invoice` row (content-hash idempotency, WP-052 Part B).

## 6. Extracting invoice data using Azure AI Document Intelligence

- [x] **Proven working** — real fields extracted from real test invoices (WP-069, WP-071).
- [ ] Confirm extracted fields (supplier, invoice number, date, currency, net/VAT/gross) display correctly on the Invoice Review screen, each with a confidence score, including `Currency` (which always shows a blank/null confidence — WP-056, this is correct, not a bug).
- [ ] A deliberately unclear test PDF produces visibly lower confidence, correctly reflected in badge colour (≥0.85 high, 0.6–0.84 medium, <0.6 low).

## 7. Persisting invoices in Azure SQL

- [ ] `GET /api/invoices/{id}` (via the UI) shows all expected fields populated for a real invoice.
- [ ] Invoice Queue's list, search, filter, sort, and pagination all function against real data.

## 8. Detecting duplicate invoices

- [x] **Proven working** with real evidence (WP-071's flagged pair, WP-073's clickable reference).
- [ ] Two invoices with the same Supplier + Invoice Number are flagged: the second shows `IsPotentialDuplicate` styling and a **"View matching invoice →"** link (WP-073) — click it and confirm it navigates to the correct original invoice, not just displays a raw ID.
- [ ] The flagged invoice is **not blocked** — confirm it can still proceed through the normal workflow.
- [ ] Duplicate override remains interim-scoped to `FINANCE_MANAGER` only — the Product Owner's confirmation with GB Skips on who exactly counts as "authorised" is still outstanding; test against the current interim rule, don't treat a future change here as a defect.

## 9. React web interface for reviewing invoices

- [ ] Invoice Queue: search (invoice number only — supplier-name search was a known, accepted capability reduction from the original fixture version, confirm this matches current expectations), filter, sort, pagination all function against real data.
- [ ] Invoice Review screen: canonical fields, extracted fields with confidence, PDF viewer, audit history, Previous/Next navigation all function against real data.
- [ ] Notes: add a note, confirm correct server-resolved author display name and timestamp; confirm no edit/delete affordance exists anywhere.
- [ ] **Supplier & Folder Views now show real data** (WP-065 — no longer fixture-backed). Confirm the Suppliers view shows genuine suppliers from real ingested invoices, not placeholder names.
- [ ] **Query Queue is now a real, working view** (WP-074) — combined `NEEDS_QUERY`/`QUERY_RAISED`/`AWAITING_SUPPLIER_RESPONSE`. Confirm it shows the right invoices.
- [ ] Confirm **"Approved" no longer appears as a standalone nav link** (WP-074, deliberately removed) — approved invoices remain reachable via Invoice Queue's all-statuses view.

## 10. Managing the manual workflow

**Updated:** the live system uses GB Skips' full, confirmed 15-status template (WP-070/075), not the simplified 4-stage description originally circulated. Test against the real graph below, not the earlier simplified version.

- [ ] The nav's Invoice Queue sub-links show all 15 GB Skips statuses in the correct order, including `Checked & Ready to Approve` and `Needs Review by Febina` positioned between `Awaiting Review` and `Approved` (WP-075 — this was recently broken and fixed; worth confirming explicitly given the history).
- [ ] Every workflow action shown reflects only transitions genuinely valid for the invoice's status and the acting user's role (live from `available-actions`, WP-054).
- [ ] Full flow: `AWAITING_REVIEW → CHECKED_READY_TO_APPROVE` (Reviewer) → `APPROVED` (Approver only) works end-to-end.
- [ ] Confirm `AWAITING_REVIEW → APPROVED` directly is **not possible** for GB Skips (the two-tier gate can't be bypassed).
- [ ] Escalation (`NEEDS_REVIEW_FEBINA`) and reopen paths (`REJECTED → AWAITING_REVIEW`, `CANCELLED → RECEIVED`, both `FINANCE_MANAGER`-gated) all function as specified.
- [ ] An invalid or role-gated transition attempt is rejected with a clear message, and the invoice is left completely unchanged.

## 11. Recording notes and audit history

- [ ] Every status change, invoice creation, deletion, and note addition produces a corresponding audit entry (WP-052 Part C's extended scope — not just status changes).
- [ ] The audit trail is genuinely append-only.
- [ ] Recent audit entries display correctly on the Review screen.

## 12. Automated Azure deployment pipeline with monitoring and logging enabled

- [ ] Confirm the currently-deployed commit matches `origin/main`'s latest, and the pipeline's last run succeeded.
- [ ] `GET /health/live` → `200 Healthy`; `GET /health/ready` → `200`, `database: Healthy`. Graph/Blob `Degraded` remains expected and is not itself a fail condition.
- [ ] **New — confirm the monitoring dashboard is genuinely useful, not just present** (WP-024): open `dash-apflow-dev-ops` in the Azure Portal and confirm all 5 tiles show real data (already visually confirmed once by the Architect — QA should independently confirm too).
- [ ] Application Insights shows real request and dependency telemetry (SQL, Graph, Blob, Document Intelligence) from actual test activity performed during this QA pass.
- [ ] **Not required to test:** actually triggering an alert — this was deliberately not destructive-tested against the live dev environment; alert rule correctness was validated by executing each rule's KQL live, not by forcing a real failure.

---

## Sign-off

QA should return one of: **PASS**, **PASS WITH ISSUES** (each categorised Critical/High/Medium/Low), or **FAIL** — per the existing Sprint 1 QA work package format.

**Context for calibrating severity:** this system has been extensively live-verified at the API/data level throughout its build (documented in `docs/Backlog.md`'s Closed section), but this is its first real UI walkthrough. Finding UI-level issues that API-level testing couldn't have caught is the expected, valuable outcome of this pass — not a sign the system is less ready than believed. Weight findings accordingly: a rendering/UX issue is not the same class of problem as a data-integrity or security gap, even though both may surface for the first time in this pass.
